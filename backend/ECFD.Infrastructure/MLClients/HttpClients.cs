using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECFD.Infrastructure.MLClients;

/// <summary>Bound from the "MlServices" configuration section.</summary>
public class MlServicesOptions
{
    public const string SectionName = "MlServices";

    /// <summary>true = in-process mocks (no Python needed); false = call the FastAPI services.</summary>
    public bool UseMocks { get; set; } = true;
    public string AsrUrl { get; set; } = "http://localhost:8001";
    public string NlpUrl { get; set; } = "http://localhost:8002";
    public int AsrTimeoutSeconds { get; set; } = 60;
    public int NlpTimeoutSeconds { get; set; } = 10;

    /// <summary>Ping AsrUrl/health at startup so a scaled-to-zero cloud GPU is warm before the first call.</summary>
    public bool AsrWarmUpOnStart { get; set; } = true;
    /// <summary>Ping again every N minutes while the backend runs (below Modal's 5-minute scale-down), so the
    /// GPU stays loaded for the whole session. 0 = only at startup.</summary>
    public int AsrKeepWarmMinutes { get; set; } = 4;

    /// <summary>AI call analyst service (ml/brain/app.py). Empty = analyst disabled, rules only.</summary>
    public string? AnalystUrl { get; set; }
    /// <summary>The analyst runs in the background, so a slow LLM never blocks the call.</summary>
    public int AnalystTimeoutSeconds { get; set; } = 90;

    /// <summary>Voice anti-spoofing service (ml/antispoof). Empty = mock (no voice evidence).</summary>
    public string? AntiSpoofUrl { get; set; }
    /// <summary>Runs in the background on the caller's sentences; long enough for a GPU cold start.</summary>
    public int AntiSpoofTimeoutSeconds { get; set; } = 120;
}

// Wire contracts from docs/03_ECFD_Technical_Architecture.md (camelCase JSON).
internal record AsrRequestDto(string SessionId, string SegmentId, int SampleRate, string AudioFormat, string LanguageHint, string AudioBase64);
internal record AsrResponseDto(string SegmentId, string Text, float Confidence, bool IsFinal, long StartMs, long EndMs, string ModelVersion, double InferenceDurationMs);
internal record AnalystTurnRequestDto(string SessionId, string Speaker, string Text, string? CallerLine);
internal record AnalystEndRequestDto(string SessionId);
internal record NlpRequestDto(string SessionId, string SegmentId, string Text, string Language);
internal record NlpTacticDto(string Type, float Confidence);
internal record VoiceRequestDto(string SessionId, string WindowId, int SampleRate, string AudioFormat, string AudioBase64);
internal record VoiceResponseDto(string WindowId, float SpoofProbability, float QualityScore, string ModelVersion, double InferenceDurationMs);
internal record NlpResponseDto(string SegmentId, List<NlpTacticDto> Tactics, string ModelVersion, double InferenceDurationMs);

public class HttpAntiSpoofClient : IAntiSpoofClient
{
    private readonly HttpClient _http;

    public HttpAntiSpoofClient(HttpClient http)
    {
        _http = http;
    }

    /// <param name="pcmAudio">16 kHz mono 16-bit little-endian PCM (one sentence).</param>
    public async Task<VoiceAnalysisResult> AnalyzeVoiceAsync(Guid sessionId, Guid windowId, byte[] pcmAudio, CancellationToken cancellationToken = default)
    {
        var request = new VoiceRequestDto(sessionId.ToString(), windowId.ToString(), 16000, "pcm_s16le", Convert.ToBase64String(pcmAudio));
        using var response = await _http.PostAsJsonAsync("/v1/voice/analyze", request, cancellationToken);
        await HttpAsrClient.EnsureSuccess(response, "Anti-spoofing", cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<VoiceResponseDto>(cancellationToken)
                  ?? throw new MlServiceException("Anti-spoofing returned an empty response");
        return new VoiceAnalysisResult(windowId, dto.SpoofProbability, dto.QualityScore, dto.ModelVersion);
    }
}

public class HttpAsrClient : IAsrClient
{
    private readonly HttpClient _http;

    public HttpAsrClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] audio, string audioFormat = "pcm_s16le", CancellationToken cancellationToken = default)
    {
        var request = new AsrRequestDto(sessionId.ToString(), segmentId.ToString(), 16000, audioFormat, "ar", Convert.ToBase64String(audio));
        using var response = await PostWithOneRetryAsync(request, cancellationToken);
        await EnsureSuccess(response, "ASR", cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<AsrResponseDto>(cancellationToken)
                  ?? throw new MlServiceException("ASR returned an empty response");
        return new AsrResult(segmentId, dto.Text, dto.Confidence, dto.IsFinal, dto.StartMs, dto.EndMs, dto.ModelVersion);
    }

    /// <summary>A server error or a dropped connection is retried once at once; a timeout is not (that is the cold-start wait).</summary>
    private async Task<HttpResponseMessage> PostWithOneRetryAsync(AsrRequestDto request, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/v1/asr/analyze", request, ct);
            if ((int)response.StatusCode < 500)
            {
                return response;
            }
            response.Dispose();
        }
        catch (HttpRequestException)
        {
        }
        await Task.Delay(300, ct);
        return await _http.PostAsJsonAsync("/v1/asr/analyze", request, ct);
    }

    internal static async Task EnsureSuccess(HttpResponseMessage response, string service, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new MlServiceException($"{service} service returned {(int)response.StatusCode}: {body}");
        }
    }
}

public class HttpAnalystClient : IAnalystClient
{
    private readonly HttpClient _http;

    public HttpAnalystClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, string? callerLine = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/v1/analyst/turn",
            new AnalystTurnRequestDto(sessionId.ToString(), speaker, text, callerLine), cancellationToken);
        await HttpAsrClient.EnsureSuccess(response, "Analyst", cancellationToken);
        return await response.Content.ReadFromJsonAsync<AnalystResult>(cancellationToken)
               ?? throw new MlServiceException("Analyst returned an empty response");
    }

    public async Task EndCallAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/v1/analyst/end",
            new AnalystEndRequestDto(sessionId.ToString()), cancellationToken);
    }
}

/// <summary>Used when no AnalystUrl is configured (or with mocks): the rules alone score the call.</summary>
public class DisabledAnalystClient : IAnalystClient
{
    public Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, string? callerLine = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<AnalystResult?>(null);

    public Task EndCallAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class HttpNlpClient : INlpClient
{
    private readonly HttpClient _http;

    public HttpNlpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<NlpResult> AnalyzeTextAsync(Guid sessionId, Guid segmentId, string text, CancellationToken cancellationToken = default)
    {
        var request = new NlpRequestDto(sessionId.ToString(), segmentId.ToString(), text, "ar");
        using var response = await _http.PostAsJsonAsync("/v1/nlp/analyze", request, cancellationToken);
        await HttpAsrClient.EnsureSuccess(response, "NLP", cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<NlpResponseDto>(cancellationToken)
                  ?? throw new MlServiceException("NLP returned an empty response");
        var tactics = dto.Tactics.ConvertAll(t => new TacticMatch(t.Type, t.Confidence));
        return new NlpResult(segmentId, tactics, dto.ModelVersion);
    }
}

public class MlServiceException : Exception
{
    public MlServiceException(string message) : base(message)
    {
    }
}
