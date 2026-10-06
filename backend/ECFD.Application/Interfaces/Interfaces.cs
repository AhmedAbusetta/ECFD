using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;
using ECFD.Application.Risk;

namespace ECFD.Application.Interfaces;

public record AsrResult(Guid SegmentId, string Text, float Confidence, bool IsFinal, long StartMs, long EndMs, string ModelVersion);
public record TacticMatch(string Type, float Confidence);
public record NlpResult(Guid SegmentId, List<TacticMatch> Tactics, string ModelVersion);
public record VoiceAnalysisResult(Guid WindowId, float SpoofProbability, float QualityScore, string ModelVersion);

public interface IAsrClient
{
    // audioFormat: "pcm_s16le" (16 kHz mono, from the Media Gateway) or a container such as "wav" / "webm".
    Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] audio, string audioFormat = "pcm_s16le", CancellationToken cancellationToken = default);
}

public interface INlpClient
{
    Task<NlpResult> AnalyzeTextAsync(Guid sessionId, Guid segmentId, string text, CancellationToken cancellationToken = default);
}

public interface IAntiSpoofClient
{
    Task<VoiceAnalysisResult> AnalyzeVoiceAsync(Guid sessionId, Guid windowId, byte[] pcmAudio, CancellationToken cancellationToken = default);
}

public interface IRiskEngine
{
    RiskResult Calculate(IReadOnlyCollection<Evidence> evidenceList, AttackStage currentStage);
}

public interface IAttackProgressionEngine
{
    (AttackStage NewStage, bool Transitioned, string Trigger) ProcessEvidence(AttackStage currentStage, Evidence newEvidence);
}

/// <summary>One quoted piece of evidence from the AI call analyst (turn numbers are 1-based).</summary>
public record AnalystEvidence(int Turn, string Speaker, string Label, string Quote);

/// <summary>The AI call analyst's view of the whole call after one turn (ml/brain, ADR-0005).</summary>
public record AnalystResult(
    int Turn, bool Analyzed, int Risk, string Level, bool LevelChanged, int? LlmRisk, int Floor,
    string Stage, string Trend, string EmployeeState, string CallerGoal, string Strategy, string NextLikelyMove,
    string AlertAr, List<string> PolicyViolations, List<AnalystEvidence> Evidence, List<string> HardSignals,
    List<string> Reasons, string Model, int LatencyMs, string? Error);

public interface IAnalystClient
{
    /// <summary>Send one finished turn; returns null when the analyst is disabled.</summary>
    Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, CancellationToken cancellationToken = default);
    Task EndCallAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public interface ISignalRNotifier
{
    Task NotifyCallStartedAsync(CallSession session);
    Task NotifyTranscriptFinalAsync(Guid sessionId, TranscriptSegment segment);
    /// <summary>Live text while the speaker is still talking; replaced by the final segment. Empty text clears it.</summary>
    Task NotifyTranscriptPartialAsync(Guid sessionId, string utteranceId, string speaker, string text);
    Task NotifyTacticDetectedAsync(Guid sessionId, Evidence evidence);
    Task NotifyStageChangedAsync(Guid sessionId, AttackStage previousStage, AttackStage newStage, string trigger);
    Task NotifyRiskUpdatedAsync(Guid sessionId, RiskResult risk);
    Task NotifyAlertRaisedAsync(Guid sessionId, Alert alert);
    Task NotifyAnalystUpdatedAsync(Guid sessionId, AnalystResult result);
    Task NotifyCallEndedAsync(Guid sessionId);
}
