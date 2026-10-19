using Argus.Core;
using StbImageSharp;

namespace Argus.Agents.Visual;

public static class VisualHudBarWitness
{
    public static bool IsVisible(byte[] png, VisualHudBarSettings settings)
    {
        settings.Validate();
        if (png.Length == 0) return false;
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
        return IsVisibleRgb(image.Data, image.Width, image.Height, settings);
    }

    public static bool IsVisibleRgb(byte[] rgb, int width, int height, VisualHudBarSettings settings)
    {
        settings.Validate();
        if (width < 100 || height < 150 || rgb.Length != width * height * 3) return false;
        var left = width * settings.LeftPercent / 100;
        var right = Math.Min(width, (width * settings.RightPercent + 99) / 100);
        var top = height * settings.TopPercent / 100;
        var bottom = Math.Min(height, (height * settings.BottomPercent + 99) / 100);
        for (var y = top; y < bottom; y++)
        {
            var run = 0;
            for (var x = left; x < right; x++)
            {
                var i = (y * width + x) * 3;
                var match = settings.FillPalettes.Any(rule => rule.Matches(rgb[i], rgb[i + 1], rgb[i + 2]));
                run = match ? run + 1 : 0;
                if (run >= settings.MinimumConsecutivePixels) return true;
            }
        }
        return false;
    }
}
