using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
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

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly ISignalRNotifier _notifier;
    private readonly IAsrClient _asrClient;
    private readonly INlpClient _nlpClient;
    private readonly IAttackProgressionEngine _progressionEngine;
    private readonly IRiskEngine _riskEngine;
    private readonly IAnalystClient _analystClient;
    private readonly ILogger<DemoController> _logger;

    private const string RulesAlertTitle = "CRITICAL: Potential Social Engineering Attack";
    private const string AnalystAlertTitle = "AI analyst: social engineering in progress";

    private static CallSession? _activeSession;
    // Utterances whose final transcription has started: a late live (partial) result must not overwrite it.
    private static readonly ConcurrentDictionary<string, byte> _finalizedUtterances = new();

    public DemoController(
        ISignalRNotifier notifier,
        IAsrClient asrClient,
        INlpClient nlpClient,
        IAttackProgressionEngine progressionEngine,
        IRiskEngine riskEngine,
        IAnalystClient analystClient,
        ILogger<DemoController> logger)
    {
        _notifier = notifier;
        _asrClient = asrClient;
        _nlpClient = nlpClient;
        _progressionEngine = progressionEngine;
        _riskEngine = riskEngine;
        _analystClient = analystClient;
        _logger = logger;
    }

    [HttpPost("start-call")]
    public async Task<IActionResult> StartCall([FromBody] SimulateCallRequest request)
    {
        _finalizedUtterances.Clear();
        _activeSession = new CallSession
        {
            ExternalCallId = "CALL-" + Guid.NewGuid().ToString().Substring(0, 8),
            CallerEndpoint = request.Caller ?? "1002 (Attacker)",
            CalleeEndpoint = request.Callee ?? "1001 (Employee)",
            Status = CallStatus.Analyzing
        };

        await _notifier.NotifyCallStartedAsync(_activeSession);
        return Ok(_activeSession);
    }

    [HttpPost("utterance")]
    public async Task<IActionResult> ProcessUtterance([FromBody] SimulateUtteranceRequest request)
    {
        if (_activeSession == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Utterance text is required.");
        }

        var segment = new TranscriptSegment
        {
            CallSessionId = _activeSession.Id,
            Text = request.Text,
            Confidence = 0.96f,
            IsFinal = true,
            ModelVersion = "typed-text",
            Speaker = SpeakerOf(request.Speaker)
        };
        return Ok(await AnalyzeTranscriptAsync(_activeSession, segment));
    }

    /// <summary>
    /// Spoken utterance: a short recording (browser microphone webm/ogg, or a wav clip)
    /// goes through ASR and then the same tactic / progression / risk pipeline.
    /// </summary>
    [HttpPost("audio")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ProcessAudio(IFormFile audio, [FromForm] string? speaker, [FromForm] string? utteranceId, CancellationToken cancellationToken)
    {
        if (_activeSession == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }
        if (!string.IsNullOrEmpty(utteranceId))
        {
            _finalizedUtterances[utteranceId] = 0;
        }

        if (audio == null || audio.Length == 0)
        {
            return BadRequest("An audio file is required (form field 'audio').");
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await audio.CopyToAsync(ms, cancellationToken);
            bytes = ms.ToArray();
        }

        var segmentId = Guid.NewGuid();
        AsrResult asr;
        try
        {
            asr = await _asrClient.AnalyzeAudioAsync(_activeSession.Id, segmentId, bytes, AudioFormatOf(audio), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or MlServiceException)
        {
            _logger.LogError(ex, "ASR call failed");
            await _notifier.NotifyTranscriptPartialAsync(_activeSession.Id, utteranceId ?? "", SpeakerOf(speaker), "");
            return StatusCode(StatusCodes.Status502BadGateway, $"ASR service failed: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(asr.Text))
        {
            await _notifier.NotifyTranscriptPartialAsync(_activeSession.Id, utteranceId ?? "", SpeakerOf(speaker), "");
            return Ok(new { sessionId = _activeSession.Id, text = "", note = "No speech detected." });
        }

        var segment = new TranscriptSegment
        {
            Id = segmentId,
            CallSessionId = _activeSession.Id,
            Text = asr.Text,
            Confidence = asr.Confidence,
            IsFinal = asr.IsFinal,
            StartMs = asr.StartMs,
            EndMs = asr.EndMs,
            ModelVersion = asr.ModelVersion,
            Speaker = SpeakerOf(speaker)
        };
        return Ok(await AnalyzeTranscriptAsync(_activeSession, segment));
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
        var session = _activeSession;
        if (session == null)
        {
            return BadRequest("No active call session. Call /api/demo/start-call first.");
        }
        if (audio == null || audio.Length == 0)
        {
            return BadRequest("An audio file is required (form field 'audio').");
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await audio.CopyToAsync(ms, cancellationToken);
            bytes = ms.ToArray();
        }

        AsrResult asr;
        try
        {
            asr = await _asrClient.AnalyzeAudioAsync(session.Id, Guid.NewGuid(), bytes, AudioFormatOf(audio), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or MlServiceException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, $"ASR service failed: {ex.Message}");
        }

        var stale = !string.IsNullOrEmpty(utteranceId) && _finalizedUtterances.ContainsKey(utteranceId);
        if (!stale && !string.IsNullOrWhiteSpace(asr.Text))
        {
            await _notifier.NotifyTranscriptPartialAsync(session.Id, utteranceId ?? "", SpeakerOf(speaker), asr.Text);
        }
        return Ok(new { text = asr.Text, stale });
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

    private static string SpeakerOf(string? speaker) =>
        string.Equals(speaker?.Trim(), "EMPLOYEE", StringComparison.OrdinalIgnoreCase) ? "EMPLOYEE" : "CALLER";

    private async Task<object> AnalyzeTranscriptAsync(CallSession session, TranscriptSegment segment)
    {
        lock (session)
        {
            segment.SequenceNo = session.TranscriptSegments.Count + 1;
            session.TranscriptSegments.Add(segment);
        }
        await _notifier.NotifyTranscriptFinalAsync(session.Id, segment);

        // AI call analyst (ADR-0005): judges the whole call in the background and pushes its result
        // over SignalR when it arrives, so a slow LLM never blocks this fast rule-based path.
        _ = Task.Run(() => RunAnalystAsync(session, segment));

        // 2. Classify Tactics - fast rules, caller turns only (tactics are what the caller does)
        var tactics = new List<TacticMatch>();
        if (segment.Speaker == "CALLER")
        {
            var nlpResult = await _nlpClient.AnalyzeTextAsync(session.Id, segment.Id, segment.Text);
            tactics = nlpResult.Tactics;
            foreach (var t in nlpResult.Tactics)
            {
                if (EvidenceTypeLabels.TryParse(t.Type, out var evType))
                {
                    var evidence = new Evidence
                    {
                        CallSessionId = session.Id,
                        TranscriptSegmentId = segment.Id,
                        Type = evType,
                        Confidence = t.Confidence,
                        Source = nlpResult.ModelVersion
                    };
                    AttackStage prev, newStage;
                    bool transitioned;
                    string trigger;
                    lock (session)
                    {
                        session.EvidenceList.Add(evidence);
                        // 3. Attack Progression
                        prev = session.CurrentStage;
                        (newStage, transitioned, trigger) = _progressionEngine.ProcessEvidence(session.CurrentStage, evidence);
                        if (transitioned)
                        {
                            session.CurrentStage = newStage;
                        }
                    }
                    await _notifier.NotifyTacticDetectedAsync(session.Id, evidence);
                    if (transitioned)
                    {
                        await _notifier.NotifyStageChangedAsync(session.Id, prev, newStage, trigger);
                    }
                }
                else
                {
                    _logger.LogWarning("Ignoring unknown tactic label '{Label}' from {Model}", t.Type, nlpResult.ModelVersion);
                }
            }
        }

        // 4. Calculate Risk: rules, fused with the analyst's latest view of the call
        RiskResult riskResult;
        Alert? alert = null;
        lock (session)
        {
            var rules = _riskEngine.Calculate(session.EvidenceList.ToList(), session.CurrentStage);
            riskResult = RiskFusion.Fuse(rules, session.AnalystRisk, session.CurrentStage);
            session.CurrentRisk = riskResult.Score;

            // 5. Raise one rules-based Critical alert per call (not one per utterance once the score is high)
            if (rules.Severity == RiskSeverity.Critical && !session.Alerts.Any(a => a.Title == RulesAlertTitle))
            {
                alert = new Alert
                {
                    CallSessionId = session.Id,
                    Severity = RiskSeverity.Critical,
                    Title = RulesAlertTitle,
                    Description = "High confidence credential extraction attempt detected."
                };
                session.Alerts.Add(alert);
            }
        }
        await _notifier.NotifyRiskUpdatedAsync(session.Id, riskResult);
        if (alert != null)
        {
            await _notifier.NotifyAlertRaisedAsync(session.Id, alert);
        }

        return new
        {
            sessionId = session.Id,
            text = segment.Text,
            speaker = segment.Speaker,
            asrModel = segment.ModelVersion,
            asrConfidence = segment.Confidence,
            tactics,
            stage = riskResult.Stage.ToString(),
            riskScore = riskResult.Score,
            severity = riskResult.Severity.ToString()
        };
    }

    private async Task RunAnalystAsync(CallSession session, TranscriptSegment segment)
    {
        try
        {
            var result = await _analystClient.AnalyzeTurnAsync(session.Id, segment.Speaker, segment.Text);
            if (result == null)
            {
                return; // analyst disabled
            }

            RiskResult fused;
            AttackStage previousStage;
            bool stageAdvanced;
            Alert? alert = null;
            lock (session)
            {
                if (_activeSession?.Id != session.Id)
                {
                    return; // the call ended while the LLM was thinking
                }
                previousStage = session.CurrentStage;
                stageAdvanced = RiskFusion.TryAdvanceStage(session.CurrentStage, result.Stage, out var next);
                session.CurrentStage = next;
                session.AnalystRisk = result.Risk;
                var rules = _riskEngine.Calculate(session.EvidenceList.ToList(), session.CurrentStage);
                fused = RiskFusion.Fuse(rules, session.AnalystRisk, session.CurrentStage);
                session.CurrentRisk = fused.Score;

                if (result.Level == "ALERT" && !session.Alerts.Any(a => a.Title == AnalystAlertTitle))
                {
                    alert = new Alert
                    {
                        CallSessionId = session.Id,
                        Severity = RiskSeverity.Critical,
                        Title = AnalystAlertTitle,
                        Description = string.Join("\n", new[] { result.AlertAr, result.Strategy }.Where(x => !string.IsNullOrWhiteSpace(x)))
                    };
                    session.Alerts.Add(alert);
                }
            }

            await _notifier.NotifyAnalystUpdatedAsync(session.Id, result);
            if (stageAdvanced)
            {
                await _notifier.NotifyStageChangedAsync(session.Id, previousStage, fused.Stage, "AI_CALL_ANALYST");
            }
            await _notifier.NotifyRiskUpdatedAsync(session.Id, fused);
            if (alert != null)
            {
                await _notifier.NotifyAlertRaisedAsync(session.Id, alert);
            }
        }
        catch (Exception ex)
        {
            // The rules already scored this turn; the analyst is an addition, never a dependency.
            _logger.LogWarning("AI call analyst failed for turn {Seq}: {Error}", segment.SequenceNo, ex.Message);
        }
    }

    [HttpPost("end-call")]
    public async Task<IActionResult> EndCall()
    {
        if (_activeSession != null)
        {
            var sessionId = _activeSession.Id;
            await _notifier.NotifyCallEndedAsync(sessionId);
            _activeSession = null;
            _ = _analystClient.EndCallAsync(sessionId).ContinueWith(
                t => _logger.LogDebug("Analyst end-call failed: {Error}", t.Exception?.GetBaseException().Message),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        return Ok(new { message = "Call ended" });
    }
}
