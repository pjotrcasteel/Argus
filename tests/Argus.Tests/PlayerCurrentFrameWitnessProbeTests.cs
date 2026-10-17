using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PlayerCurrentFrameWitnessProbeTests
{
    private const int Width = 390;
    private const int Height = 844;
    private static readonly VisualCandidatePoint StaleCenter = new(194, 420);
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
    public void InspectRgb_UniformEmptyBackground_VetoesStalePlayerWithoutClaimingNewActor()
    {
        var witness = Inspect(Fill());
        Assert.AreEqual("EMPTY_PATCH_VETO", witness.Status);
        Assert.IsTrue(witness.RejectsStaleHypothesis);
        Assert.AreEqual(0, witness.PlayerPalettePixels);
        Assert.AreEqual(0f, witness.StrongGradientFraction);
        Assert.IsTrue(witness.MaximumRgbChannelStdDev < 0.01f);
    }

    [TestMethod]
    public void InspectRgb_FragmentedButHumanVisibleBluePixels_DoesNotRejectAnOccludedPlayer()
    {
        var rgb = Fill();
        Paint(rgb, 190, 416, 9, 7, 72, 91, 115);
        var witness = Inspect(rgb);
        Assert.AreEqual("INDETERMINATE", witness.Status);
        Assert.IsFalse(witness.RejectsStaleHypothesis);
        Assert.AreEqual(63, witness.PlayerPalettePixels);
        Assert.IsTrue(witness.MaximumRgbChannelStdDev > 5f);
    }

    [TestMethod]
    public void InspectRgb_OverwideSpellPatch_CannotConfirmPlayerJustFromCurrentColor()
    {
        var rgb = Fill();
        Paint(rgb, 165, 400, 60, 36, 72, 91, 115);
        var witness = Inspect(rgb);
        Assert.AreEqual("INDETERMINATE", witness.Status);
        Assert.IsTrue(witness.PlayerPalettePixels > 500);
    }

    [TestMethod]
    public void InspectRgb_TexturedNonPlayerDecoration_RemainsUnverifiedRatherThanBeingMisidentified()
    {
        var rgb = Fill();
        for (var y = 396; y < 445; y++)
            for (var x = 170; x < 219; x++)
            {
                if ((x + y) % 2 != 0) continue;
                var offset = (y * Width + x) * 3;
                rgb[offset] = 92;
                rgb[offset + 1] = 43;
                rgb[offset + 2] = 46;
            }

        var witness = Inspect(rgb);
        Assert.AreEqual("INDETERMINATE", witness.Status);
        Assert.AreEqual(0, witness.PlayerPalettePixels);
        Assert.IsTrue(witness.StrongGradientFraction > 0.1f);
    }

    [TestMethod]
    public void InspectRgb_UniformPlayerPaletteDecoy_DoesNotBecomeAConfirmedPlayer()
    {
        var rgb = Fill(72, 91, 115);
        var witness = Inspect(rgb);
        Assert.AreEqual("INDETERMINATE", witness.Status);
        Assert.IsFalse(witness.RejectsStaleHypothesis);
        Assert.IsTrue(witness.PlayerPalettePixels > 2000);
        Assert.IsTrue(witness.MaximumRgbChannelStdDev < 0.01f);
    }

    [TestMethod]
    public void InspectRgb_ScreenEdgeAndBadCenter_ClampsSafelyOrFailsClosed()
    {
        var rgb = Fill();
        var edge = PlayerCurrentFrameWitnessProbe.InspectRgb(rgb, Width, Height, Settings,
            new VisualCandidatePoint(0, 0));
        Assert.AreEqual("EMPTY_PATCH_VETO", edge.Status);
        Assert.IsTrue(edge.SampledPixels >= 400);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PlayerCurrentFrameWitnessProbe.InspectRgb(rgb, Width, Height, Settings,
                new VisualCandidatePoint(-1, 420)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            PlayerCurrentFrameWitnessProbe.InspectRgb(rgb[..100], Width, Height, Settings, StaleCenter));
    }

    private static PlayerCurrentFrameWitness Inspect(byte[] pixels) =>
        PlayerCurrentFrameWitnessProbe.InspectRgb(pixels, Width, Height, Settings, StaleCenter);

    private static byte[] Fill(byte red = 30, byte green = 39, byte blue = 34)
    {
        var bytes = new byte[Width * Height * 3];
        for (var i = 0; i < bytes.Length; i += 3)
        {
            bytes[i] = red;
            bytes[i + 1] = green;
            bytes[i + 2] = blue;
        }
        return bytes;
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
