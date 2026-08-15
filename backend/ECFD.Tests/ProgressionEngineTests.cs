using System;
using System.Collections.Generic;
using Xunit;
using ECFD.Application.Progression;
using ECFD.Application.Risk;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Tests;

public class ProgressionEngineTests
{
    private readonly AttackProgressionEngine _engine = new();

    [Fact]
    public void Normal_Should_Transition_To_IdentityClaim_On_Impersonation()
    {
        var evidence = new Evidence
        {
            Type = EvidenceType.Impersonation,
            Confidence = 0.95f
        };

        var (newStage, transitioned, trigger) = _engine.ProcessEvidence(AttackStage.Normal, evidence);

        Assert.True(transitioned);
        Assert.Equal(AttackStage.IdentityClaim, newStage);
        Assert.Equal("IMPERSONATION_DETECTED", trigger);
    }

    [Fact]
    public void IdentityClaim_Should_Transition_To_Pressure_On_Urgency()
    {
        var evidence = new Evidence
        {
            Type = EvidenceType.Urgency,
            Confidence = 0.88f
        };

        var (newStage, transitioned, trigger) = _engine.ProcessEvidence(AttackStage.IdentityClaim, evidence);

        Assert.True(transitioned);
        Assert.Equal(AttackStage.Pressure, newStage);
    }

    [Fact]
    public void LowConfidence_Evidence_Should_Not_Trigger_Transition()
    {
        var evidence = new Evidence
        {
            Type = EvidenceType.OtpRequest,
            Confidence = 0.35f // Below 0.60 threshold
        };

        var (newStage, transitioned, _) = _engine.ProcessEvidence(AttackStage.Normal, evidence);

        Assert.False(transitioned);
        Assert.Equal(AttackStage.Normal, newStage);
    }
}

public class RiskEngineTests
{
    private readonly RiskEngine _riskEngine = new();

    [Fact]
    public void Benign_Call_Should_Produce_Low_Risk()
    {
        var evidenceList = new List<Evidence>(); // No suspicious tactics
        var result = _riskEngine.Calculate(evidenceList, AttackStage.Normal);

        Assert.Equal(0, result.Score);
        Assert.Equal(RiskSeverity.Low, result.Severity);
    }

    [Fact]
    public void Full_Attack_Progression_Should_Produce_Critical_Risk()
    {
        var evidenceList = new List<Evidence>
        {
            new Evidence { Type = EvidenceType.Impersonation, Confidence = 0.95f },
            new Evidence { Type = EvidenceType.Urgency, Confidence = 0.90f },
            new Evidence { Type = EvidenceType.OtpRequest, Confidence = 0.98f }
        };

        var result = _riskEngine.Calculate(evidenceList, AttackStage.CredentialExtraction);

        Assert.True(result.Score >= 80);
        Assert.Equal(RiskSeverity.Critical, result.Severity);
        Assert.NotEmpty(result.TopContributors);
    }
}
