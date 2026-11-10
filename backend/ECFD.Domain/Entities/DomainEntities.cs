using System;
using System.Collections.Generic;
using ECFD.Domain.Enums;

namespace ECFD.Domain.Entities;

public class CallSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalCallId { get; set; } = string.Empty;
    public string CallerEndpoint { get; set; } = string.Empty;
    public string CalleeEndpoint { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public CallStatus Status { get; set; } = CallStatus.Ringing;
    public int CurrentRisk { get; set; } = 0;
    /// <summary>Latest risk from the AI call analyst (LLM + deterministic floor); CurrentRisk = max(rules, analyst).</summary>
    public int AnalystRisk { get; set; } = 0;
    public AttackStage CurrentStage { get; set; } = AttackStage.Normal;
    /// <summary>EXTERNAL or INTERNAL: where the phone system says the caller's line is. Null = unknown (simulated call).</summary>
    public string? CallerLine { get; set; }
    public int PeakRisk { get; set; }
    /// <summary>Risk the call can no longer fall below: set when the instant rules hear a forbidden request.</summary>
    public int RequestFloor { get; set; }
    /// <summary>Spoken warnings played to the employee on this call.</summary>
    public List<EmployeeWarningRecord> Warnings { get; set; } = new();

    public List<CallParticipant> Participants { get; set; } = new();
    public List<TranscriptSegment> TranscriptSegments { get; set; } = new();
    public List<Evidence> EvidenceList { get; set; } = new();
    public List<AttackEvent> AttackEvents { get; set; } = new();
    public List<RiskSnapshot> RiskSnapshots { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
}

/// <summary>One spoken warning: which one, and how long after the caller's sentence ended it started playing.</summary>
public record EmployeeWarningRecord(string Kind, int? DelayMs, DateTime At);

public class CallParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public string EndpointId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Employee" or "Caller"
    public string DisplayName { get; set; } = string.Empty;
}

public class TranscriptSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public int SequenceNo { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>CALLER or EMPLOYEE - from the phone leg, never inferred from the words.</summary>
    public string Speaker { get; set; } = "CALLER";
    public float Confidence { get; set; }
    public bool IsFinal { get; set; }
    public string ModelVersion { get; set; } = "asr-v1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>When the speaker stopped talking (PBX calls); used to time how fast a warning follows.</summary>
    public DateTime? SpeechEndedAt { get; set; }
}

public class Evidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public Guid? TranscriptSegmentId { get; set; }
    public EvidenceType Type { get; set; }
    public string Source { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = "{}";
    public string? ModelVersion { get; set; }
}

public class AttackEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public AttackStage PreviousStage { get; set; }
    public AttackStage NewStage { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public float TriggerConfidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RiskSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public int Score { get; set; }
    public RiskSeverity Severity { get; set; }
    public float ContentRisk { get; set; }
    public float ProgressionRisk { get; set; }
    public float VoiceRisk { get; set; }
    public float ContextRisk { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallSessionId { get; set; }
    public RiskSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Acknowledged { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
