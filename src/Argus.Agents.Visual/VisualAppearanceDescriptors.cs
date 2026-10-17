using Argus.Core;
using StbImageSharp;

namespace Argus.Agents.Visual;

/// <summary>Measured appearance of unverified screenshot candidates; no enemy identity or classification.</summary>
public sealed record VisualAppearanceDescriptor(float X, float Y, int Width, int Height, bool ClippedAtViewportEdge,
    float MeanLuminance, float LuminanceStdDev, float ContrastToSurroundings, float GradientDensity,
    float MeanSaturation);

/// <summary>Single screenshot's measured palette candidates, without enemy classification.</summary>
public sealed record AppearanceCandidate(string Source, VisualAppearanceDescriptor Features);
public sealed record AppearanceFrame(bool PlayerLocated, IReadOnlyList<AppearanceCandidate> Candidates);

/// <summary>
/// Stateless, game-agnostic image features. Uses only RGB pixels inside the visible candidate bounding
/// rectangle and a four-pixel surrounding ring; independent of human labels, actions and game state.
/// </summary>
public static class VisualAppearanceDescriptors
{
    public static AppearanceFrame ObservePng(byte[] png, CombatVisualSettings settings)
    {
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
        var detected = CombatPixelPerception.ObserveShadowRgb(image.Data, image.Width, image.Height, settings);
        if (detected.Baseline.Player is null) return new AppearanceFrame(false, []);

        var candidates = detected.Baseline.ThreatCandidates.Select(b =>
                new AppearanceCandidate("baseline", Measure(image.Data, image.Width, image.Height, b)))
            .Concat(detected.AddedCandidates.Select(b =>
                new AppearanceCandidate("shadow", Measure(image.Data, image.Width, image.Height, b))))
            .ToArray();
        return new AppearanceFrame(true, candidates);
    }

    public static VisualAppearanceDescriptor Measure(byte[] rgb, int width, int height, VisualBlob blob)
    {
        if (width < 1 || height < 1 || rgb.LongLength != (long)width * height * 3)
            throw new ArgumentException("Invalid RGB image dimensions.", nameof(rgb));
        if (!float.IsFinite(blob.X) || !float.IsFinite(blob.Y) || blob.Width < 1 || blob.Height < 1)
            throw new ArgumentOutOfRangeException(nameof(blob));

        var left = (int)MathF.Floor(blob.X - (blob.Width - 1f) / 2);
        var top = (int)MathF.Floor(blob.Y - (blob.Height - 1f) / 2);
        var right = left + blob.Width - 1;
        var bottom = top + blob.Height - 1;
        var x0 = Math.Clamp(left, 0, width - 1);
        var y0 = Math.Clamp(top, 0, height - 1);
        var x1 = Math.Clamp(right, 0, width - 1);
        var y1 = Math.Clamp(bottom, 0, height - 1);
        if (x0 > x1 || y0 > y1 || right < 0 || bottom < 0 || left >= width || top >= height)
            throw new ArgumentOutOfRangeException(nameof(blob), "Candidate must intersect the visible image.");

        var count = 0;
        var mean = 0d;
        var squaredDeviation = 0d;
        var saturation = 0d;
        var comparisons = 0;
        var gradients = 0;
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var luminance = Luma(rgb, width, x, y);
                count++;
                var delta = luminance - mean;
                mean += delta / count;
                squaredDeviation += delta * (luminance - mean);

                var offset = (y * width + x) * 3;
                var max = Math.Max(rgb[offset], Math.Max(rgb[offset + 1], rgb[offset + 2]));
                var min = Math.Min(rgb[offset], Math.Min(rgb[offset + 1], rgb[offset + 2]));
                saturation += (max - min) / 255d;

                if (x > x0)
                {
                    comparisons++;
                    if (Math.Abs(luminance - Luma(rgb, width, x - 1, y)) >= 0.075) gradients++;
                }
                if (y > y0)
                {
                    comparisons++;
                    if (Math.Abs(luminance - Luma(rgb, width, x, y - 1)) >= 0.075) gradients++;
                }
            }

        var ringSum = 0d;
        var ringCount = 0;
        for (var y = Math.Max(0, top - 4); y <= Math.Min(height - 1, bottom + 4); y++)
            for (var x = Math.Max(0, left - 4); x <= Math.Min(width - 1, right + 4); x++)
            {
                if (x >= left && x <= right && y >= top && y <= bottom) continue;
                ringSum += Luma(rgb, width, x, y);
                ringCount++;
            }

        return new VisualAppearanceDescriptor(blob.X, blob.Y, blob.Width, blob.Height,
            left < 0 || top < 0 || right >= width || bottom >= height,
            (float)mean, (float)Math.Sqrt(squaredDeviation / count),
            ringCount == 0 ? 0 : (float)Math.Abs(mean - ringSum / ringCount),
            comparisons == 0 ? 0 : (float)gradients / comparisons, (float)(saturation / count));
    }

    private static double Luma(byte[] rgb, int width, int x, int y)
    {
        var offset = (y * width + x) * 3;
        return (0.2126 * rgb[offset] + 0.7152 * rgb[offset + 1] + 0.0722 * rgb[offset + 2]) / 255d;
    }
}
