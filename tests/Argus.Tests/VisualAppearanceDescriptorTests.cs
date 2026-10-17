using Argus.Agents.Visual;
using Argus.Core;
using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class VisualAppearanceDescriptorTests
{
    private const int Width = 120;
    private const int Height = 160;

    [TestMethod]
    public void Measure_UniformPatchOnDifferentBackground_ReportsContrastWithoutTexture()
    {
        var image = Solid(20, 20, 20);
        Paint(image, 40, 55, 20, 24, 220, 100, 70);

        var result = VisualAppearanceDescriptors.Measure(image, Width, Height, new VisualBlob(49.5f, 66.5f, 20, 24, 150, 0.5f));

        Assert.IsTrue(result.ContrastToSurroundings > 0.25f);
        Assert.AreEqual(0f, result.GradientDensity);
        Assert.AreEqual(0f, result.LuminanceStdDev);
        Assert.IsTrue(result.MeanSaturation > 0.5f);
        Assert.IsFalse(result.ClippedAtViewportEdge);
    }

    [TestMethod]
    public void Measure_CheckerboardWithSameMean_IsTexturedAndHigherVariation()
    {
        var flat = Solid(100, 100, 100);
        var texture = Solid(100, 100, 100);
        for (var y = 55; y < 79; y++)
            for (var x = 40; x < 60; x++)
            {
                var v = (x + y) % 2 == 0 ? (byte)30 : (byte)170;
                Set(texture, x, y, v, v, v);
            }
        Paint(flat, 40, 55, 20, 24, 100, 100, 100);
        var blob = new VisualBlob(49.5f, 66.5f, 20, 24, 150, 0.5f);

        var original = VisualAppearanceDescriptors.Measure(flat, Width, Height, blob);
        var detailed = VisualAppearanceDescriptors.Measure(texture, Width, Height, blob);

        Assert.IsTrue(detailed.GradientDensity > original.GradientDensity);
        Assert.IsTrue(detailed.LuminanceStdDev > original.LuminanceStdDev);
        Assert.IsTrue(detailed.MeanLuminance is >= 0 and <= 1);
        Assert.IsTrue(detailed.GradientDensity is >= 0 and <= 1);
    }

    [TestMethod]
    public void Measure_EdgeClippedCandidate_FlagsViewportClippingAndFiniteFeatures()
    {
        var image = Solid(40, 40, 40);
        Paint(image, 0, 40, 6, 22, 220, 140, 80);

        var result = VisualAppearanceDescriptors.Measure(image, Width, Height, new VisualBlob(2f, 50f, 16, 22, 110, 0.5f));

        Assert.IsTrue(result.ClippedAtViewportEdge);
        Assert.IsTrue(float.IsFinite(result.ContrastToSurroundings));
        Assert.IsTrue(float.IsFinite(result.MeanLuminance));
        Assert.IsTrue(float.IsFinite(result.MeanSaturation));
    }

    [TestMethod]
    public void Measure_InvalidGeometry_RejectsInsteadOfManufacturingFeatures()
    {
        var image = Solid(30, 30, 30);

        Assert.ThrowsExactly<ArgumentException>(() => VisualAppearanceDescriptors.Measure([], Width, Height,
            new VisualBlob(30, 40, 15, 15, 100, 0.5f)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => VisualAppearanceDescriptors.Measure(image, Width, Height,
            new VisualBlob(float.NaN, 40, 15, 15, 100, 0.5f)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => VisualAppearanceDescriptors.Measure(image, Width, Height,
            new VisualBlob(800, 40, 15, 15, 100, 0.5f)));
    }

    [TestMethod]
    public void Match_CloseDuplicatePredictions_AttributesOnlyOneToHumanTruth()
    {
        VisualCandidatePoint[] expected = [new(55, 90)];
        VisualCandidatePoint[] predicted = [new(55, 90), new(56, 90)];

        var match = ThreatMatching.Match(expected, predicted, 26);

        Assert.AreEqual(1, match.TruePositives);
        Assert.HasCount(1, match.MatchedPredictionIndices);
        Assert.AreEqual(0, match.MatchedPredictionIndices[0]);
        Assert.HasCount(1, match.FalsePositives);
    }

    [TestMethod]
    public void RenderMarkdown_NoScoredFrames_DoesNotClaimClassifierAccuracy()
    {
        var report = new AppearanceAuditReport("run-02", 1, 0, 0,
            [new AppearanceAuditFrame(26, "NOT_COMBAT_FRAME", 0, 0, 0)], [], []);

        var markdown = AppearanceAudit.RenderMarkdown(report);

        StringAssert.Contains(markdown, "scored: 0");
        StringAssert.Contains(markdown, "No model was trained");
        StringAssert.Contains(markdown, "NOT_COMBAT_FRAME");
    }

    private static byte[] Solid(byte r, byte g, byte b)
    {
        var data = new byte[Width * Height * 3];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                Set(data, x, y, r, g, b);
        return data;
    }

    private static void Paint(byte[] rgb, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        for (var iy = y; iy < y + h; iy++)
            for (var ix = x; ix < x + w; ix++)
                Set(rgb, ix, iy, r, g, b);
    }

    private static void Set(byte[] rgb, int x, int y, byte r, byte g, byte b)
    {
        var i = (y * Width + x) * 3;
        rgb[i] = r;
        rgb[i + 1] = g;
        rgb[i + 2] = b;
    }
}
