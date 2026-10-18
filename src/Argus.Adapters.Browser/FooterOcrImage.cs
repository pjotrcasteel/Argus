using StbImageSharp;

namespace Argus.Adapters.Browser;

/// <summary>
/// Optional viewport-relative footer enlargement for OCR on small action controls.
/// It samples screenshot pixels only; no DOM, engine, game label, button location
/// or gameplay identity enters the image transformation.
/// </summary>
public sealed record FooterOcrRegion(byte[] BmpBytes, int OriginalTop, int Scale)
{
    public VisibleText Map(VisibleText label) => new(
        label.Text, label.X / Scale, OriginalTop + label.Y / Scale,
        Math.Max(1, label.Width / Scale), Math.Max(1, label.Height / Scale),
        label.Confidence);
}

public static class FooterOcrImage
{
    public const int Scale = 3;
    public const int TopPercent = 81;
    public const int BottomPercent = 96;

    public static FooterOcrRegion? FromPng(byte[] png)
    {
        if (png.Length == 0) return null;
        var screenshot = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
        return FromRgb(screenshot.Data, screenshot.Width, screenshot.Height);
    }

    public static FooterOcrRegion? FromRgb(byte[] rgb, int width, int height)
    {
        if (width < 100 || height < 200 || width > 3840 || height > 2160 ||
            rgb.Length != (long)width * height * 3) return null;

        var top = height * TopPercent / 100;
        var bottom = height * BottomPercent / 100;
        var bmpWidth = checked(width * Scale);
        var bmpHeight = checked((bottom - top) * Scale);
        var rowStride = checked((bmpWidth * 3 + 3) & ~3);
        var size = checked(54 + rowStride * bmpHeight);

        using var buffer = new MemoryStream(size);
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(size);
            writer.Write(0); // reserved
            writer.Write(54); // pixels follow standard 14+40 byte headers
            writer.Write(40); // Windows BITMAPINFOHEADER
            writer.Write(bmpWidth);
            writer.Write(bmpHeight); // positive: BMP stores rows bottom-up
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0); // BI_RGB, uncompressed
            writer.Write(rowStride * bmpHeight);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            for (var outputY = bmpHeight - 1; outputY >= 0; outputY--)
            {
                var originalY = top + outputY / Scale;
                for (var outputX = 0; outputX < bmpWidth; outputX++)
                {
                    var index = (originalY * width + outputX / Scale) * 3;
                    writer.Write(rgb[index + 2]); // BMP blue
                    writer.Write(rgb[index + 1]); // BMP green
                    writer.Write(rgb[index]);     // BMP red
                }
                for (var padding = bmpWidth * 3; padding < rowStride; padding++)
                    writer.Write((byte)0);
            }
        }

        return new FooterOcrRegion(buffer.ToArray(), top, Scale);
    }
}
