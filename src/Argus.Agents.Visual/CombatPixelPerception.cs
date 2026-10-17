using Argus.Core;
using StbImageSharp;

namespace Argus.Agents.Visual;

/// <summary>Bounding box inferred from visible RGB pixels, not an actor identity or privileged world coordinate.</summary>
public sealed record VisualBlob(float X, float Y, int Width, int Height, int MatchingPixels, float Confidence);

public sealed record CombatVisualObservation(VisualBlob? Player, IReadOnlyList<VisualBlob> ThreatCandidates)
{
    public float? NearestThreatDistance => Player is null || ThreatCandidates.Count == 0
        ? null
        : ThreatCandidates.Min(t => MathF.Sqrt((t.X - Player.X) * (t.X - Player.X) + (t.Y - Player.Y) * (t.Y - Player.Y)));
}

/// <summary>
/// Experimental color segmentation, restricted to the middle of the visible viewport.
/// Decorative objects may match threat palettes: they are candidates, not known enemies.
/// </summary>
public static class CombatPixelPerception
{
    public static CombatVisualObservation Observe(byte[] pngBytes, CombatVisualSettings settings)
    {
        var image = ImageResult.FromMemory(pngBytes, ColorComponents.RedGreenBlue);
        return ObserveRgb(image.Data, image.Width, image.Height, settings);
    }

    public static CombatVisualObservation ObserveRgb(byte[] rgb, int width, int height, CombatVisualSettings settings)
    {
        settings.Validate();
        if (width < 100 || height < 150 || rgb.Length != width * height * 3)
            return new CombatVisualObservation(null, []);

        var player = Locate(rgb, width, height, settings.PlayerPalette!, settings.MinimumPlayerPixels)
            .OrderByDescending(b => b.MatchingPixels).FirstOrDefault();

        if (player is null) return new CombatVisualObservation(null, []);

        var threats = settings.ThreatPalettes
            .SelectMany(p => Locate(rgb, width, height, p, settings.MinimumThreatPixels))
            .Where(p => Distance(p, player) > 14)
            .OrderByDescending(p => p.MatchingPixels)
            .ToArray();

        // The same sprite can match several palette rules; keep only one candidate per screen position.
        var unique = new List<VisualBlob>();
        foreach (var candidate in threats)
        {
            if (unique.All(existing => Distance(existing, candidate) >= 18))
                unique.Add(candidate with { Confidence = MathF.Min(0.65f, candidate.Confidence) });
        }

        return new CombatVisualObservation(player, unique);
    }

    private static IEnumerable<VisualBlob> Locate(byte[] rgb, int width, int height, VisualColorRule rule, int minimumPixels)
    {
        var worldTop = height * 19 / 100;
        var worldBottom = height * 82 / 100;
        var mask = new bool[width * height];
        var visited = new bool[width * height];
        var queue = new int[width * height];

        for (var y = worldTop; y < worldBottom; y++)
        {
            for (var x = width / 30; x < width * 29 / 30; x++)
            {
                var pixel = y * width + x;
                var p = pixel * 3;
                mask[pixel] = rule.Matches(rgb[p], rgb[p + 1], rgb[p + 2]);
            }
        }

        var blobs = new List<VisualBlob>();
        for (var y = worldTop; y < worldBottom; y++)
        {
            for (var x = width / 30; x < width * 29 / 30; x++)
            {
                var start = y * width + x;
                if (!mask[start] || visited[start]) continue;

                var head = 0;
                var tail = 1;
                queue[0] = start;
                visited[start] = true;
                var minX = x;
                var maxX = x;
                var minY = y;
                var maxY = y;
                var count = 0;

                while (head < tail)
                {
                    var pos = queue[head++];
                    var px = pos % width;
                    var py = pos / width;
                    count++;
                    minX = Math.Min(minX, px);
                    maxX = Math.Max(maxX, px);
                    minY = Math.Min(minY, py);
                    maxY = Math.Max(maxY, py);

                    // Join nearby sprite pixels through small gaps without assuming a sprite atlas.
                    for (var dy = -2; dy <= 2; dy++)
                    {
                        var ny = py + dy;
                        if (ny < worldTop || ny >= worldBottom) continue;
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var nx = px + dx;
                            if (nx < width / 30 || nx >= width * 29 / 30) continue;
                            var next = ny * width + nx;
                            if (!mask[next] || visited[next]) continue;
                            visited[next] = true;
                            queue[tail++] = next;
                        }
                    }
                }

                var blobWidth = maxX - minX + 1;
                var blobHeight = maxY - minY + 1;
                if (count < minimumPixels || blobWidth is < 8 or > 52 || blobHeight is < 12 or > 60) continue;

                var density = count / (float)(blobWidth * blobHeight);
                var confidence = MathF.Min(0.90f, 0.35f + 0.5f * density + MathF.Min(0.15f, count / 1000f));
                blobs.Add(new VisualBlob((minX + maxX) / 2f, (minY + maxY) / 2f, blobWidth, blobHeight, count, confidence));
            }
        }

        return blobs;
    }

    private static float Distance(VisualBlob a, VisualBlob b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
