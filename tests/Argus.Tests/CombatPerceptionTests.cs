using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class CombatPerceptionTests
{
    private static readonly CombatVisualSettings Settings = new()
    {
        MinimumPlayerPixels = 70,
        MinimumThreatPixels = 75,
        ThreatAvoidanceRadius = 115,
        PlayerPalette = new VisualColorRule { MinRed = 35, MinGreen = 54, MinBlue = 82, MinBlueMinusRed = 24, MinBlueMinusGreen = 14 },
        ThreatPalettes =
        [
            new VisualColorRule { MinRed = 55, MinGreen = 35, MinBlue = 78, MinBlueMinusRed = 10, MinBlueMinusGreen = 19, MinRedMinusGreen = 6 },
            new VisualColorRule { MinRed = 74, MinGreen = 30, MinBlue = 35, MinRedMinusGreen = 18, MinRedMinusBlue = 18 }
        ]
    };

    [TestMethod]
    public void ObserveRgb_VisibleMageAndOpponents_LocatesScreenSpaceCandidates()
    {
        var rgb = MakeEmptyFrame();
        Paint(rgb, 179, 400, 20, 30, 72, 91, 115);
        Paint(rgb, 104, 406, 19, 28, 120, 84, 66);
        Paint(rgb, 260, 410, 24, 31, 148, 119, 168);

        var detected = CombatPixelPerception.ObserveRgb(rgb, 390, 844, Settings);

        Assert.IsNotNull(detected.Player);
        Assert.IsTrue(Math.Abs(detected.Player.X - 188.5f) <= 2);
        Assert.IsTrue(Math.Abs(detected.Player.Y - 414.5f) <= 2);
        Assert.AreEqual(2, detected.ThreatCandidates.Count);
        Assert.IsTrue(detected.NearestThreatDistance < 100);
        Assert.IsTrue(detected.ThreatCandidates.All(t => t.Confidence <= 0.65f));
    }

    [TestMethod]
    public void ObserveRgb_NoRecognizableMage_ReturnsUnknownInsteadOfInventedPosition()
    {
        var rgb = MakeEmptyFrame();
        Paint(rgb, 104, 406, 19, 28, 120, 84, 66);

        var detected = CombatPixelPerception.ObserveRgb(rgb, 390, 844, Settings);

        Assert.IsNull(detected.Player);
        Assert.HasCount(0, detected.ThreatCandidates);
        Assert.IsNull(VisualCombatSteering.SelectEvasionDirection(detected, 115));
    }

    [TestMethod]
    public void SelectEvasionDirection_CloseCandidateToTheLeft_SteersRight()
    {
        var player = new VisualBlob(195, 420, 18, 32, 200, 0.85f);
        var danger = new VisualBlob(155, 422, 18, 28, 180, 0.60f);
        var observed = new CombatVisualObservation(player, [danger]);

        var direction = VisualCombatSteering.SelectEvasionDirection(observed, 115);

        Assert.AreEqual(3, direction);
    }

    [TestMethod]
    public void SelectEvasionDirection_DistantCandidates_UsesConservativeFallback()
    {
        var player = new VisualBlob(195, 420, 18, 32, 200, 0.85f);
        var danger = new VisualBlob(19, 150, 18, 28, 180, 0.60f);
        var observed = new CombatVisualObservation(player, [danger]);

        Assert.IsNull(VisualCombatSteering.SelectEvasionDirection(observed, 115));
    }

    [TestMethod]
    public void ObserveRgb_MageOnlyInTopHud_IsNotMistakenForPlayer()
    {
        var rgb = MakeEmptyFrame();
        Paint(rgb, 130, 35, 20, 30, 72, 91, 115);

        Assert.IsNull(CombatPixelPerception.ObserveRgb(rgb, 390, 844, Settings).Player);
    }

    private static byte[] MakeEmptyFrame()
    {
        var pixels = new byte[390 * 844 * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = 30;
            pixels[i + 1] = 39;
            pixels[i + 2] = 34;
        }
        return pixels;
    }

    private static void Paint(byte[] rgb, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var col = x; col < x + width; col++)
            {
                var index = (row * 390 + col) * 3;
                rgb[index] = red;
                rgb[index + 1] = green;
                rgb[index + 2] = blue;
            }
        }
    }
}
