using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// A current-screenshot visual contradiction to a STALE player-memory hypothesis.
/// Not a detector, a person classifier, a positive identity confirmation, or live policy.
/// </summary>
public sealed record PlayerCurrentFrameWitness(string Status, int SampledPixels, int PlayerPalettePixels,
    float MaximumRgbChannelStdDev, float StrongGradientFraction)
{
    public bool RejectsStaleHypothesis => Status == "EMPTY_PATCH_VETO";
}

/// <summary>
/// Conservative, stateless shadow-only rejection rule. Colored effects, busy scenery, and
/// even a fully visible actor all remain INDETERMINATE, never "player confirmed".
/// </summary>
public static class PlayerCurrentFrameWitnessProbe
{
    public const int Radius = 24;
    public const float MaxEmptyChannelStdDev = 5f;
    public const float MaxEmptyGradientFraction = 0.01f;
    private const int HighContrastChannelDifference = 15;

    public static PlayerCurrentFrameWitness InspectRgb(byte[] rgb, int width, int height,
        CombatVisualSettings settings, VisualCandidatePoint staleCenter)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        settings.Validate();
        if (width < 100 || height < 150 || (long)width * height * 3 != rgb.LongLength)
            throw new ArgumentException("Invalid original screenshot dimensions.", nameof(rgb));
        if (!float.IsFinite(staleCenter.X) || !float.IsFinite(staleCenter.Y) ||
            staleCenter.X < 0 || staleCenter.Y < 0 || staleCenter.X >= width || staleCenter.Y >= height)
            throw new ArgumentOutOfRangeException(nameof(staleCenter));

        var left = Math.Max(0, (int)MathF.Round(staleCenter.X) - Radius);
        var top = Math.Max(0, (int)MathF.Round(staleCenter.Y) - Radius);
        var right = Math.Min(width - 1, (int)MathF.Round(staleCenter.X) + Radius);
        var bottom = Math.Min(height - 1, (int)MathF.Round(staleCenter.Y) + Radius);
        var n = 0;
        var palette = 0;
        var edges = 0;
        var comparisons = 0;
        double redSum = 0, greenSum = 0, blueSum = 0;
        double redSquares = 0, greenSquares = 0, blueSquares = 0;

        for (var y = top; y <= bottom; y++)
            for (var x = left; x <= right; x++)
            {
                var p = (y * width + x) * 3;
                var red = rgb[p];
                var green = rgb[p + 1];
                var blue = rgb[p + 2];
                n++;
                redSum += red;
                greenSum += green;
                blueSum += blue;
                redSquares += red * red;
                greenSquares += green * green;
                blueSquares += blue * blue;
                if (settings.PlayerPalette!.Matches(red, green, blue)) palette++;

                if (x > left)
                {
                    comparisons++;
                    if (StrongGradient(rgb, p, p - 3)) edges++;
                }
                if (y > top)
                {
                    comparisons++;
                    if (StrongGradient(rgb, p, p - width * 3)) edges++;
                }
            }

        var deviation = (float)Math.Sqrt(Math.Max(0,
            Math.Max(redSquares / n - Math.Pow(redSum / n, 2),
                Math.Max(greenSquares / n - Math.Pow(greenSum / n, 2),
                    blueSquares / n - Math.Pow(blueSum / n, 2)))));
        var gradient = comparisons == 0 ? 0 : edges / (float)comparisons;
        // No positive proof exists. A truly uniform patch without ANY player palette pixels
        // merely contradicts the assumption that the old actor is still evidenced here.
        var blank = n >= 400 && palette == 0 && deviation < MaxEmptyChannelStdDev &&
            gradient < MaxEmptyGradientFraction;
        return new PlayerCurrentFrameWitness(blank ? "EMPTY_PATCH_VETO" : "INDETERMINATE",
            n, palette, deviation, gradient);
    }

    private static bool StrongGradient(byte[] rgb, int a, int b) =>
        Math.Abs(rgb[a] - rgb[b]) >= HighContrastChannelDifference ||
        Math.Abs(rgb[a + 1] - rgb[b + 1]) >= HighContrastChannelDifference ||
        Math.Abs(rgb[a + 2] - rgb[b + 2]) >= HighContrastChannelDifference;
}
