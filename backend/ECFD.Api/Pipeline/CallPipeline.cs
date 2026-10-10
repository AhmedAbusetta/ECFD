using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ECFD.Application.Alerts;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Application.Voice;
using ECFD.Infrastructure.MLClients;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Api.Pipeline;

/// <summary>What one analysed turn returns to the caller of the pipeline (and the demo API).</summary>
public record TurnResult(Guid SessionId, string Text, string Speaker, string AsrModel, float AsrConfidence,
    List<TacticMatch> Tactics, string Stage, int RiskScore, string Severity);

/// <summary>
/// The call pipeline shared by real PBX calls and the dashboard's simulated calls:
/// transcript turn -> rules (tactics, attack stage, risk) instantly, plus the AI call analyst in the
/// background (ADR-0005), with every step pushed to the dashboard over SignalR.
/// </summary>
public class CallPipeline
{
    public const string RulesAlertTitle = "CRITICAL: Potential Social Engineering Attack";
    public const string AnalystAlertTitle = "AI analyst: social engineering in progress";

    private readonly IServiceProvider _services;
    private readonly ISignalRNotifier _notifier;
    private readonly IAttackProgressionEngine _progressionEngine;
    private readonly IRiskEngine _riskEngine;
    private readonly ILogger<CallPipeline> _logger;

    private readonly ConcurrentDictionary<Guid, CallSession> _active = new();
    // Utterances whose final transcription has started: a late live (partial) result must not overwrite it.
    private readonly ConcurrentDictionary<string, byte> _finalizedUtterances = new();
    // Anti-spoofing scores of each call's caller sentences, combined into one call-level score.
    private readonly ConcurrentDictionary<Guid, VoiceSpoofTracker> _voice = new();

    public CallPipeline(IServiceProvider services, ISignalRNotifier notifier, IAttackProgressionEngine progressionEngine,
        IRiskEngine riskEngine, ILogger<CallPipeline> logger)
    {
        _services = services;
        _notifier = notifier;
        _progressionEngine = progressionEngine;
        _riskEngine = riskEngine;
        _logger = logger;
    }

    // The ML clients are transient (typed HttpClients), so resolve them per use.
    private IAsrClient Asr => _services.GetRequiredService<IAsrClient>();
    private INlpClient Nlp => _services.GetRequiredService<INlpClient>();
    private IAnalystClient Analyst => _services.GetRequiredService<IAnalystClient>();
    private IAntiSpoofClient AntiSpoof => _services.GetRequiredService<IAntiSpoofClient>();
    // Resolved per use: the telephony warner depends on the call sink, which depends on this pipeline.
    private IEmployeeWarner? Warner => _services.GetService<IEmployeeWarner>();

