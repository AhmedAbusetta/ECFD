using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Infrastructure.SignalR;

public class SignalRNotifier : ISignalRNotifier
{
    private readonly IHubContext<Hub> _hubContext;

    public SignalRNotifier(IHubContext<Hub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyCallStartedAsync(CallSession session)
    {
        await _hubContext.Clients.All.SendAsync("call.started", new
        {
            sessionId = session.Id,
            externalCallId = session.ExternalCallId,
            caller = session.CallerEndpoint,
            callee = session.CalleeEndpoint,
            startedAt = session.StartedAt
        });
    }

    public async Task NotifyTranscriptFinalAsync(Guid sessionId, TranscriptSegment segment)
    {
        await _hubContext.Clients.All.SendAsync("transcript.final", new
        {
            sessionId,
            segmentId = segment.Id,
            text = segment.Text,
            confidence = segment.Confidence,
            isFinal = segment.IsFinal,
            startMs = segment.StartMs,
            endMs = segment.EndMs
        });
    }

    public async Task NotifyTacticDetectedAsync(Guid sessionId, Evidence evidence)
    {
        await _hubContext.Clients.All.SendAsync("tactic.detected", new
        {
            sessionId,
            evidenceId = evidence.Id,
            tactic = evidence.Type.ToString(),
            confidence = evidence.Confidence,
            timestamp = evidence.Timestamp
        });
    }

    public async Task NotifyStageChangedAsync(Guid sessionId, AttackStage previousStage, AttackStage newStage, string trigger)
    {
        await _hubContext.Clients.All.SendAsync("stage.changed", new
        {
            sessionId,
            previousStage = previousStage.ToString(),
            newStage = newStage.ToString(),
            trigger
        });
    }

    public async Task NotifyRiskUpdatedAsync(Guid sessionId, RiskResult risk)
    {
        await _hubContext.Clients.All.SendAsync("risk.updated", new
        {
            sessionId,
            riskScore = risk.Score,
            severity = risk.Severity.ToString(),
            topContributors = risk.TopContributors,
            stage = risk.Stage.ToString()
        });
    }

    public async Task NotifyAlertRaisedAsync(Guid sessionId, Alert alert)
    {
        await _hubContext.Clients.All.SendAsync("alert.raised", new
        {
            sessionId,
            alertId = alert.Id,
            severity = alert.Severity.ToString(),
            title = alert.Title,
            description = alert.Description,
            createdAt = alert.CreatedAt
        });
    }

    public async Task NotifyCallEndedAsync(Guid sessionId)
    {
        await _hubContext.Clients.All.SendAsync("call.ended", new { sessionId });
    }
}
