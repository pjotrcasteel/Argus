using StbImageSharp;

namespace Argus.Agents.Visual;

public sealed record VisualButton(float X, float Y, float Confidence);

/// <summary>Finds a wide, visibly enabled, filled purple action surface near the lower viewport. Not a DOM button lookup.</summary>
public static class FilledActionButtonDetector
{
    public static VisualButton? Detect(byte[] screenshot)
    {
        var image = ImageResult.FromMemory(screenshot, ColorComponents.RedGreenBlue);
        return DetectRgb(image.Data, image.Width, image.Height);
    }

    public static VisualButton? DetectRgb(byte[] rgb, int width, int height)
    {
        if (width < 100 || height < 150 || rgb.Length != width * height * 3) return null;

        var firstX = width / 10;
        var lastX = width * 9 / 10;
        var selectedY = -1;
        var bestFill = 0f;

        // The viewport-relative region describes a common mobile action footer, not a game's fixed coordinates.
        for (var y = height * 72 / 100; y < height * 98 / 100; y += 3)
        {
            var matches = 0;
            var sampled = 0;
            for (var x = firstX; x < lastX; x += 5)
            {
                var p = (y * width + x) * 3;
                var red = rgb[p];
                var green = rgb[p + 1];
                var blue = rgb[p + 2];
                sampled++;
                if (red >= 75 && green >= 40 && blue >= 90 && blue * 10 > red * 11 && red * 10 > green * 11)
                    matches++;
            }

            var fill = matches / (float)sampled;
            if (fill < 0.75f || fill < bestFill) continue;
            bestFill = fill;
            selectedY = y;
        }

        return selectedY < 0 ? null : new VisualButton(width / 2f, selectedY, bestFill);
    }
}
