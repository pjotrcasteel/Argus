using Argus.Agents.Visual;
using Argus.Core;
using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class ShadowEdgePerceptionTests
{
    private const int Width = 390;
    private const int Height = 844;

    private static readonly CombatVisualSettings Settings = new()
    {
        MinimumPlayerPixels = 70,
        MinimumThreatPixels = 75,
        PlayerPalette = new VisualColorRule
        {
            MinRed = 35, MinGreen = 54, MinBlue = 82, MinBlueMinusRed = 24, MinBlueMinusGreen = 14
        },
        ThreatPalettes = [new VisualColorRule
        {
            MinRed = 74, MinGreen = 30, MinBlue = 35, MinRedMinusGreen = 18, MinRedMinusBlue = 18
        }],
        MaxPlayerDistanceFromScreenCenter = 155
    };

    [TestMethod]
    public void ObserveShadowRgb_UpperLowerAndSideSprites_ReportsOnlyAdditionalCandidates()
    {
        var rgb = EmptyRgb();
        Paint(rgb, 180, 405, 20, 30, 72, 91, 115); // Player
        Paint(rgb, 120, 400, 20, 28, 120, 84, 66); // Old in-bounds enemy
        Paint(rgb, 300, 110, 20, 25, 120, 84, 66); // Upper gameplay band
        Paint(rgb, 372, 385, 18, 27, 120, 84, 66); // Edge-clipped sprite
        Paint(rgb, 120, 750, 20, 25, 120, 84, 66); // Lower gameplay band
        Paint(rgb, 120, 40, 20, 25, 120, 84, 66); // Top HUD: excluded
        Paint(rgb, 120, 805, 20, 25, 120, 84, 66); // Footer: excluded

        var original = CombatPixelPerception.ObserveRgb(rgb, Width, Height, Settings);
        var shadow = CombatPixelPerception.ObserveShadowRgb(rgb, Width, Height, Settings);

        Assert.AreEqual(1, original.ThreatCandidates.Count);
        Assert.AreEqual(original.ThreatCandidates.Single(), shadow.Baseline.ThreatCandidates.Single());
        Assert.HasCount(3, shadow.AddedCandidates);
        Assert.HasCount(4, shadow.CombinedCandidates);
        Assert.IsTrue(shadow.AddedCandidates.Any(p => p.Y < Height * 0.19f));
        Assert.IsTrue(shadow.AddedCandidates.Any(p => p.Y > Height * 0.82f));
        Assert.IsTrue(shadow.AddedCandidates.Any(p => p.X > Width * 0.96f));
        Assert.IsTrue(shadow.AddedCandidates.All(p => p.Confidence <= 0.50f));
    }

    [TestMethod]
    public void ObserveShadowRgb_NoVisiblePlayer_DoesNotInventThreatCandidates()
    {
        var rgb = EmptyRgb();
        Paint(rgb, 300, 110, 20, 25, 120, 84, 66);
        Paint(rgb, 120, 400, 20, 28, 120, 84, 66);

        var shadow = CombatPixelPerception.ObserveShadowRgb(rgb, Width, Height, Settings);

        Assert.IsNull(shadow.Baseline.Player);
        Assert.HasCount(0, shadow.AddedCandidates);
        Assert.HasCount(0, shadow.CombinedCandidates);
    }

    [TestMethod]
    public void ShadowThreatCounts_ZeroDenominators_DoNotFabricatePrecisionOrRecall()
    {
        var empty = new ShadowThreatCounts(0, 0, 0);
        Assert.IsNull(empty.Precision);
        Assert.IsNull(empty.Recall);

        var positive = new ShadowThreatCounts(3, 1, 2);
        Assert.AreEqual(0.75d, positive.Precision);
        Assert.AreEqual(0.6d, positive.Recall);
    }

    [TestMethod]
    public void RenderMarkdown_NoScoredFrames_DoesNotClaimValidAccuracy()
    {
        var report = new ShadowEdgeStudyReport("run-02", 1, 0, 1, 26,
            new ShadowThreatCounts(0, 0, 0), new ShadowThreatCounts(0, 0, 0),
            [new ShadowEdgeFrame(26, "NOT_COMBAT_FRAME", null, null, [])]);

        var text = ShadowEdgeStudy.RenderMarkdown(report);

        StringAssert.Contains(text, "N/A");
        StringAssert.Contains(text, "NOT_COMBAT_FRAME");
        StringAssert.Contains(text, "not held-out accuracy");
    }

    private static byte[] EmptyRgb()
    {
        var rgb = new byte[Width * Height * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 30;
            rgb[i + 1] = 39;
            rgb[i + 2] = 34;
        }
        return rgb;
    }

    private static void Paint(byte[] rgb, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
            {
                var index = (row * Width + column) * 3;
                rgb[index] = red;
                rgb[index + 1] = green;
                rgb[index + 2] = blue;
            }
    }
}
