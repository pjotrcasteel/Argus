using StbImageSharp;

namespace Argus.Agents.Visual;

/// <summary>A shift of visible background pixels, not a world-space camera coordinate or engine velocity.</summary>
public sealed record VisualSceneShift(int X, int Y, float Confidence, float MeanPixelError);

/// <summary>Estimates background translation from textured viewport pixels, ignoring the player and HUD.</summary>
public static class VisualSceneMotion
{
    public static VisualSceneShift? Estimate(byte[] previousPng, byte[] currentPng)
    {
        var previous = ImageResult.FromMemory(previousPng, ColorComponents.RedGreenBlue);
        var current = ImageResult.FromMemory(currentPng, ColorComponents.RedGreenBlue);
        if (previous.Width != current.Width || previous.Height != current.Height) return null;
        return EstimateRgb(previous.Data, current.Data, previous.Width, previous.Height);
    }

    public static VisualSceneShift? EstimateRgb(byte[] previous, byte[] current, int width, int height)
    {
        if (width < 150 || height < 200 || previous.Length != width * height * 3 || current.Length != previous.Length)
            return null;

        var samples = CollectTexturedSamples(previous, width, height);
        if (samples.Count < 32) return null;

        var zero = Score(previous, current, width, height, samples, 0, 0);
        var best = (X: 0, Y: 0, Error: zero);
        for (var dy = -48; dy <= 48; dy += 8)
        {
            for (var dx = -48; dx <= 48; dx += 8)
            {
                var error = Score(previous, current, width, height, samples, dx, dy);
                if (error < best.Error) best = (dx, dy, error);
            }
        }

        best = Refine(previous, current, width, height, samples, best, 8, 2);
        best = Refine(previous, current, width, height, samples, best, 2, 1);
        var improvement = zero - best.Error;
        if (best.Error > 35 || ((best.X != 0 || best.Y != 0) && improvement < 2.5f)) return null;

        var confidence = best.X == 0 && best.Y == 0
            ? Math.Clamp((25f - best.Error) / 25f, 0.2f, 0.8f)
            : Math.Clamp(0.3f + improvement / 30f, 0.3f, 0.95f);
        return new VisualSceneShift(best.X, best.Y, confidence, best.Error);
    }

    private static (int X, int Y, float Error) Refine(byte[] previous, byte[] current, int width, int height,
        IReadOnlyList<int> samples, (int X, int Y, float Error) best, int radius, int stride)
    {
        var winner = best;
        for (var dy = best.Y - radius; dy <= best.Y + radius; dy += stride)
        {
            for (var dx = best.X - radius; dx <= best.X + radius; dx += stride)
            {
                if (dx is < -56 or > 56 || dy is < -56 or > 56) continue;
                var error = Score(previous, current, width, height, samples, dx, dy);
                if (error < winner.Error) winner = (dx, dy, error);
            }
        }
        return winner;
    }

    private static List<int> CollectTexturedSamples(byte[] rgb, int width, int height)
    {
        var result = new List<int>();
        for (var y = height / 4; y < height * 3 / 4; y += 12)
        {
            for (var x = width / 8; x < width * 7 / 8; x += 12)
            {
                if (Math.Abs(x - width / 2) < width / 7 && Math.Abs(y - height / 2) < height / 7) continue;
                var p = (y * width + x) * 3;
                var q = p + 3;
                var gradient = Math.Abs(rgb[p] - rgb[q]) + Math.Abs(rgb[p + 1] - rgb[q + 1]) +
                    Math.Abs(rgb[p + 2] - rgb[q + 2]);
                if (gradient >= 18) result.Add(y * width + x);
            }
        }
        return result;
    }

    private static float Score(byte[] previous, byte[] current, int width, int height, IReadOnlyList<int> samples, int dx, int dy)
    {
        var total = 0f;
        var count = 0;
        foreach (var pos in samples)
        {
            var x = pos % width + dx;
            var y = pos / width + dy;
            if (x < 0 || y < 0 || x >= width || y >= height) continue;
            var a = pos * 3;
            var b = (y * width + x) * 3;
            var difference = (Math.Abs(previous[a] - current[b]) +
                Math.Abs(previous[a + 1] - current[b + 1]) +
                Math.Abs(previous[a + 2] - current[b + 2])) / 3;
            total += Math.Min(70, difference);
            count++;
        }
        return count < samples.Count * 3 / 4 ? float.PositiveInfinity : total / count;
    }
}
