using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class VisualHudBarWitnessTests
{
    private const int Width = 390;
    private const int Height = 844;

    private static VisualHudBarSettings Settings => new()
    {
        LeftPercent = 2, RightPercent = 75, TopPercent = 3, BottomPercent = 9,
        MinimumConsecutivePixels = 14,
        FillPalettes =
        [
            new VisualColorRule { MinGreen = 54, MinGreenMinusRed = 6, MinGreenMinusBlue = 6 },
            new VisualColorRule { MinRed = 80, MinGreen = 40, MinRedMinusGreen = 16, MinGreenMinusBlue = 8 },
            new VisualColorRule { MinRed = 80, MinRedMinusGreen = 20, MinRedMinusBlue = 20 }
        ]
    };

    [TestMethod]
    [DataRow((byte)102, (byte)164, (byte)102)]
    [DataRow((byte)190, (byte)135, (byte)75)]
    [DataRow((byte)186, (byte)71, (byte)77)]
    public void IsVisibleRgb_OriginalHudColorVariations_KeepsActiveWitness(byte red, byte green, byte blue)
    {
        var rgb = new byte[Width * Height * 3];
        PaintBar(rgb, 20, 46, 18, red, green, blue);

        Assert.IsTrue(VisualHudBarWitness.IsVisibleRgb(rgb, Width, Height, Settings));
    }

    [TestMethod]
    public void IsVisibleRgb_FullscreenUpgradeWithoutStatusBar_RejectsCombatWitness()
    {
        var rgb = new byte[Width * Height * 3];
        PaintBar(rgb, 20, 300, 180, 190, 135, 75);

        Assert.IsFalse(VisualHudBarWitness.IsVisibleRgb(rgb, Width, Height, Settings));
    }

    [TestMethod]
    public void IsVisibleRgb_ShortOrOutsideHudStroke_RejectsFalsePresence()
    {
        var rgb = new byte[Width * Height * 3];
        PaintBar(rgb, 20, 46, 12, 102, 164, 102);
        PaintBar(rgb, 20, 125, 160, 102, 164, 102);

        Assert.IsFalse(VisualHudBarWitness.IsVisibleRgb(rgb, Width, Height, Settings));
    }

    [TestMethod]
    public void ProfileValidation_IllegalHudBounds_Rejects()
    {
        var invalid = new VisualGameProfile
        {
            CombatHudBar = new VisualHudBarSettings
            {
                TopPercent = 25, BottomPercent = 20,
                FillPalettes = [new VisualColorRule()]
            }
        };

        Assert.ThrowsExactly<ArgumentException>(() => invalid.Validate());
    }

    private static void PaintBar(byte[] rgb, int x, int y, int length, byte r, byte g, byte b)
    {
        for (var i = x; i < x + length; i++)
        {
            var pixel = (y * Width + i) * 3;
            rgb[pixel] = r;
            rgb[pixel + 1] = g;
            rgb[pixel + 2] = b;
        }
    }
}
