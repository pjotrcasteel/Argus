using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PlayerColorEvidenceTests
{
    private const int Width = 390;
    private const int Height = 844;

    private static readonly CombatVisualSettings Settings = new()
    {
        MinimumPlayerPixels = 70,
        MinimumThreatPixels = 75,
        MaxPlayerDistanceFromScreenCenter = 155,
        PlayerPalette = new VisualColorRule
        {
            MinRed = 35, MinGreen = 54, MinBlue = 82, MinBlueMinusRed = 24, MinBlueMinusGreen = 14
        },
        ThreatPalettes =
        [
            new VisualColorRule
            {
                MinRed = 74, MinGreen = 30, MinBlue = 35,
                MinRedMinusGreen = 18, MinRedMinusBlue = 18
            }
        ]
    };

    [TestMethod]
    public void InspectPlayerColorComponentsRgb_NormalVisibleActor_ReportsEligibleWithoutChangingDetection()
    {
        var rgb = Scene(20, 30, 184, 405);
        var normal = CombatPixelPerception.ObserveRgb(rgb, Width, Height, Settings);
        var audit = CombatPixelPerception.InspectPlayerColorComponentsRgb(rgb, Width, Height, Settings,
            new VisualCandidatePoint(193, 420));

        Assert.IsNotNull(normal.Player);
        Assert.HasCount(1, audit);
        Assert.AreEqual("ELIGIBLE_GEOMETRY", audit[0].RejectionReason);
        Assert.AreEqual(600, audit[0].MatchingPixels);
        Assert.AreEqual(20, audit[0].Width);
        Assert.AreEqual(30, audit[0].Height);
    }

    [TestMethod]
    public void InspectPlayerColorComponentsRgb_MergedSpellColoredPatch_ExplainsTooWideRejection()
    {
        var rgb = Scene(60, 36, 165, 400);
        var normal = CombatPixelPerception.ObserveRgb(rgb, Width, Height, Settings);
        var audit = CombatPixelPerception.InspectPlayerColorComponentsRgb(rgb, Width, Height, Settings,
            new VisualCandidatePoint(195, 420));

        Assert.IsNull(normal.Player);
        Assert.HasCount(1, audit);
        Assert.AreEqual("TOO_WIDE", audit[0].RejectionReason);
        Assert.AreEqual(2160, audit[0].MatchingPixels);
        Assert.AreEqual(60, audit[0].Width);
    }

    [TestMethod]
    public void InspectPlayerColorComponentsRgb_OccludedTinyPatch_ExplainsMinimumPixelRejection()
    {
        var rgb = Scene(9, 7, 190, 416);
        var normal = CombatPixelPerception.ObserveRgb(rgb, Width, Height, Settings);
        var audit = CombatPixelPerception.InspectPlayerColorComponentsRgb(rgb, Width, Height, Settings,
            new VisualCandidatePoint(194, 420));

        Assert.IsNull(normal.Player);
        Assert.HasCount(1, audit);
        Assert.AreEqual("BELOW_MINIMUM_PIXELS", audit[0].RejectionReason);
        Assert.AreEqual(63, audit[0].MatchingPixels);
    }

    [TestMethod]
    public void InspectPlayerColorComponentsRgb_EmptyBackground_ReturnsNoNearbyPalette()
    {
        var rgb = Scene(0, 0, 0, 0);
        var audit = CombatPixelPerception.InspectPlayerColorComponentsRgb(rgb, Width, Height, Settings,
            new VisualCandidatePoint(195, 420));
        Assert.HasCount(0, audit);
    }

    private static byte[] Scene(int playerWidth, int playerHeight, int left, int top)
    {
        var rgb = new byte[Width * Height * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 30;
            rgb[i + 1] = 39;
            rgb[i + 2] = 34;
        }
        for (var y = top; y < top + playerHeight; y++)
            for (var x = left; x < left + playerWidth; x++)
            {
                var index = (y * Width + x) * 3;
                rgb[index] = 72;
                rgb[index + 1] = 91;
                rgb[index + 2] = 115;
            }
        return rgb;
    }
}
