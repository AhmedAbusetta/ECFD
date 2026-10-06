using System;
using System.Collections.Generic;
using System.Linq;
using ECFD.Domain.Enums;

namespace ECFD.Application.Risk;

/// <summary>
/// Fuses the fast rule-based score with the AI call analyst's score (ADR-0005):
/// the call's risk is the higher of the two, so the analyst can raise risk the rules cannot see,
/// and a slow or missing analyst answer never lowers what the rules already found.
/// </summary>
public static class RiskFusion
{
    public const string AnalystContributor = "AI_CALL_ANALYST";

    public static RiskResult Fuse(RiskResult rules, int analystRisk, AttackStage stage)
    {
        if (analystRisk <= rules.Score)
        {
            return rules with { Stage = stage };
        }
        var contributors = new List<RiskContributor> { new(AnalystContributor, analystRisk) };
        contributors.AddRange(rules.TopContributors.Where(c => c.Type != AnalystContributor));
        return new RiskResult(analystRisk, RiskEngine.SeverityFor(analystRisk), contributors, stage);
    }

    /// <summary>The analyst reports stages by name; only forward moves are applied.</summary>
    public static bool TryAdvanceStage(AttackStage current, string analystStage, out AttackStage next)
    {
        next = current;
        if (Enum.TryParse<AttackStage>(analystStage, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed) && parsed > current)
        {
            next = parsed;
            return true;
        }
        return false;
    }
}
