using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;

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
public record SimulateUtteranceRequest(string Text);

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly ISignalRNotifier _notifier;
    private readonly INlpClient _nlpClient;
    private readonly IAttackProgressionEngine _progressionEngine;
    private readonly IRiskEngine _riskEngine;

    private static CallSession? _activeSession;

    public DemoController(
        ISignalRNotifier notifier,
        INlpClient nlpClient,
        IAttackProgressionEngine progressionEngine,
        IRiskEngine riskEngine)
    {
        _notifier = notifier;
        _nlpClient = nlpClient;
        _progressionEngine = progressionEngine;
        _riskEngine = riskEngine;
    }

    [HttpPost("start-call")]
    public async Task<IActionResult> StartCall([FromBody] SimulateCallRequest request)
    {
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

        // 1. Create Transcript
        var segment = new TranscriptSegment
        {
            CallSessionId = _activeSession.Id,
            Text = request.Text,
            Confidence = 0.96f,
            IsFinal = true
        };
        await _notifier.NotifyTranscriptFinalAsync(_activeSession.Id, segment);

        // 2. Classify Tactics
        var nlpResult = await _nlpClient.AnalyzeTextAsync(_activeSession.Id, segment.Id, request.Text);
        foreach (var t in nlpResult.Tactics)
        {
            if (Enum.TryParse<EvidenceType>(t.Type, true, out var evType))
            {
                var evidence = new Evidence
                {
                    CallSessionId = _activeSession.Id,
                    TranscriptSegmentId = segment.Id,
                    Type = evType,
                    Confidence = t.Confidence,
                    Source = nlpResult.ModelVersion
                };
                _activeSession.EvidenceList.Add(evidence);
                await _notifier.NotifyTacticDetectedAsync(_activeSession.Id, evidence);

                // 3. Attack Progression
                var (newStage, transitioned, trigger) = _progressionEngine.ProcessEvidence(_activeSession.CurrentStage, evidence);
                if (transitioned)
                {
                    var prev = _activeSession.CurrentStage;
                    _activeSession.CurrentStage = newStage;
                    await _notifier.NotifyStageChangedAsync(_activeSession.Id, prev, newStage, trigger);
                }
            }
        }

        // 4. Calculate Risk
        var riskResult = _riskEngine.Calculate(_activeSession.EvidenceList, _activeSession.CurrentStage);
        _activeSession.CurrentRisk = riskResult.Score;
        await _notifier.NotifyRiskUpdatedAsync(_activeSession.Id, riskResult);

        // 5. Raise Alert if High/Critical
        if (riskResult.Severity == RiskSeverity.Critical)
        {
            var alert = new Alert
            {
                CallSessionId = _activeSession.Id,
                Severity = RiskSeverity.Critical,
                Title = "CRITICAL: Potential Social Engineering Attack",
                Description = "High confidence credential extraction attempt detected."
            };
            await _notifier.NotifyAlertRaisedAsync(_activeSession.Id, alert);
        }

        return Ok(new
        {
            sessionId = _activeSession.Id,
            text = request.Text,
            tactics = nlpResult.Tactics,
            stage = _activeSession.CurrentStage.ToString(),
            riskScore = riskResult.Score,
            severity = riskResult.Severity.ToString()
        });
    }

    [HttpPost("end-call")]
    public async Task<IActionResult> EndCall()
    {
        if (_activeSession != null)
        {
            await _notifier.NotifyCallEndedAsync(_activeSession.Id);
            _activeSession = null;
        }
        return Ok(new { message = "Call ended" });
    }
}
