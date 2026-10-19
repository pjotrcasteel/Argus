using Argus.Adapters.Browser;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StbImageSharp;

namespace Argus.Tests;

[TestClass]
public sealed class FooterOcrImageTests
{
    [TestMethod]
    public void FromRgb_ViewportFooter_ResamplesOriginalPixelsWithoutKnownGameCoordinates()
    {
        const int width = 390;
        const int height = 844;
        var source = new byte[width * height * 3];
        var originalX = 167;
        var originalY = 759;
        var sample = (originalY * width + originalX) * 3;
        source[sample] = 210;
        source[sample + 1] = 155;
        source[sample + 2] = 80;

        var region = FooterOcrImage.FromRgb(source, width, height);

        Assert.IsNotNull(region);
        Assert.AreEqual(height * 81 / 100, region.OriginalTop);
        Assert.AreEqual(3, region.Scale);
        Assert.AreEqual((byte)'B', region.BmpBytes[0]);
        Assert.AreEqual((byte)'M', region.BmpBytes[1]);

        var decoded = ImageResult.FromMemory(region.BmpBytes, ColorComponents.RedGreenBlue);
        Assert.AreEqual(width * region.Scale, decoded.Width);
        Assert.AreEqual((height * 96 / 100 - region.OriginalTop) * region.Scale, decoded.Height);

        var x = originalX * region.Scale;
        var y = (originalY - region.OriginalTop) * region.Scale;
        var pixel = (y * decoded.Width + x) * 3;
        Assert.AreEqual((byte)210, decoded.Data[pixel]);
        Assert.AreEqual((byte)155, decoded.Data[pixel + 1]);
        Assert.AreEqual((byte)80, decoded.Data[pixel + 2]);
    }

    [TestMethod]
    public void Map_FooterOcrBox_ReturnsOriginalViewportCoordinates()
    {
        var region = FooterOcrImage.FromRgb(new byte[390 * 844 * 3], 390, 844);
        Assert.IsNotNull(region);

        const int x = 180;
        const int y = 758;
        var imageText = new VisibleText(
            "BEGIN FIRST HUNT", x * 3, (y - region.OriginalTop) * 3, 90 * 3, 10 * 3, 95);
        var restored = region.Map(imageText);

        Assert.AreEqual(x, restored.X);
        Assert.AreEqual(y, restored.Y);
        Assert.AreEqual(90, restored.Width);
        Assert.AreEqual(10, restored.Height);
        Assert.AreEqual(95f, restored.Confidence);
    }

    [TestMethod]
    public void FromRgb_MalformedFrame_DoesNotGenerateInventedPixels()
    {
        Assert.IsNull(FooterOcrImage.FromRgb([], 390, 844));
        Assert.IsNull(FooterOcrImage.FromRgb(new byte[390 * 844 * 3], 390, 100));
    }
}
