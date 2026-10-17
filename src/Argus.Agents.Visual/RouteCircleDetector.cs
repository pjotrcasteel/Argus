using StbImageSharp;

namespace Argus.Agents.Visual;

public sealed record VisualCircle(float X, float Y, float Confidence);

/// <summary>Pixel-only circle detection. Prefer warm outlined connected nodes over cool decorative map geometry.</summary>
public static class RouteCircleDetector
{
    private static readonly (double X, double Y)[] Directions = Enumerable.Range(0, 16)
        .Select(i => (Math.Cos(i * Math.PI / 8), Math.Sin(i * Math.PI / 8))).ToArray();

    public static VisualCircle? Detect(byte[] screenshot)
    {
        var image = ImageResult.FromMemory(screenshot, ColorComponents.RedGreenBlue);
        return DetectRgb(image.Data, image.Width, image.Height);
    }

    public static VisualCircle? DetectRgb(byte[] rgb, int width, int height)
    {
        if (width < 100 || height < 150 || rgb.Length != width * height * 3) return null;

        var bestWarm = new VisualCircle(0, 0, 0);
        var bestCool = new VisualCircle(0, 0, 0);
        var minX = Math.Max(24, width / 14);

        for (var y = height / 8; y < height * 4 / 5; y += 5)
        {
            for (var x = minX; x < width - minX; x += 5)
            {
                var warm = 0;
                var cool = 0;
                foreach (var (dx, dy) in Directions)
                {
                    var foundWarm = false;
                    var foundCool = false;

                    for (var radius = 14; radius <= 22; radius++)
                    {
                        var xx = (int)Math.Round(x + radius * dx);
                        var yy = (int)Math.Round(y + radius * dy);
                        if (xx < 0 || xx >= width || yy < 0 || yy >= height) continue;

                        var p = (yy * width + xx) * 3;
                        var red = rgb[p];
                        var green = rgb[p + 1];
                        var blue = rgb[p + 2];
                        foundWarm |= IsWarmRingPixel(red, green, blue);
                        foundCool |= IsCoolRingPixel(red, green, blue);
                        if (foundWarm && foundCool) break;
                    }

                    if (foundWarm) warm++;
                    if (foundCool) cool++;
                }

                if (warm >= 10 && warm > bestWarm.Confidence * 16)
                    bestWarm = new VisualCircle(x, y, warm / 16f);

                if (cool >= 10 && cool > bestCool.Confidence * 16)
                    bestCool = new VisualCircle(x, y, cool / 16f);
            }
        }

        // Connected, interactable nodes have a strong gold/rose border in observed WyrmForge routes.
        // This avoids false "circles" caused by cool-colored rivers and crossings.
        return bestWarm.Confidence >= 10f / 16 ? bestWarm
            : bestCool.Confidence >= 10f / 16 ? bestCool
            : null;
    }

    private static bool IsWarmRingPixel(byte red, byte green, byte blue)
    {
        var rose = red >= 105 && red - green >= 20 && green - blue >= 8 && red - blue >= 32;
        var gold = red >= 125 && green >= 105 && red - blue >= 35 && green - blue >= 15;
        return rose || gold;
    }

    private static bool IsCoolRingPixel(byte red, byte green, byte blue)
    {
        var violet = red >= 95 && blue >= 110 && blue * 10 > green * 11 && red * 100 > green * 107;
        var cyan = green >= 88 && blue >= 100 && blue * 10 > red * 12 && green * 10 > red * 12;
        return violet || cyan;
    }
}
