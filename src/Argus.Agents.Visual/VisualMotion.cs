using Argus.Core;
using StbImageSharp;

namespace Argus.Agents.Visual;

/// <summary>
/// Detects changed screen pixels in four world-view quadrants. These are not enemy positions or privileged game data.
/// </summary>
public static class VisualMotion
{
    public static (float Change, float[] Quadrants) Measure(byte[] previous, byte[] current)
    {
        var earlier = ImageResult.FromMemory(previous, ColorComponents.RedGreenBlue);
        var later = ImageResult.FromMemory(current, ColorComponents.RedGreenBlue);
        if (earlier.Width != later.Width || earlier.Height != later.Height) return (0, new float[4]);

        var counts = new int[4];
        var changed = new int[4];
        var w = later.Width;
        var h = later.Height;

        // Ignore top and bottom HUD areas; sample only every eighth pixel to keep decisions inexpensive.
        for (var y = h / 5; y < h * 4 / 5; y += 8)
        {
            for (var x = w / 12; x < w * 11 / 12; x += 8)
            {
                var quadrant = (y < h / 2 ? 0 : 2) + (x < w / 2 ? 0 : 1);
                var p = (y * w + x) * 3;
                var delta = Math.Abs(later.Data[p] - earlier.Data[p])
                    + Math.Abs(later.Data[p + 1] - earlier.Data[p + 1])
                    + Math.Abs(later.Data[p + 2] - earlier.Data[p + 2]);
                counts[quadrant]++;
                if (delta > 75) changed[quadrant]++;
            }
        }

        var ratios = Enumerable.Range(0, 4).Select(i => counts[i] == 0 ? 0f : changed[i] / (float)counts[i]).ToArray();
        return (ratios.Average(), ratios);
    }

    /// <summary>Exploratory policy: prefer quieter screen directions, rotate deterministically if activity is ambiguous.</summary>
    public static int ChooseDirection(float[] quadrants, int step)
    {
        if (quadrants.Length != 4) throw new ArgumentException("Exactly four motion quadrants are required.", nameof(quadrants));

        var top = quadrants[0] + quadrants[1];
        var left = quadrants[0] + quadrants[2];
        var bottom = quadrants[2] + quadrants[3];
        var right = quadrants[1] + quadrants[3];
        var costs = new[] { top, left, bottom, right };
        if (costs.Max() - costs.Min() < 0.05f) return (step / 4) % 4;

        var min = costs.Min();
        return Array.IndexOf(costs, min);
    }
}
