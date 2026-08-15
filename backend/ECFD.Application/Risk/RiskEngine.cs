using System;
using System.Collections.Generic;
using System.Linq;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Application.Risk;

public record RiskContributor(string Type, int Contribution);
public record RiskResult(int Score, RiskSeverity Severity, List<RiskContributor> TopContributors, AttackStage Stage);

public class RiskEngine : IRiskEngine
{
    private const float WeightContent = 0.45f;
    private const float WeightProgression = 0.30f;
    private const float WeightVoice = 0.15f;
    private const float WeightContext = 0.10f;

    public RiskResult Calculate(IReadOnlyCollection<Evidence> evidenceList, AttackStage currentStage)
    {
        var contributors = new List<RiskContributor>();

        // 1. Content Risk (From NLP Tactics)
        float rawContentScore = 0f;
        foreach (var ev in evidenceList.Where(e => e.Type != EvidenceType.VoiceSpoof))
        {
            int point = ev.Type switch
            {
                EvidenceType.OtpRequest => 35,
                EvidenceType.CredentialRequest => 30,
                EvidenceType.PaymentRequest => 25,
                EvidenceType.RemoteAccess => 25,
                EvidenceType.Impersonation => 20,
                EvidenceType.Authority => 15,
                EvidenceType.Urgency => 15,
                EvidenceType.Secrecy => 15,
                EvidenceType.VerificationBypass => 20,
                EvidenceType.SensitiveAction => 10,
                _ => 5
            };

            int weightedPoint = (int)(point * ev.Confidence);
            rawContentScore += weightedPoint;
            contributors.Add(new RiskContributor(ev.Type.ToString(), weightedPoint));
        }

        // 2. Progression Risk (Stage Multiplier)
        int progressionPoints = currentStage switch
        {
            AttackStage.CredentialExtraction => 35,
            AttackStage.SensitiveAction => 25,
            AttackStage.Pressure => 15,
            AttackStage.IdentityClaim => 10,
            _ => 0
        };
        if (progressionPoints > 0)
        {
            contributors.Add(new RiskContributor("ATTACK_PROGRESSION", progressionPoints));
        }

        // 3. Voice Spoof Risk
        var voiceEv = evidenceList.FirstOrDefault(e => e.Type == EvidenceType.VoiceSpoof);
        int voicePoints = 0;
        if (voiceEv != null && voiceEv.Confidence > 0.6f)
        {
            voicePoints = (int)(voiceEv.Confidence * 30);
            contributors.Add(new RiskContributor("VOICE_AUTHENTICITY_SUSPICION", voicePoints));
        }

        // Total Calibrated Score (0 - 100)
        int finalScore = Math.Min(100, (int)(rawContentScore + progressionPoints + voicePoints));

        var severity = finalScore switch
        {
            >= 80 => RiskSeverity.Critical,
            >= 60 => RiskSeverity.High,
            >= 30 => RiskSeverity.Medium,
            _ => RiskSeverity.Low
        };

        var topContributors = contributors
            .OrderByDescending(c => c.Contribution)
            .Take(5)
            .ToList();

        return new RiskResult(finalScore, severity, topContributors, currentStage);
    }
}
