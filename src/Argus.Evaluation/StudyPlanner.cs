using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

/// <summary>A stable, no-leakage plan: all screenshots from one run belong to one partition.</summary>
public sealed record StudyRun(string RunId, ReplayTrace Trace);
public sealed record StudyAssignment(string RunId, string Partition, IReadOnlyList<int> FrameSteps);
public sealed record StudyPlan(int SchemaVersion, string Seed, int RequestedFramesPerRun, IReadOnlyList<StudyAssignment> Assignments);

public static class StudyPlanner
{
    private const int MaximumRuns = 128;
    private const int MaximumFramesPerRun = 64;

    public static StudyPlan Create(IEnumerable<StudyRun> input, string seed, int framesPerRun = 12)
    {
        if (string.IsNullOrWhiteSpace(seed) || seed.Length > 128)
            throw new ArgumentException("Provide a short, nonempty study seed.", nameof(seed));
        if (framesPerRun is < 1 or > MaximumFramesPerRun)
            throw new ArgumentOutOfRangeException(nameof(framesPerRun));

        var runs = input.OrderBy(r => r.RunId, StringComparer.Ordinal).ToArray();
        if (runs.Length is < 2 or > MaximumRuns)
            throw new InvalidDataException("A study needs 2–128 independently recorded sessions.");
        if (runs.GroupBy(r => r.RunId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1))
            throw new InvalidDataException("Duplicate run IDs are not independent experiments.");

        foreach (var run in runs)
        {
            if (!ValidRunId(run.RunId) || run.Trace is null || run.Trace.Frames.Count == 0 ||
                run.Trace.Frames.Where((frame, index) => frame.Step != index).Any())
                throw new InvalidDataException("Invalid run ID or unsorted/empty screenshot evidence.");
        }

        // Split by *entire run*, never by neighboring frames. No candidate positions, outcomes,
        // agent success flags or manual labels influence selection.
        var ranked = runs.OrderBy(r => Hash(seed, r.RunId, "partition"))
            .ThenBy(r => r.RunId, StringComparer.Ordinal).ToArray();
        var heldOutCount = Math.Clamp((int)Math.Ceiling(runs.Length * 0.3), 1, runs.Length - 1);
        var heldOut = ranked.Take(heldOutCount).Select(r => r.RunId).ToHashSet(StringComparer.Ordinal);

        var assignments = runs.Select(run => new StudyAssignment(run.RunId,
            heldOut.Contains(run.RunId) ? "held-out" : "development",
            SampleSteps(run.Trace, seed, run.RunId, framesPerRun))).ToArray();
        return new StudyPlan(1, seed, framesPerRun, assignments);
    }

    public static LabelSet Template(StudyAssignment assignment, ReplayTrace trace)
    {
        if (!ValidRunId(assignment.RunId) || assignment.Partition is not ("held-out" or "development") ||
            assignment.FrameSteps.Count == 0 || assignment.FrameSteps.Count > MaximumFramesPerRun ||
            assignment.FrameSteps.Distinct().Count() != assignment.FrameSteps.Count ||
            assignment.FrameSteps.Any(step => step < 0 || step >= trace.Frames.Count))
            throw new InvalidDataException("Study assignment is invalid for this run.");

        var labels = assignment.FrameSteps.Order().Select(step =>
            new FrameLabel(step, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))).ToArray();
        return new LabelSet(1, assignment.RunId, labels);
    }

    private static int[] SampleSteps(ReplayTrace trace, string seed, string runId, int requested)
    {
        var total = trace.Frames.Count;
        var count = Math.Min(total, requested);
        var chosen = new int[count];

        // One seeded draw from each nonoverlapping temporal bin. No frames are handpicked
        // based on agent predictions, combat confidence, favorable outcomes or labels.
        for (var i = 0; i < count; i++)
        {
            var start = i * total / count;
            var end = (i + 1) * total / count;
            var value = Hash(seed, runId, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            chosen[i] = start + (int)(value % (uint)(end - start));
        }

        return chosen;
    }

    private static uint Hash(string seed, string runId, string component)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed + "\n" + runId + "\n" + component));
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4));
    }

    private static bool ValidRunId(string? id) =>
        id is { Length: > 0 and <= 64 } && id is not "." and not ".." &&
        char.IsLetterOrDigit(id[0]) && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
