using Argus.Agents.Visual;
using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class AppearanceReadinessTests
{
    [TestMethod]
    public void Assess_TwoCorrelatedDevelopmentRuns_BlocksTrainingDespiteHighCandidatePrecision()
    {
        AppearanceAuditReport[] samples = [Report("dev-a", 30, 16, 6), Report("dev-b", 43, 2, 14)];
        var result = AppearanceReadiness.Assess(samples);

        Assert.AreEqual("NOT_READY", result.Status);
        Assert.AreEqual(2, result.DevelopmentRuns);
        Assert.AreEqual(73, result.MatchedCandidateCenters);
        Assert.AreEqual(18, result.UnmatchedCandidateCenters);
        Assert.IsTrue(result.Blockers.Any(b => b.Contains("Only 2 distinct development runs")));
        Assert.IsTrue(result.Blockers.Any(b => b.Contains("dev-b: Only 2 unmatched")));
        Assert.IsFalse(result.Runs.Single(r => r.RunId == "dev-b").EnoughReferences);
        StringAssert.Contains(AppearanceReadiness.RenderMarkdown(result), "NOT_READY");
        StringAssert.Contains(AppearanceReadiness.RenderMarkdown(result), "not confirmed");
    }

    [TestMethod]
    public void Assess_ThreeBalancedIndependentDevelopmentRuns_PermitsOnlyOfflineExploration()
    {
        AppearanceAuditReport[] samples = [Report("dev-c", 14, 11, 8), Report("dev-a", 20, 17, 6),
            Report("dev-b", 30, 10, 10)];
        var result = AppearanceReadiness.Assess(samples);

        Assert.AreEqual("SUFFICIENT_FOR_OFFLINE_EXPLORATION", result.Status);
        Assert.HasCount(3, result.Runs);
        Assert.HasCount(0, result.Blockers);
        CollectionAssert.AreEqual(new[] { "dev-a", "dev-b", "dev-c" }, result.Runs.Select(r => r.RunId).ToArray());
        StringAssert.Contains(AppearanceReadiness.RenderMarkdown(result), "never live steering");
    }

    [TestMethod]
    public void Assess_LessThanFiveScoredScreenshots_RemainsBlockedRegardlessOfCandidateCounts()
    {
        var result = AppearanceReadiness.Assess([Report("dev-a", 50, 30, 4), Report("dev-b", 50, 30, 3),
            Report("dev-c", 50, 30, 4)]);

        Assert.AreEqual("NOT_READY", result.Status);
        Assert.IsTrue(result.Blockers.Any(b => b.Contains("Only 4 scored frames")));
    }

    [TestMethod]
    public void Assess_UnreviewedOrMismatchedCandidateTotals_RejectsInsteadOfInventingNegatives()
    {
        var ok = Report("dev-a", 30, 16, 4);
        var incorrect = ok with { Candidates = ok.Candidates.Skip(1).ToArray() };
        Assert.ThrowsExactly<InvalidDataException>(() => AppearanceReadiness.Assess([incorrect]));
        Assert.ThrowsExactly<InvalidDataException>(() => AppearanceReadiness.Assess([ok, ok]));
        Assert.ThrowsExactly<InvalidDataException>(() => AppearanceReadiness.Assess([]));
    }

    private static AppearanceAuditReport Report(string id, int matched, int unmatched, int missed)
    {
        var features = new VisualAppearanceDescriptor(30, 200, 18, 22, false, 0.3f, 0.1f, 0.2f, 0.2f, 0.3f);
        AppearanceAuditCandidate[] candidates =
        [
            .. Enumerable.Range(0, matched).Select(_ => new AppearanceAuditCandidate(0, "baseline", true, features)),
            .. Enumerable.Range(0, unmatched).Select(_ => new AppearanceAuditCandidate(0, "shadow", false, features))
        ];
        AppearanceAuditFrame[] frames =
        [
            new AppearanceAuditFrame(0, "SCORED", matched, unmatched, missed),
            .. Enumerable.Range(1, 5).Select(i => new AppearanceAuditFrame(i, "SCORED", 0, 0, 0))
        ];

        return new AppearanceAuditReport(id, frames.Length, frames.Length, missed, frames, candidates, []);
    }
}
