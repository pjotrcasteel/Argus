using Argus.Agents.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class IlluminatedEntryDetectorTests
{
    [TestMethod]
    public void DetectRgb_ThreeSeparatedWarmSources_ProposesOnlyCenterFromPixels()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        Paint(rgb, width, 48, 192, 23, 39);
        Paint(rgb, width, 191, 192, 29, 32);
        Paint(rgb, width, 324, 194, 23, 40);

        var result = IlluminatedEntryDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.IsTrue(Math.Abs(result.X - 205f) < 5);
        Assert.IsTrue(Math.Abs(result.Y - 208f) < 5);
        Assert.IsTrue(result.Confidence < 0.8f, "A visual affordance is a hypothesis, not a verified entrance.");
    }

    [TestMethod]
    public void DetectRgb_OneWarmSource_AbstainsInsteadOfClicking()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        Paint(rgb, width, 191, 192, 29, 32);

        Assert.IsNull(IlluminatedEntryDetector.DetectRgb(rgb, width, height));
    }

    [TestMethod]
    public void DetectRgb_OnlySideTorches_AbstainsInsteadOfInventingCenter()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        Paint(rgb, width, 48, 192, 23, 39);
        Paint(rgb, width, 324, 194, 23, 40);

        Assert.IsNull(IlluminatedEntryDetector.DetectRgb(rgb, width, height));
    }

    [TestMethod]
    public void DetectRgb_InvalidFrame_Abstains()
    {
        Assert.IsNull(IlluminatedEntryDetector.DetectRgb([], 390, 844));
    }

    private static void Paint(byte[] rgb, int width, int left, int top, int boxWidth, int boxHeight)
    {
        for (var y = top; y < top + boxHeight; y++)
        {
            for (var x = left; x < left + boxWidth; x++)
            {
                var pixel = (y * width + x) * 3;
                rgb[pixel] = 220;
                rgb[pixel + 1] = 140;
                rgb[pixel + 2] = 40;
            }
        }
    }
}
