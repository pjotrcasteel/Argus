using StbImageSharp;

namespace Argus.Agents.Visual;

public sealed record VisualCircle(float X, float Y, float Confidence);

/// <summary>
/// Finds high-contrast circular outlines in a visible screenshot. This is approximate image perception, not map data.
/// Color thresholds and 14-22px radius are an initial local-baseline heuristic, not a general-purpose circle recognizer.
/// </summary>
public static class RouteCircleDetector
{
    private static readonly (double X, double Y)[] Directions = Enumerable.Range(0, 16)
        .Select(i => (Math.Cos(i * Math.PI / 8), Math.Sin(i * Math.PI / 8))).ToArray();

    public static VisualCircle? Detect(byte[] screenshot)
    {
        var image = ImageResult.FromMemory(screenshot, ColorComponents.RedGreenBlue);
        return DetectRgb(image.Data, image.Width, image.Height);
    }

    /// <summary>Public raw-RGB entry point makes the detector testable without installing a browser or PNG encoder.</summary>
    public static VisualCircle? DetectRgb(byte[] rgb, int width, int height)
    {
        if (width < 100 || height < 150 || rgb.Length != width * height * 3) return null;

        var bestScore = 9;
        var bestX = 0;
        var bestY = 0;
        var minX = Math.Max(24, width / 14);

        // Ignore the top HUD/header and bottom fixed action footer: they are not selectable map nodes.
        for (var y = height / 8; y < height * 4 / 5; y += 5)
        {
            for (var x = minX; x < width - minX; x += 5)
            {
                var score = 0;
                foreach (var (dx, dy) in Directions)
                {
                    if (HasRingPixel(rgb, width, height, x, y, dx, dy)) score++;
                }

                if (score < 10 || score < bestScore || (score == bestScore && y < bestY)) continue;
                bestScore = score;
                bestX = x;
                bestY = y;
            }
        }

        return bestScore >= 10 ? new VisualCircle(bestX, bestY, bestScore / 16f) : null;
    }

    private static bool HasRingPixel(byte[] rgb, int width, int height, int centerX, int centerY, double dx, double dy)
    {
        for (var radius = 14; radius <= 22; radius++)
        {
            var x = (int)Math.Round(centerX + radius * dx);
            var y = (int)Math.Round(centerY + radius * dy);
            if (x < 0 || x >= width || y < 0 || y >= height) continue;

            var p = (y * width + x) * 3;
            var red = rgb[p];
            var green = rgb[p + 1];
            var blue = rgb[p + 2];
            var violet = red >= 95 && blue >= 110 && blue * 10 > green * 11 && red * 100 > green * 107;
            var cyan = green >= 88 && blue >= 100 && blue * 10 > red * 12 && green * 10 > red * 12;
            if (violet || cyan) return true;
        }

        return false;
    }
}
