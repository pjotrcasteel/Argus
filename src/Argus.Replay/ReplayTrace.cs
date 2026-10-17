using System.Text.Json;
using Argus.Core;

namespace Argus.Replay;

/// <summary>Read-only presentation of evidence, not a statement about true game state.</summary>
public sealed record ReplayFrame(
    int Step, string Screenshot, DateTimeOffset TimestampUtc, int Width, int Height, string Phase, string Reason,
    float? Confidence, string? Action, float? PlayerX, float? PlayerY, int? ThreatCandidates)
{
    public IReadOnlyList<VisualCandidatePoint>? ThreatLocations { get; init; }
}

public sealed record ReplayTrace(IReadOnlyList<ReplayFrame> Frames);

public static class ReplayTraceReader
{
    private const int MaximumFrames = 2_000;
    private const long MaximumTraceBytes = 32L * 1024 * 1024;
    private const long MaximumImageBytes = 12L * 1024 * 1024;
    private const long MaximumTotalImages = 350L * 1024 * 1024;
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

    public static async Task<ReplayTrace> ReadAsync(string folder, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(folder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var tracePath = Path.Combine(root, "trace.jsonl");
        var traceInfo = new FileInfo(tracePath);
        if (!traceInfo.Exists) throw new FileNotFoundException("No ARGUS trace.jsonl was found.", tracePath);
        if (traceInfo.Length > MaximumTraceBytes) throw new InvalidDataException("ARGUS trace exceeds the local replay size limit.");

        var frames = new List<ReplayFrame>();
        long totalImageBytes = 0;
        await using var stream = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;
            if (frames.Count == MaximumFrames) throw new InvalidDataException("ARGUS replay has too many frames.");

            using var document = JsonDocument.Parse(line);
            var frame = Parse(document.RootElement, frames.Count);
            var imagePath = Path.Combine(root, frame.Screenshot);
            var imageInfo = new FileInfo(imagePath);
            if (!imageInfo.Exists || imageInfo.Length < PngHeader.Length || imageInfo.Length > MaximumImageBytes)
                throw new InvalidDataException($"Invalid or missing screenshot at step {frame.Step}.");
            totalImageBytes += imageInfo.Length;
            if (totalImageBytes > MaximumTotalImages) throw new InvalidDataException("Replay screenshots exceed the local size limit.");

            await using (var image = imageInfo.OpenRead())
            {
                var bytes = new byte[PngHeader.Length];
                await image.ReadExactlyAsync(bytes, cancellationToken);
                if (!bytes.SequenceEqual(PngHeader))
                    throw new InvalidDataException($"Screenshot at step {frame.Step} is not a PNG.");
            }
            frames.Add(frame);
        }

        if (frames.Count == 0) throw new InvalidDataException("Replay contains no screen observations.");
        return new ReplayTrace(frames);
    }

    private static ReplayFrame Parse(JsonElement item, int index)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("step", out var stepElement) ||
            !stepElement.TryGetInt32(out var step) || step != index)
            throw new InvalidDataException($"Replay steps must be contiguous, starting at zero; expected {index}.");

        var expected = $"frame-{step:D4}.png";
        var name = RequiredString(item, "screenshot");
        if (!string.Equals(name, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Screenshot name at step {step} is invalid; expected {expected}.");

        if (!item.TryGetProperty("timestampUtc", out var time) || !time.TryGetDateTimeOffset(out var timestamp))
            throw new InvalidDataException($"Missing/invalid screenshot timestamp at step {step}.");

        var width = RequiredPositiveInt(item, "width");
        var height = RequiredPositiveInt(item, "height");
        if (width > 3840 || height > 2160)
            throw new InvalidDataException("Screenshot dimensions exceed replay limits.");

        var phase = "unknown";
        var reason = "No decision evidence was recorded for this observation.";
        float? confidence = null;
        float? playerX = null;
        float? playerY = null;
        int? candidates = null;
        IReadOnlyList<VisualCandidatePoint>? locations = null;

        if (item.TryGetProperty("evidence", out var evidence) && evidence.ValueKind == JsonValueKind.Object)
        {
            phase = OptionalString(evidence, "phase") ?? "unknown";
            reason = OptionalString(evidence, "reason") ?? reason;
            confidence = OptionalFloat(evidence, "confidence");
            if (evidence.TryGetProperty("combat", out var combat) && combat.ValueKind == JsonValueKind.Object)
            {
                var x = OptionalFloat(combat, "playerX");
                var y = OptionalFloat(combat, "playerY");
                if (x >= 0 && x <= width && y >= 0 && y <= height)
                {
                    playerX = x;
                    playerY = y;
                }
                if (combat.TryGetProperty("threatCandidates", out var count) && count.TryGetInt32(out var n) && n >= 0)
                    candidates = n;
                if (combat.TryGetProperty("threatLocations", out var list))
                {
                    if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 100)
                        throw new InvalidDataException("Invalid threat candidate list.");
                    var recorded = new List<VisualCandidatePoint>();
                    foreach (var position in list.EnumerateArray())
                    {
                        if (position.ValueKind != JsonValueKind.Object ||
                            !position.TryGetProperty("x", out var x) || !x.TryGetSingle(out var px) ||
                            !position.TryGetProperty("y", out var y) || !y.TryGetSingle(out var py) ||
                            !float.IsFinite(px) || !float.IsFinite(py) ||
                            px < 0 || px >= width || py < 0 || py >= height)
                            throw new InvalidDataException("Invalid threat candidate pixel position.");
                        recorded.Add(new VisualCandidatePoint(px, py));
                    }
                    locations = recorded;
                }
            }
        }

        string? action = null;
        if (item.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            var kind = OptionalString(a, "kind") ?? "unknown";
            var key = OptionalString(a, "key");
            action = key is null ? kind : kind + " (" + key + ")";
        }

        return new ReplayFrame(step, name, timestamp, width, height, phase, reason, confidence, action,
            playerX, playerY, candidates) { ThreatLocations = locations };
    }

    private static string RequiredString(JsonElement element, string name) =>
        OptionalString(element, name) ?? throw new InvalidDataException($"Missing required replay field '{name}'.");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int RequiredPositiveInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number) || number < 1)
            throw new InvalidDataException($"Invalid replay field '{name}'.");
        return number;
    }

    private static float? OptionalFloat(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetSingle(out var number) && float.IsFinite(number) ? number : null;
}
