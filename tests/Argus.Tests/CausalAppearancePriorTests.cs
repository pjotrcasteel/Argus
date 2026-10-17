using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class CausalAppearancePriorTests
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
    public void ObserveRgb_PlayerAtEdgeWithoutPrior_ReturnsUnknownNotSpuriousEnemy()
    {
        var rgb = Scene(370);
        var result = VisualAppearanceDescriptors.ObserveRgb(rgb, Width, Height, Settings);

        Assert.IsFalse(result.PlayerLocated);
        Assert.IsNull(result.Player);
        Assert.HasCount(0, result.Candidates);
    }

    [TestMethod]
    public void ObserveRgb_EdgePlayerWithPreviousVisiblePixelPosition_RestoresAppearanceMeasurement()
    {
        var rgb = Scene(370);
        var previous = new VisualBlob(340, 420, 20, 30, 200, 0.5f);
        var result = VisualAppearanceDescriptors.ObserveRgb(rgb, Width, Height, Settings, previous);

        Assert.IsTrue(result.PlayerLocated);
        Assert.IsNotNull(result.Player);
        Assert.IsTrue(result.Player.X > 370);
        Assert.HasCount(1, result.Candidates);
        Assert.AreEqual("baseline", result.Candidates[0].Source);
        Assert.IsTrue(result.Candidates[0].Features.ContrastToSurroundings > 0);
    }

    [TestMethod]
    public void ObserveRgb_DistantUnknownPrior_CannotBootstrapUnrelatedEdgeDecoration()
    {
        var rgb = Scene(370);
        var unrelated = new VisualBlob(190, 420, 20, 30, 200, 0.5f);
        var result = VisualAppearanceDescriptors.ObserveRgb(rgb, Width, Height, Settings, unrelated);

        Assert.IsFalse(result.PlayerLocated);
        Assert.HasCount(0, result.Candidates);
    }

    private static byte[] Scene(int playerLeft)
    {
        var rgb = new byte[Width * Height * 3];
        for (var offset = 0; offset < rgb.Length; offset += 3)
        {
            rgb[offset] = 30;
            rgb[offset + 1] = 39;
            rgb[offset + 2] = 34;
        }
        Paint(rgb, playerLeft, 405, 20, 30, 72, 91, 115);
        Paint(rgb, 100, 320, 20, 28, 120, 84, 66);
        return rgb;
    }

    private static void Paint(byte[] rgb, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
            {
                var offset = (row * Width + column) * 3;
                rgb[offset] = red;
                rgb[offset + 1] = green;
                rgb[offset + 2] = blue;
            }
    }
}
