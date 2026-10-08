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

    /// <summary>Optional second ASR (e.g. local faster-whisper) used when AsrUrl fails or is too slow. Empty = no fallback.</summary>
    public string? AsrFallbackUrl { get; set; }
    /// <summary>How long the primary ASR gets before the fallback takes over (a Modal cold start takes ~60 s).</summary>
    public int AsrFallbackAfterSeconds { get; set; } = 8;
    /// <summary>Ping AsrUrl/health at startup so a scaled-to-zero cloud GPU is warm before the first call.</summary>
    public bool AsrWarmUpOnStart { get; set; } = true;
    /// <summary>Ping again every N minutes while the backend runs (below Modal's 5-minute scale-down), so the
    /// GPU stays loaded for the whole session. 0 = only at startup.</summary>
    public int AsrKeepWarmMinutes { get; set; } = 4;

    /// <summary>AI call analyst service (ml/brain/app.py). Empty = analyst disabled, rules only.</summary>
    public string? AnalystUrl { get; set; }
    /// <summary>The analyst runs in the background, so a slow LLM never blocks the call.</summary>
    public int AnalystTimeoutSeconds { get; set; } = 90;
}

// Wire contracts from docs/03_ECFD_Technical_Architecture.md (camelCase JSON).
internal record AsrRequestDto(string SessionId, string SegmentId, int SampleRate, string AudioFormat, string LanguageHint, string AudioBase64);
internal record AsrResponseDto(string SegmentId, string Text, float Confidence, bool IsFinal, long StartMs, long EndMs, string ModelVersion, double InferenceDurationMs);
internal record AnalystTurnRequestDto(string SessionId, string Speaker, string Text);
internal record AnalystEndRequestDto(string SessionId);
internal record NlpRequestDto(string SessionId, string SegmentId, string Text, string Language);
internal record NlpTacticDto(string Type, float Confidence);
internal record NlpResponseDto(string SegmentId, List<NlpTacticDto> Tactics, string ModelVersion, double InferenceDurationMs);

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
        using var response = await _http.PostAsJsonAsync("/v1/asr/analyze", request, cancellationToken);
        await EnsureSuccess(response, "ASR", cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<AsrResponseDto>(cancellationToken)
                  ?? throw new MlServiceException("ASR returned an empty response");
        return new AsrResult(segmentId, dto.Text, dto.Confidence, dto.IsFinal, dto.StartMs, dto.EndMs, dto.ModelVersion);
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

/// <summary>
/// Tries the primary ASR (cloud GPU) and, if it errors or exceeds its time budget, re-sends the
/// same audio to the fallback ASR (local faster-whisper) so a call never stalls on the cloud.
/// </summary>
public class FallbackAsrClient : IAsrClient
{
    private readonly IAsrClient _primary;
    private readonly IAsrClient _fallback;
    private readonly TimeSpan _primaryBudget;
    private readonly TimeSpan _cooldown;
    private readonly ILogger<FallbackAsrClient> _logger;
    private long _skipPrimaryUntilTicks;

    /// <param name="cooldown">After the primary fails, go straight to the fallback for this long instead of
    /// making every sentence wait out the budget again (default 30 s).</param>
    public FallbackAsrClient(IAsrClient primary, IAsrClient fallback, TimeSpan primaryBudget, ILogger<FallbackAsrClient> logger,
        TimeSpan? cooldown = null)
    {
        _primary = primary;
        _fallback = fallback;
        _primaryBudget = primaryBudget;
        _cooldown = cooldown ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] audio, string audioFormat = "pcm_s16le", CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _skipPrimaryUntilTicks))
        {
            return await _fallback.AnalyzeAudioAsync(sessionId, segmentId, audio, audioFormat, cancellationToken);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_primaryBudget);
        try
        {
            return await _primary.AnalyzeAudioAsync(sessionId, segmentId, audio, audioFormat, budget.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref _skipPrimaryUntilTicks, DateTime.UtcNow.Add(_cooldown).Ticks);
            _logger.LogWarning("Primary ASR failed or exceeded {Budget}s for segment {SegmentId} ({Error}); using fallback ASR for the next {Cooldown}s.",
                _primaryBudget.TotalSeconds, segmentId, ex.Message, _cooldown.TotalSeconds);
            return await _fallback.AnalyzeAudioAsync(sessionId, segmentId, audio, audioFormat, cancellationToken);
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

    public async Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/v1/analyst/turn",
            new AnalystTurnRequestDto(sessionId.ToString(), speaker, text), cancellationToken);
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
    public Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, CancellationToken cancellationToken = default)
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
