using System;
using System.Collections.Generic;
using System.Linq;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Application.Risk;

public record RiskContributor(string Type, int Contribution);
public record RiskResult(int Score, RiskSeverity Severity, List<RiskContributor> TopContributors, AttackStage Stage);

/// <summary>
/// v0 explainable risk scorer (see ADR-0004).
///
/// Signals are split into three roles:
///  - Context  (IDENTITY_CLAIM): who the caller says they are. Legitimate callers say this too,
///    so it carries almost no risk alone; it changes how later requests are read.
///  - Pressure (URGENCY, AUTHORITY, SECRECY): manipulation cues, small alone.
///  - Requests (OTP, CREDENTIAL, PAYMENT, ...): what the caller wants the employee to do.
/// Most of the score comes from combinations, each reported as its own contributor so an
/// analyst sees *why* (e.g. "IDENTITY_CLAIM + OTP_REQUEST").
/// </summary>
public class RiskEngine : IRiskEngine
{
    // Standalone points (scaled by confidence).
    private static readonly Dictionary<EvidenceType, int> BasePoints = new()
    {
        [EvidenceType.IdentityClaim] = 3,
        [EvidenceType.Authority] = 6,
        [EvidenceType.Urgency] = 8,
        [EvidenceType.Secrecy] = 12,
        [EvidenceType.OtpRequest] = 30,
        [EvidenceType.CredentialRequest] = 30,
        [EvidenceType.PaymentRequest] = 25,
        [EvidenceType.VerificationBypass] = 20,
        [EvidenceType.RemoteAccess] = 15,
        [EvidenceType.SensitiveAction] = 10,
    };

    private static readonly HashSet<EvidenceType> PressureCues = new()
    {
        EvidenceType.Urgency, EvidenceType.Authority, EvidenceType.Secrecy
    };

    // Requests a genuine colleague, bank or IT department should never make by phone.
    private static readonly HashSet<EvidenceType> NeverLegitimateRequests = new()
    {
        EvidenceType.OtpRequest, EvidenceType.CredentialRequest,
        EvidenceType.PaymentRequest, EvidenceType.VerificationBypass
    };

    private static readonly HashSet<EvidenceType> SensitiveRequests = new(NeverLegitimateRequests)
    {
        EvidenceType.RemoteAccess, EvidenceType.SensitiveAction
    };

    private const int ClaimedIdentityThenForbiddenRequestBonus = 15;
    private const int PressureThenSensitiveRequestBonus = 10;

    public RiskResult Calculate(IReadOnlyCollection<Evidence> evidenceList, AttackStage currentStage)
    {
        var contributors = new List<RiskContributor>();

        // Each tactic counts once per call at its strongest confidence, so repeating the same
        // phrase (or a benign phrase that trips a rule) cannot pump the score up on its own.
        var strongest = evidenceList
            .Where(e => e.Type != EvidenceType.VoiceSpoof)
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Max(e => e.Confidence));

        // 1. Standalone signals
        float total = 0f;
        foreach (var (type, confidence) in strongest)
        {
            var points = (int)(BasePoints.GetValueOrDefault(type, 5) * confidence);
            total += points;
            contributors.Add(new RiskContributor(type.ToLabel(), points));
        }

        // 2. Combinations: the claimed identity and pressure only matter through what is asked for
        var forbiddenRequest = StrongestOf(strongest, NeverLegitimateRequests);
        if (strongest.TryGetValue(EvidenceType.IdentityClaim, out var claimConfidence) && forbiddenRequest is { } fr)
        {
            var points = (int)(ClaimedIdentityThenForbiddenRequestBonus * Math.Min(claimConfidence, fr.Confidence));
            total += points;
            contributors.Add(new RiskContributor($"IDENTITY_CLAIM + {fr.Type.ToLabel()}", points));
        }

        var pressure = StrongestOf(strongest, PressureCues);
        var sensitiveRequest = StrongestOf(strongest, SensitiveRequests);
        if (pressure is { } p && sensitiveRequest is { } sr)
        {
            var points = (int)(PressureThenSensitiveRequestBonus * Math.Min(p.Confidence, sr.Confidence));
            total += points;
            contributors.Add(new RiskContributor($"{p.Type.ToLabel()} + {sr.Type.ToLabel()}", points));
        }

        // 3. Progression: reaching a later stage matters; merely claiming an identity does not
        int progressionPoints = currentStage switch
        {
            AttackStage.CredentialExtraction => 25,
            AttackStage.SensitiveAction => 10,
            AttackStage.Pressure => 5,
            _ => 0
        };
        if (progressionPoints > 0)
        {
            total += progressionPoints;
            contributors.Add(new RiskContributor("ATTACK_PROGRESSION", progressionPoints));
        }

        // 4. Voice spoof risk
        var voiceEv = evidenceList
            .Where(e => e.Type == EvidenceType.VoiceSpoof)
            .MaxBy(e => e.Confidence);
        if (voiceEv != null && voiceEv.Confidence > 0.6f)
        {
            var voicePoints = (int)(voiceEv.Confidence * 30);
            total += voicePoints;
            contributors.Add(new RiskContributor("VOICE_AUTHENTICITY_SUSPICION", voicePoints));
        }

        int finalScore = Math.Min(100, (int)total);

        var severity = SeverityFor(finalScore);

        var topContributors = contributors
            .Where(c => c.Contribution > 0)
            .OrderByDescending(c => c.Contribution)
            .Take(6)
            .ToList();

        return new RiskResult(finalScore, severity, topContributors, currentStage);
    }

    private static (EvidenceType Type, float Confidence)? StrongestOf(
        Dictionary<EvidenceType, float> strongest, HashSet<EvidenceType> types)
    {
        var matches = strongest.Where(kv => types.Contains(kv.Key)).ToList();
        if (matches.Count == 0)
        {
            return null;
        }
        var best = matches.MaxBy(kv => kv.Value);
        return (best.Key, best.Value);
    }

    public static RiskSeverity SeverityFor(int score) => score switch
    {
        >= 80 => RiskSeverity.Critical,
        >= 60 => RiskSeverity.High,
        >= 30 => RiskSeverity.Medium,
        _ => RiskSeverity.Low
    };
}
