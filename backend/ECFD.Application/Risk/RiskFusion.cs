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
    public const string ForbiddenRequestContributor = "FORBIDDEN_REQUEST";
    /// <summary>
    /// Once the caller has asked for something no legitimate caller asks for by phone, the call's risk never
    /// falls below this again (the same floor the analyst service applies, ml/brain/ecfd_brain/floor.py).
    /// </summary>
    public const int ForbiddenRequestFloor = 80;

    /// <param name="requestFloor">0, or <see cref="ForbiddenRequestFloor"/> once the instant rules heard a forbidden request.</param>
    public static RiskResult Fuse(RiskResult rules, int analystRisk, AttackStage stage, int requestFloor = 0)
    {
        if (requestFloor > rules.Score && requestFloor >= analystRisk)
        {
            var withFloor = new List<RiskContributor> { new(ForbiddenRequestContributor, requestFloor) };
            withFloor.AddRange(rules.TopContributors);
            return new RiskResult(requestFloor, RiskEngine.SeverityFor(requestFloor), withFloor, stage);
        }
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
