using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ECFD.Api.Pipeline;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Infrastructure.MLClients;

namespace ECFD.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "HEALTHY",
            system = "ECFD Backend (.NET 8)",
            timestamp = DateTime.UtcNow,
            version = "0.1.0-alpha"
        });
    }
}

public record SimulateCallRequest(string Caller, string Callee);
/// <summary>Speaker: "CALLER" (default) or "EMPLOYEE" - in a real call this comes from the phone leg.</summary>
public record SimulateUtteranceRequest(string Text, string? Speaker = null);

/// <summary>
/// Simulated calls from the dashboard (microphone, recorded clips or typed text). Real PBX calls take the
/// same <see cref="CallPipeline"/> through the Asterisk taps instead.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly CallPipeline _pipeline;
    private readonly ILogger<DemoController> _logger;

    // the dashboard drives one simulated call at a time
    private static Guid? _demoSessionId;

    public DemoController(CallPipeline pipeline, ILogger<DemoController> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    private CallSession? DemoSession => _demoSessionId is { } id ? _pipeline.Get(id) : null;

    [HttpPost("start-call")]
    public async Task<IActionResult> StartCall([FromBody] SimulateCallRequest request)
    {
        if (_demoSessionId is { } previous)
        {
            await _pipeline.EndCallAsync(previous);
        }
        var session = await _pipeline.StartCallAsync(
            "CALL-" + Guid.NewGuid().ToString().Substring(0, 8),
            request.Caller ?? "1002 (Attacker)",
            request.Callee ?? "1001 (Employee)");
        _demoSessionId = session.Id;
        return Ok(session);
    }

    [HttpPost("utterance")]
    public async Task<IActionResult> ProcessUtterance([FromBody] SimulateUtteranceRequest request)
    {
        var session = DemoSession;
        if (session == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Utterance text is required.");
        }

        var segment = new TranscriptSegment
        {
            CallSessionId = session.Id,
            Text = request.Text,
            Confidence = 0.96f,
            IsFinal = true,
            ModelVersion = "typed-text",
            Speaker = CallPipeline.SpeakerOf(request.Speaker)
        };
        return Ok(await _pipeline.AnalyzeSegmentAsync(session, segment));
    }

    /// <summary>
    /// Spoken utterance: a short recording (browser microphone webm/ogg, or a wav clip)
    /// goes through ASR and then the same tactic / progression / risk pipeline.
    /// </summary>
    [HttpPost("audio")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ProcessAudio(IFormFile audio, [FromForm] string? speaker, [FromForm] string? utteranceId, CancellationToken cancellationToken)
    {
        var session = DemoSession;
        if (session == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }
        _pipeline.MarkFinalized(utteranceId);

        if (audio == null || audio.Length == 0)
        {
            return BadRequest("An audio file is required (form field 'audio').");
        }

        var who = CallPipeline.SpeakerOf(speaker);
        AsrResult asr;
        try
        {
            asr = await _pipeline.TranscribeAsync(session, await ReadAsync(audio, cancellationToken), AudioFormatOf(audio), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or MlServiceException)
        {
            _logger.LogError(ex, "ASR call failed");
            await _pipeline.ClearPartialAsync(session, utteranceId, who);
            return StatusCode(StatusCodes.Status502BadGateway, $"ASR service failed: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(asr.Text))
        {
            await _pipeline.ClearPartialAsync(session, utteranceId, who);
            return Ok(new { sessionId = session.Id, text = "", note = "No speech detected." });
        }

        var segment = new TranscriptSegment
        {
            CallSessionId = session.Id,
            Text = asr.Text,
            Confidence = asr.Confidence,
            IsFinal = asr.IsFinal,
            StartMs = asr.StartMs,
            EndMs = asr.EndMs,
            ModelVersion = asr.ModelVersion,
            Speaker = who
        };
        return Ok(await _pipeline.AnalyzeSegmentAsync(session, segment));
    }

    /// <summary>
    /// Live transcript while the speaker is still talking: the dashboard re-sends the audio recorded so
    /// far about once a second. Only ASR runs (no tactics, no analyst); the text is pushed as
    /// transcript.partial and replaced by the final segment when the recording stops.
    /// </summary>
    [HttpPost("audio-partial")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ProcessAudioPartial(IFormFile audio, [FromForm] string? speaker, [FromForm] string? utteranceId, CancellationToken cancellationToken)
    {
        var session = DemoSession;
        if (session == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }
        if (audio == null || audio.Length == 0)
        {
            return BadRequest("An audio file is required (form field 'audio').");
        }

        AsrResult asr;
        try
        {
            asr = await _pipeline.TranscribeAsync(session, await ReadAsync(audio, cancellationToken), AudioFormatOf(audio), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or MlServiceException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, $"ASR service failed: {ex.Message}");
        }

        var pushed = await _pipeline.PushPartialAsync(session, utteranceId, CallPipeline.SpeakerOf(speaker), asr.Text);
        return Ok(new { text = asr.Text, stale = _pipeline.IsFinalized(utteranceId) && !pushed });
    }

    [HttpPost("end-call")]
    public async Task<IActionResult> EndCall()
    {
        if (_demoSessionId is { } id)
        {
            _demoSessionId = null;
            await _pipeline.EndCallAsync(id);
        }
        return Ok(new { message = "Call ended" });
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string AudioFormatOf(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext))
        {
            return ext;
        }
        // e.g. "audio/webm;codecs=opus" -> "webm"
        var subtype = file.ContentType.Split(';')[0].Split('/').LastOrDefault();
        return string.IsNullOrEmpty(subtype) ? "wav" : subtype;
    }
}
