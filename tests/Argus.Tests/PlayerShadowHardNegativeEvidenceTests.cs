using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PlayerShadowHardNegativeEvidenceTests
{
    [TestMethod]
    public void Summarize_AbsentRichSceneryAndBlueImposters_StayExplicitlyUnresolved()
    {
        var report = BuildReport();
        var result = PlayerShadowHardNegativeEvidence.Summarize(report);

        Assert.AreEqual("dev-red-blue", result.RunId);
        Assert.AreEqual(8, result.TotalBridgeProposals);
        Assert.AreEqual(6, result.ReviewedAbsentProposals);
        Assert.AreEqual(2, result.CorrectlyVetoedAbsent);
        Assert.AreEqual(2, result.UnresolvedAbsentTexturedWithoutPalette);
        Assert.AreEqual(2, result.UnresolvedAbsentWithPalette);
        Assert.AreEqual(0, result.UnresolvedAbsentOther);
        Assert.AreEqual(0, result.FalseVetoedVisible);
        Assert.AreEqual(2, result.UnresolvedVisible);
        Assert.AreEqual(0, result.UnknownOrUnreviewed);
        StringAssert.Contains(PlayerShadowHardNegativeEvidence.RenderMarkdown(result), "blue");
    }

    [TestMethod]
    public void RenderFeatureCsv_SeparatesFeaturesAndHumanTruthWithRunLevelGroupKey()
    {
        var csv = PlayerShadowHardNegativeEvidence.RenderFeatureCsv(BuildReport());
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.HasCount(9, lines);
        StringAssert.Contains(lines[0], "run_id,step,raw_bridge,player_palette_pixels");
        StringAssert.Contains(lines[0], "reviewed_candidate_local_label_v2");
        Assert.IsTrue(lines.Skip(1).All(line => line.StartsWith("\"dev-red-blue\",", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.EndsWith(",1", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.EndsWith(",0", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.Contains(",37.5,0.25,")));
    }

    [TestMethod]
    public void RenderFeatureCsv_UnknownOrUnreviewedNeverBecomesNegative()
    {
        var original = BuildReport();
        var data = original.Frames.ToArray();
        data[0] = data[0] with { HumanReference = "UNKNOWN" };
        data[1] = data[1] with { HumanReference = "UNREVIEWED" };
        var report = Recount("dev-unknown", data);

        var summary = PlayerShadowHardNegativeEvidence.Summarize(report);
        Assert.AreEqual(2, summary.UnknownOrUnreviewed);
        var lines = PlayerShadowHardNegativeEvidence.RenderFeatureCsv(report)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines[1].EndsWith(",", StringComparison.Ordinal));
        Assert.IsTrue(lines[2].EndsWith(",", StringComparison.Ordinal));
        Assert.AreEqual(6, lines.Count(line => line.EndsWith(",0", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RenderFeatureCsv_VisibleElsewhere_IsLocalNegativeEvenWhenGloballyVisible()
    {
        var original = BuildReport();
        var data = original.Frames.ToArray();
        data[1] = data[1] with { HumanDistancePixels = 78f };
        var report = Recount("dev-far", data);
        var summary = PlayerShadowHardNegativeEvidence.Summarize(report);
        Assert.AreEqual(1, summary.VisibleElsewhereCandidates);
        Assert.AreEqual(1, summary.UnresolvedFarVisible);
        Assert.AreEqual(1, summary.UnresolvedVisible);
        var lines = PlayerShadowHardNegativeEvidence.RenderFeatureCsv(report)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines[1].EndsWith(",1", StringComparison.Ordinal));
        Assert.IsTrue(lines[2].EndsWith(",0", StringComparison.Ordinal));
        StringAssert.Contains(PlayerShadowHardNegativeEvidence.RenderMarkdown(summary),
            "VISIBLE elsewhere (>26px from stale candidate): LOCAL NEGATIVE");
    }

    [TestMethod]
    public void RenderFeatureCsv_VisibleWithoutMeasuredDistance_RejectsUnknownIdentity()
    {
        var original = BuildReport();
        var data = original.Frames.ToArray();
        data[0] = data[0] with { HumanDistancePixels = null };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PlayerShadowHardNegativeEvidence.RenderFeatureCsv(Recount("dev-unsafe", data)));
    }

    [TestMethod]
    public void Summarize_CorruptCountersOrUntrustedPixelValues_Rejects()
    {
        var report = BuildReport();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PlayerShadowHardNegativeEvidence.Summarize(report with { VetoedOverHumanAbsent = 1 }));
        var broken = report.Frames.ToArray();
        broken[0] = broken[0] with { MaximumRgbChannelStdDev = float.NaN };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PlayerShadowHardNegativeEvidence.Summarize(Recount("dev", broken)));
        broken[0] = broken[0] with { HumanReference = "MAGIC_PLAYER" };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PlayerShadowHardNegativeEvidence.Summarize(Recount("dev", broken)));
    }

    private static PlayerCurrentFrameVetoReport BuildReport()
    {
        PlayerCurrentFrameVetoEvidence[] rows =
        [
            E(3, "VISIBLE", "INDETERMINATE", 63, 37.5f, 0.25f),
            E(4, "VISIBLE", "INDETERMINATE", 78, 51, 0.2f),
            E(7, "ABSENT", "EMPTY_PATCH_VETO", 0, 0, 0),
            E(8, "ABSENT", "EMPTY_PATCH_VETO", 0, 0, 0),
            E(12, "ABSENT", "INDETERMINATE", 0, 35, 0.7f),
            E(13, "ABSENT", "INDETERMINATE", 0, 35, 0.7f),
            E(17, "ABSENT", "INDETERMINATE", 1400, 38, 0.04f),
            E(18, "ABSENT", "INDETERMINATE", 1400, 38, 0.04f)
        ];
        return Recount("dev-red-blue", rows);
    }

    private static PlayerCurrentFrameVetoEvidence E(int step, string truth, string status,
        int matchingPixels, float variation, float edgeFraction) =>
        new(step, "exploring", "BRIDGE_ONE", status, truth, truth == "VISIBLE" ? 8f : null, matchingPixels, variation, edgeFraction);

    private static PlayerCurrentFrameVetoReport Recount(string id, IReadOnlyList<PlayerCurrentFrameVetoEvidence> frames) =>
        new(id, frames.Count, frames.Count(f => f.WitnessStatus == "EMPTY_PATCH_VETO"),
            frames.Count(f => f.WitnessStatus == "INDETERMINATE"),
            frames.Count(f => f.HumanReference == "ABSENT" && f.WitnessStatus == "EMPTY_PATCH_VETO"),
            frames.Count(f => f.HumanReference == "VISIBLE" && f.WitnessStatus == "EMPTY_PATCH_VETO"),
            frames.Count(f => f.HumanReference == "ABSENT" && f.WitnessStatus == "INDETERMINATE"),
            frames.Count(f => f.HumanReference == "VISIBLE" && f.WitnessStatus == "INDETERMINATE"),
            frames.Count(f => f.HumanReference is "UNKNOWN" or "UNREVIEWED"), frames);
}
