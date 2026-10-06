using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ECFD.Application.Progression;
using ECFD.Application.Risk;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Tests;

public class ProgressionEngineTests
{
    private readonly AttackProgressionEngine _engine = new();

    private static Evidence Ev(EvidenceType type, float confidence = 0.92f) => new() { Type = type, Confidence = confidence };

    [Fact]
    public void Normal_Should_Move_To_IdentityClaim_On_Identity_Claim()
    {
        var (newStage, transitioned, trigger) = _engine.ProcessEvidence(AttackStage.Normal, Ev(EvidenceType.IdentityClaim));

        Assert.True(transitioned);
        Assert.Equal(AttackStage.IdentityClaim, newStage);
        Assert.Equal("IDENTITY_CLAIMED", trigger);
    }

    [Fact]
    public void IdentityClaim_Should_Transition_To_Pressure_On_Urgency()
    {
        var (newStage, transitioned, _) = _engine.ProcessEvidence(AttackStage.IdentityClaim, Ev(EvidenceType.Urgency));

        Assert.True(transitioned);
        Assert.Equal(AttackStage.Pressure, newStage);
    }

    [Fact]
    public void LowConfidence_Evidence_Should_Not_Trigger_Transition()
    {
        var (newStage, transitioned, _) = _engine.ProcessEvidence(AttackStage.Normal, Ev(EvidenceType.OtpRequest, 0.35f));

        Assert.False(transitioned);
        Assert.Equal(AttackStage.Normal, newStage);
    }

    [Theory]
    [InlineData(EvidenceType.OtpRequest)]
    [InlineData(EvidenceType.CredentialRequest)]
    [InlineData(EvidenceType.PaymentRequest)]
    public void Direct_Extraction_Request_Without_Introduction_Still_Escalates(EvidenceType request)
    {
        var (newStage, transitioned, trigger) = _engine.ProcessEvidence(AttackStage.Normal, Ev(request));

        Assert.True(transitioned);
        Assert.Equal(AttackStage.CredentialExtraction, newStage);
        Assert.Equal("DIRECT_EXTRACTION_ATTEMPT", trigger);
    }

    [Fact]
    public void Payment_Request_After_Sensitive_Action_Reaches_Extraction()
    {
        var (newStage, _, _) = _engine.ProcessEvidence(AttackStage.SensitiveAction, Ev(EvidenceType.PaymentRequest));

        Assert.Equal(AttackStage.CredentialExtraction, newStage);
    }
}

public class RiskEngineTests
{
    private readonly RiskEngine _riskEngine = new();
    private readonly AttackProgressionEngine _progression = new();

    private static Evidence Ev(EvidenceType type, float confidence = 0.92f) => new() { Type = type, Confidence = confidence };

    /// <summary>Feeds evidence through the stage engine and the scorer like the live pipeline.</summary>
    private RiskResult Run(params EvidenceType[] tactics)
    {
        var stage = AttackStage.Normal;
        var evidence = new List<Evidence>();
        foreach (var t in tactics)
        {
            var ev = Ev(t);
            evidence.Add(ev);
            stage = _progression.ProcessEvidence(stage, ev).NewStage;
        }
        return _riskEngine.Calculate(evidence, stage);
    }

    [Fact]
    public void Benign_Call_Should_Produce_Low_Risk()
    {
        var result = _riskEngine.Calculate(new List<Evidence>(), AttackStage.Normal);

        Assert.Equal(0, result.Score);
        Assert.Equal(RiskSeverity.Low, result.Severity);
    }

    [Fact]
    public void Genuine_IT_Call_Identity_Claim_Alone_Stays_Low()
    {
        // "أنا من الدعم الفني، هنعمل تحديث للسيستم بالليل" - real IT staff say this every day.
        var result = Run(EvidenceType.IdentityClaim);

        Assert.Equal(RiskSeverity.Low, result.Severity);
        Assert.True(result.Score < 10, $"identity claim alone scored {result.Score}");
    }

    [Fact]
    public void Genuine_IT_Remote_Support_Without_Pressure_Stays_Low()
    {
        // Real IT does ask to connect remotely; with no pressure and no credential request it is not alarming.
        var result = Run(EvidenceType.IdentityClaim, EvidenceType.RemoteAccess);

        Assert.Equal(RiskSeverity.Low, result.Severity);
    }

    [Fact]
    public void Identity_Claim_Then_Otp_Request_Is_Far_Riskier_Than_Otp_Request_Alone()
    {
        var otpOnly = Run(EvidenceType.OtpRequest);
        var claimThenOtp = Run(EvidenceType.IdentityClaim, EvidenceType.OtpRequest);

        Assert.True(claimThenOtp.Score - otpOnly.Score >= 12,
            $"claim+OTP {claimThenOtp.Score} vs OTP alone {otpOnly.Score}");
        Assert.Contains(claimThenOtp.TopContributors, c => c.Type == "IDENTITY_CLAIM + OTP_REQUEST");
    }

    [Fact]
    public void Direct_Otp_Request_Is_At_Least_Medium()
    {
        var result = Run(EvidenceType.OtpRequest);

        Assert.True(result.Severity >= RiskSeverity.Medium, $"got {result.Score}");
    }

    [Fact]
    public void Full_Attack_Progression_Should_Produce_Critical_Risk()
    {
        var result = Run(EvidenceType.IdentityClaim, EvidenceType.Urgency, EvidenceType.Authority, EvidenceType.OtpRequest);

        Assert.True(result.Score >= 80, $"got {result.Score}");
        Assert.Equal(RiskSeverity.Critical, result.Severity);
        Assert.Equal(AttackStage.CredentialExtraction, result.Stage);
    }

    [Fact]
    public void Remote_Access_Under_Pressure_And_Secrecy_Is_Flagged()
    {
        var calm = Run(EvidenceType.IdentityClaim, EvidenceType.RemoteAccess);
        var pressured = Run(EvidenceType.IdentityClaim, EvidenceType.Urgency, EvidenceType.Secrecy, EvidenceType.RemoteAccess);

        Assert.True(pressured.Severity >= RiskSeverity.Medium, $"got {pressured.Score}");
        Assert.True(pressured.Score > calm.Score + 20);
    }

    [Fact]
    public void Repeating_The_Same_Tactic_Does_Not_Inflate_Risk()
    {
        var once = new List<Evidence> { Ev(EvidenceType.IdentityClaim, 0.95f) };
        var fiveTimes = Enumerable.Range(0, 5).Select(_ => Ev(EvidenceType.IdentityClaim, 0.95f)).ToList();

        var single = _riskEngine.Calculate(once, AttackStage.IdentityClaim);
        var repeated = _riskEngine.Calculate(fiveTimes, AttackStage.IdentityClaim);

        Assert.Equal(single.Score, repeated.Score);
        Assert.Equal(RiskSeverity.Low, repeated.Severity);
        Assert.Single(repeated.TopContributors, c => c.Type == "IDENTITY_CLAIM");
    }

    [Fact]
    public void Repeated_Tactic_Uses_Its_Strongest_Confidence()
    {
        var evidenceList = new List<Evidence>
        {
            Ev(EvidenceType.OtpRequest, 0.40f),
            Ev(EvidenceType.OtpRequest, 1.00f)
        };

        var result = _riskEngine.Calculate(evidenceList, AttackStage.Normal);

        Assert.Equal(30, result.Score);
        Assert.Equal("OTP_REQUEST", result.TopContributors[0].Type);
    }
}
