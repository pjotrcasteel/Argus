using StbImageSharp;

namespace Argus.Agents.Visual;

/// <summary>
/// A one-shot, uncertain screenshot affordance: central warm focal light with
/// two similarly high, separated flanking lights. This is NOT a gate classifier.
/// Other games may opt in through an external profile only.
/// </summary>
public sealed record VisualEntryProposal(float X, float Y, float Confidence);

public static class IlluminatedEntryDetector
{
    public static VisualEntryProposal? Detect(byte[] image)
    {
        if (image.Length == 0) return null;
        var decoded = ImageResult.FromMemory(image, ColorComponents.RedGreenBlue);
        return DetectRgb(decoded.Data, decoded.Width, decoded.Height);
    }

    public static VisualEntryProposal? DetectRgb(byte[] rgb, int width, int height)
    {
        if (width < 200 || height < 350 || rgb.Length != (long)width * height * 3)
            return null;

        // Screen-relative area of interest, not a click coordinate or a DOM selector.
        var yMin = height * 12 / 100;
        var yMax = height * 44 / 100;
        var xMin = width * 5 / 100;
        var xMax = width * 95 / 100;
        var mask = new bool[width * height];
        for (var y = yMin; y < yMax; y++)
        {
            for (var x = xMin; x < xMax; x++)
            {
                var index = y * width + x;
                var pixel = index * 3;
                var red = (int)rgb[pixel];
                var green = (int)rgb[pixel + 1];
                var blue = (int)rgb[pixel + 2];
                mask[index] = red >= 140 && green >= 75 && red - green >= 35 &&
                    green - blue >= 20 && blue <= 110;
            }
        }

        // Connected components are measured from pixels; a bright heading or
        // one random torch must not become permission to click.
        var visited = new bool[mask.Length];
        var queue = new int[mask.Length];
        var blobs = new List<LightBlob>();
        for (var y = yMin; y < yMax; y++)
        {
            for (var x = xMin; x < xMax; x++)
            {
                var start = y * width + x;
                if (!mask[start] || visited[start]) continue;
                var begin = 0;
                var end = 1;
                queue[0] = start;
                visited[start] = true;
                var count = 0;
                long sumX = 0;
                long sumY = 0;
                var left = x;
                var right = x;
                var top = y;
                var bottom = y;
                while (begin < end)
                {
                    var pixel = queue[begin++];
                    var px = pixel % width;
                    var py = pixel / width;
                    count++;
                    sumX += px;
                    sumY += py;
                    left = Math.Min(left, px);
                    right = Math.Max(right, px);
                    top = Math.Min(top, py);
                    bottom = Math.Max(bottom, py);
                    Add(px - 1, py);
                    Add(px + 1, py);
                    Add(px, py - 1);
                    Add(px, py + 1);
                }

                void Add(int nextX, int nextY)
                {
                    if (nextX < xMin || nextX >= xMax || nextY < yMin || nextY >= yMax) return;
                    var i = nextY * width + nextX;
                    if (!mask[i] || visited[i]) return;
                    visited[i] = true;
                    queue[end++] = i;
                }

                if (count >= Math.Max(40, width * height / 2000) &&
                    right - left + 1 < width / 4 && bottom - top + 1 < height / 9)
                    blobs.Add(new LightBlob((float)sumX / count, (float)sumY / count, count));
            }
        }

        var central = blobs
            .Where(blob => blob.X >= width * 0.38f && blob.X <= width * 0.62f &&
                blob.Y >= height * 0.17f && blob.Y <= height * 0.38f)
            .OrderByDescending(blob => blob.Count)
            .ToArray();
        foreach (var focus in central)
        {
            var left = blobs.Any(blob => blob.X < width * 0.25f &&
                Math.Abs(blob.Y - focus.Y) < height * 0.08f);
            var right = blobs.Any(blob => blob.X > width * 0.75f &&
                Math.Abs(blob.Y - focus.Y) < height * 0.08f);
            if (left && right)
                return new VisualEntryProposal(focus.X, focus.Y, 0.65f);
        }

        return null;
    }

    private sealed record LightBlob(float X, float Y, int Count);
}
