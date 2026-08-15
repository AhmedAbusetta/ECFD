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
    Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] pcmAudio, CancellationToken cancellationToken = default);
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

public interface ISignalRNotifier
{
    Task NotifyCallStartedAsync(CallSession session);
    Task NotifyTranscriptFinalAsync(Guid sessionId, TranscriptSegment segment);
    Task NotifyTacticDetectedAsync(Guid sessionId, Evidence evidence);
    Task NotifyStageChangedAsync(Guid sessionId, AttackStage previousStage, AttackStage newStage, string trigger);
    Task NotifyRiskUpdatedAsync(Guid sessionId, RiskResult risk);
    Task NotifyAlertRaisedAsync(Guid sessionId, Alert alert);
    Task NotifyCallEndedAsync(Guid sessionId);
}
