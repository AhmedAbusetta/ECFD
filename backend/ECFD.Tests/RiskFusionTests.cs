using System.Collections.Generic;
using Xunit;
using ECFD.Application.Risk;
using ECFD.Domain.Enums;

namespace ECFD.Tests;

public class RiskFusionTests
{
    private static RiskResult Rules(int score) =>
        new(score, RiskEngine.SeverityFor(score), new List<RiskContributor> { new("OTP_REQUEST", score) }, AttackStage.Pressure);

    [Fact]
    public void Analyst_Raises_Risk_The_Rules_Missed()
    {
        var fused = RiskFusion.Fuse(Rules(20), 85, AttackStage.SensitiveAction);

        Assert.Equal(85, fused.Score);
        Assert.Equal(RiskSeverity.Critical, fused.Severity);
        Assert.Equal(RiskFusion.AnalystContributor, fused.TopContributors[0].Type);
        Assert.Equal(AttackStage.SensitiveAction, fused.Stage);
    }

    [Fact]
    public void Lower_Analyst_Risk_Never_Lowers_The_Rules()
    {
        var fused = RiskFusion.Fuse(Rules(90), 10, AttackStage.CredentialExtraction);

        Assert.Equal(90, fused.Score);
        Assert.DoesNotContain(fused.TopContributors, c => c.Type == RiskFusion.AnalystContributor);
    }

    [Theory]
    [InlineData(AttackStage.IdentityClaim, "CredentialExtraction", true, AttackStage.CredentialExtraction)]
    [InlineData(AttackStage.CredentialExtraction, "Pressure", false, AttackStage.CredentialExtraction)] // never backwards
    [InlineData(AttackStage.Normal, "Hacking", false, AttackStage.Normal)]                              // unknown name
    [InlineData(AttackStage.Normal, "42", false, AttackStage.Normal)]                                   // numbers are not names
    public void Stage_Only_Moves_Forward_On_Known_Names(AttackStage current, string analystStage, bool advanced, AttackStage expected)
    {
        Assert.Equal(advanced, RiskFusion.TryAdvanceStage(current, analystStage, out var next));
        Assert.Equal(expected, next);
    }
}