    /// <summary>On a PBX call, speak the fitting warning into the employee's ear. Never blocks or fails the turn.</summary>
    private void WarnEmployee(CallSession session, IEnumerable<string>? hardSignals)
    {
        var warner = Warner;
        if (warner == null)
        {
            return;
        }
        List<EvidenceType> evidence;
        lock (session)
        {
            evidence = session.EvidenceList.Select(e => e.Type).ToList();
        }
        var kind = EmployeeWarnings.Pick(hardSignals, evidence);
        _ = Task.Run(async () =>
        {
            try
            {
                if (await warner.WarnEmployeeAsync(session.Id, kind))
                {
                    await _notifier.NotifyEmployeeWarnedAsync(session.Id, kind);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Employee voice warning failed: {Error}", ex.Message);
            }
        });
    }

    public static string SpeakerOf(string? speaker) =>
        string.Equals(speaker?.Trim(), "EMPLOYEE", StringComparison.OrdinalIgnoreCase) ? "EMPLOYEE" : "CALLER";

    public async Task<CallSession> StartCallAsync(string externalCallId, string callerEndpoint, string calleeEndpoint)
    {
        var session = new CallSession
        {
            ExternalCallId = externalCallId,
            CallerEndpoint = callerEndpoint,
            CalleeEndpoint = calleeEndpoint,
            Status = CallStatus.Analyzing
        };
        _active[session.Id] = session;
        await _notifier.NotifyCallStartedAsync(session);
        return session;
    }

    public CallSession? Get(Guid sessionId) => _active.TryGetValue(sessionId, out var s) ? s : null;

    public bool IsActive(Guid sessionId) => _active.ContainsKey(sessionId);

    public async Task EndCallAsync(Guid sessionId)
    {
        if (!_active.TryRemove(sessionId, out var session))
        {
            return;
        }
        lock (session)
        {
            session.Status = CallStatus.Ended;
            session.EndedAt = DateTime.UtcNow;
        }
        _voice.TryRemove(sessionId, out _);
        await _notifier.NotifyCallEndedAsync(sessionId);
        _ = Analyst.EndCallAsync(sessionId).ContinueWith(
            t => _logger.LogDebug("Analyst end-call failed: {Error}", t.Exception?.GetBaseException().Message),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Scores one caller sentence for a synthetic voice, in the background (never delays the transcript).
    /// Once the call-level score (<see cref="VoiceSpoofTracker"/>) is suspicious, it becomes VoiceSpoof evidence:
    /// the risk engine adds at most 30 points for it, so a voice score alone never raises a critical alert.
    /// </summary>
    public void AnalyzeVoiceInBackground(CallSession session, byte[] pcm16le)
    {
        if (AntiSpoof is MockAntiSpoofClient)
        {
            return; // no detector configured: show nothing rather than a made-up score
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await AnalyzeVoiceAsync(session, pcm16le);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Voice anti-spoofing failed for call {Session}: {Error}", session.Id, ex.Message);
            }
        });
    }

    public async Task AnalyzeVoiceAsync(CallSession session, byte[] pcm16le)
    {
        var result = await AntiSpoof.AnalyzeVoiceAsync(session.Id, Guid.NewGuid(), pcm16le);
        var tracker = _voice.GetOrAdd(session.Id, _ => new VoiceSpoofTracker());
        tracker.Add(result.SpoofProbability, result.QualityScore);
        _logger.LogInformation("Caller voice: spoof {Spoof:F2} (sentence {Count}), call score {Score}",
            result.SpoofProbability, tracker.Count, tracker.CallScore?.ToString("F2") ?? "-");
        await _notifier.NotifyVoiceUpdatedAsync(session.Id, new VoiceUpdate(
            result.SpoofProbability, tracker.CallScore, tracker.Count, tracker.IsSuspicious, result.ModelVersion));
        if (!tracker.IsSuspicious || tracker.CallScore is not { } score)
        {
            return;
        }

        Evidence? added = null;
        RiskResult fused;
        lock (session)
        {
            var existing = session.EvidenceList.FirstOrDefault(e => e.Type == EvidenceType.VoiceSpoof);
            if (existing != null)
            {
                if (score <= existing.Confidence + 0.05f)
                {
                    return; // already reported at this level
                }
                existing.Confidence = score;
            }
            else
            {
                added = new Evidence
                {
                    CallSessionId = session.Id,
                    Type = EvidenceType.VoiceSpoof,
                    Confidence = score,
                    Source = result.ModelVersion,
                    ModelVersion = result.ModelVersion,
                    PayloadJson = $"{{\"sentences\":{tracker.Count}}}"
                };
                session.EvidenceList.Add(added);
            }
            var rules = _riskEngine.Calculate(session.EvidenceList.ToList(), session.CurrentStage);
            fused = RiskFusion.Fuse(rules, session.AnalystRisk, session.CurrentStage);
            session.CurrentRisk = fused.Score;
        }
        _logger.LogWarning("Caller voice suspected synthetic in call {Session} (score {Score:F2} over {Count} sentences)",
            session.Id, score, tracker.Count);
        if (added != null)
        {
            await _notifier.NotifyTacticDetectedAsync(session.Id, added);
        }
        await _notifier.NotifyRiskUpdatedAsync(session.Id, fused);
    }

    public void MarkFinalized(string? utteranceId)
    {
        if (!string.IsNullOrEmpty(utteranceId))
        {
            _finalizedUtterances[utteranceId] = 0;
        }
    }

    public bool IsFinalized(string? utteranceId) =>
        !string.IsNullOrEmpty(utteranceId) && _finalizedUtterances.ContainsKey(utteranceId);

    public Task<AsrResult> TranscribeAsync(CallSession session, byte[] audio, string audioFormat, CancellationToken ct) =>
        Asr.AnalyzeAudioAsync(session.Id, Guid.NewGuid(), audio, audioFormat, ct);

    /// <summary>Live words for the sentence being spoken; dropped if its final transcript already started.</summary>
    public async Task<bool> PushPartialAsync(CallSession session, string? utteranceId, string speaker, string text)
    {
        if (IsFinalized(utteranceId) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        await _notifier.NotifyTranscriptPartialAsync(session.Id, utteranceId ?? "", speaker, text);
        return true;
    }

    public Task ClearPartialAsync(CallSession session, string? utteranceId, string speaker) =>
        _notifier.NotifyTranscriptPartialAsync(session.Id, utteranceId ?? "", speaker, "");

    public async Task<TurnResult> AnalyzeSegmentAsync(CallSession session, TranscriptSegment segment)
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

        // Classify tactics - fast rules, caller turns only (tactics are what the caller does)
        var tactics = new List<TacticMatch>();
        if (segment.Speaker == "CALLER")
        {
            var nlpResult = await Nlp.AnalyzeTextAsync(session.Id, segment.Id, segment.Text);
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

        // Risk: rules, fused with the analyst's latest view of the call
        RiskResult riskResult;
        Alert? alert = null;
        lock (session)
        {
            var rules = _riskEngine.Calculate(session.EvidenceList.ToList(), session.CurrentStage);
            riskResult = RiskFusion.Fuse(rules, session.AnalystRisk, session.CurrentStage);
            session.CurrentRisk = riskResult.Score;

            // One rules-based Critical alert per call (not one per utterance once the score is high)
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
            WarnEmployee(session, null);
        }

        return new TurnResult(session.Id, segment.Text, segment.Speaker, segment.ModelVersion, segment.Confidence,
            tactics, riskResult.Stage.ToString(), riskResult.Score, riskResult.Severity.ToString());
    }

    private async Task RunAnalystAsync(CallSession session, TranscriptSegment segment)
    {
        try
        {
            var result = await Analyst.AnalyzeTurnAsync(session.Id, segment.Speaker, segment.Text);
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
                // still applied if the call ended while the LLM was thinking: a caller who asks for the
                // OTP and hangs up straight away must still be flagged on the dashboard
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
            if (result.Level == "ALERT")
            {
                // every ALERT turn: a new, more specific request (e.g. the OTP after a general alert) gets
                // its own warning; the warner plays each kind once and caps warnings per call
                WarnEmployee(session, result.HardSignals);
            }
        }
        catch (Exception ex)
        {
            // The rules already scored this turn; the analyst is an addition, never a dependency.
            _logger.LogWarning("AI call analyst failed for turn {Seq}: {Error}", segment.SequenceNo, ex.Message);
        }
    }
}
