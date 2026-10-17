using System.Text.Json;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

/// <summary>Explicit human-visible screen labels. "unknown" means unjudgeable, NOT an absent object.</summary>
public sealed record PlayerLabel(string State, VisualCandidatePoint? Center);
public sealed record ThreatLabel(string State, IReadOnlyList<VisualCandidatePoint>? Centers);
public sealed record FrameLabel(int Step, PlayerLabel Player, ThreatLabel Threats);
public sealed record LabelSet(int SchemaVersion, string RunId, IReadOnlyList<FrameLabel> Frames);

public static class GroundTruth
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<LabelSet> ReadAsync(string path, string runFolder, ReplayTrace trace, CancellationToken token = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 2_000_000) throw new InvalidDataException("Missing or oversized annotation file.");
        var labels = JsonSerializer.Deserialize<LabelSet>(await File.ReadAllTextAsync(path, token), Json)
            ?? throw new InvalidDataException("Invalid annotation document.");
        Validate(labels, Path.GetFileName(Path.GetFullPath(runFolder).TrimEnd(Path.DirectorySeparatorChar)), trace);
        return labels;
    }

    public static LabelSet CreateTemplate(string runFolder, ReplayTrace trace, int sampleCount = 16)
    {
        if (sampleCount is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        var candidate = trace.Frames.Where(f => f.Phase == "exploring").ToArray();
        if (candidate.Length == 0) candidate = trace.Frames.ToArray();

        var count = Math.Min(sampleCount, candidate.Length);
        var selected = new List<FrameLabel>();
        for (var i = 0; i < count; i++)
        {
            var slot = count == 1 ? 0 : (int)Math.Round(i * (candidate.Length - 1d) / (count - 1));
            selected.Add(new FrameLabel(candidate[slot].Step, new PlayerLabel("unknown", null),
                new ThreatLabel("unknown", null)));
        }

        return new LabelSet(1, Path.GetFileName(Path.GetFullPath(runFolder).TrimEnd(Path.DirectorySeparatorChar)), selected);
    }

    public static void Validate(LabelSet labels, string runId, ReplayTrace trace)
    {
        if (labels.SchemaVersion != 1 || !string.Equals(labels.RunId, runId, StringComparison.Ordinal) ||
            labels.Frames is null || labels.Frames.Count > 2000)
            throw new InvalidDataException("Unsupported annotation schema, mismatched run ID or excessive frames.");

        var seen = new HashSet<int>();
        foreach (var frame in labels.Frames)
        {
            if (frame is null || !seen.Add(frame.Step) || frame.Step < 0 || frame.Step >= trace.Frames.Count ||
                frame.Player is null || frame.Threats is null)
                throw new InvalidDataException("Duplicate, out-of-range or incomplete frame annotation.");

            var size = trace.Frames[frame.Step];
            switch (frame.Player.State)
            {
                case "visible":
                    if (!IsValid(frame.Player.Center, size)) throw new InvalidDataException("Visible player requires a valid pixel center.");
                    break;
                case "absent":
                case "unknown":
                    if (frame.Player.Center is not null) throw new InvalidDataException("Absent/unknown player cannot have a center.");
                    break;
                default:
                    throw new InvalidDataException("Player state must be visible, absent or unknown.");
            }

            switch (frame.Threats.State)
            {
                case "known":
                    if (frame.Threats.Centers is null || frame.Threats.Centers.Count > 100 ||
                        frame.Threats.Centers.Any(p => !IsValid(p, size)))
                        throw new InvalidDataException("Known threat labeling requires a valid (possibly empty) center list.");
                    break;
                case "unknown":
                    if (frame.Threats.Centers is not null)
                        throw new InvalidDataException("Unknown threats must not be interpreted as an empty list.");
                    break;
                default:
                    throw new InvalidDataException("Threat state must be known or unknown.");
            }
        }
    }

    private static bool IsValid(VisualCandidatePoint? point, ReplayFrame size) =>
        point is not null && float.IsFinite(point.X) && float.IsFinite(point.Y) &&
        point.X >= 0 && point.X < size.Width && point.Y >= 0 && point.Y < size.Height;
}
