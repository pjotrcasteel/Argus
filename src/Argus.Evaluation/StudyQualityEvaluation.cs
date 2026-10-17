using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record StudyRunQuality(string RunId, string Partition, int SelectedFrames, string Status,
    int HumanReviewedPlayerFrames, int HumanReviewedThreatFrames, PerceptionReport? Perception);
public sealed record StudyRateDistribution(int EligibleRuns, double? Median, double? Minimum, double? Maximum);
public sealed record StudyPartitionQuality(string Partition, int Runs, int RunsWithHumanReview, int SelectedFrames,
    int HumanReviewedPlayerFrames, int HumanReviewedThreatFrames, StudyRateDistribution PlayerPrecision,
    StudyRateDistribution PlayerRecall, StudyRateDistribution ThreatPrecision, StudyRateDistribution ThreatRecall);
public sealed record StudyQualityReport(int SchemaVersion, string Seed, float MatchingRadiusPixels, string Status,
    IReadOnlyList<StudyRunQuality> Runs, IReadOnlyList<StudyPartitionQuality> Partitions);

/// <summary>
/// Evaluates only independently supplied human labels from a frozen, reproducible run-level sample plan.
/// Missing/unreviewed data stays NOT_EVALUATED, never synthetic truth or an accidental 0% score.
/// </summary>
public static class StudyQualityEvaluation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<StudyQualityReport> EvaluateAsync(string root, float radius = 26,
        CancellationToken cancellationToken = default)
    {
        if (!float.IsFinite(radius) || radius is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var folder = Path.GetFullPath(root);
        if (!Directory.Exists(folder) || IsLink(new DirectoryInfo(folder)))
            throw new InvalidDataException("Study folder must be an existing ordinary directory.");

        var planFile = Path.Combine(folder, "study-plan.json");
        var planInfo = new FileInfo(planFile);
        if (!planInfo.Exists || planInfo.Length is <= 0 or > 1_000_000 || IsLink(planInfo))
            throw new InvalidDataException("Missing, linked or oversized study-plan.json.");

        var plan = JsonSerializer.Deserialize<StudyPlan>(await File.ReadAllTextAsync(planFile, cancellationToken), Json)
            ?? throw new InvalidDataException("Invalid study-plan.json.");
        if (plan.SchemaVersion != 1 || plan.Assignments is null || plan.Assignments.Count is < 2 or > 128)
            throw new InvalidDataException("Invalid study schema or number of recorded sessions.");

        var runs = new List<StudyRun>();
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in plan.Assignments)
        {
            if (string.IsNullOrEmpty(assignment.RunId) || assignment.RunId.Length > 64 ||
                assignment.RunId is "." or ".." || assignment.RunId.Any(c =>
                    !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) ||
                !char.IsAsciiLetterOrDigit(assignment.RunId[0]))
                throw new InvalidDataException("Unsafe run ID in study plan.");

            var runFolder = Path.Combine(folder, assignment.RunId);
            var dir = new DirectoryInfo(runFolder);
            if (!dir.Exists || IsLink(dir))
                throw new InvalidDataException("Missing or linked study run: " + assignment.RunId);

            var traceInfo = new FileInfo(Path.Combine(runFolder, "trace.jsonl"));
            if (!traceInfo.Exists || IsLink(traceInfo))
                throw new InvalidDataException("Missing or linked screenshot trace.");
            var trace = await ReplayTraceReader.ReadAsync(runFolder, cancellationToken);
            await using var stream = traceInfo.OpenRead();
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!hashes.Add(digest))
                throw new InvalidDataException("Duplicate recorded traces must not count as independent runs.");
            runs.Add(new StudyRun(assignment.RunId, trace));
        }

        // Recompute the complete original assignment from the raw traces. An edited study-plan cannot
        // silently cherry-pick only easy-to-detect screenshots or reassign sessions across the holdout.
        var expected = StudyPlanner.Create(runs, plan.Seed, plan.RequestedFramesPerRun);
        if (plan.Assignments.Count != expected.Assignments.Count ||
            !plan.Assignments.Zip(expected.Assignments).All(pair =>
                pair.First.RunId == pair.Second.RunId &&
                pair.First.Partition == pair.Second.Partition &&
                pair.First.FrameSteps.SequenceEqual(pair.Second.FrameSteps)))
            throw new InvalidDataException("Study plan differs from deterministic preselected run/frame assignments.");

        var byId = runs.ToDictionary(run => run.RunId, StringComparer.Ordinal);
        var results = new List<StudyRunQuality>();
        foreach (var assignment in expected.Assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trace = byId[assignment.RunId].Trace;
            var runFolder = Path.Combine(folder, assignment.RunId);
            var labelPath = Path.Combine(runFolder, "labels.json");
            var labelFile = new FileInfo(labelPath);
            if (!labelFile.Exists)
            {
                results.Add(new StudyRunQuality(assignment.RunId, assignment.Partition,
                    assignment.FrameSteps.Count, "NOT_EVALUATED", 0, 0, null));
                continue;
            }
            if (IsLink(labelFile)) throw new InvalidDataException("Linked label files are not accepted.");

            var labels = await GroundTruth.ReadAsync(labelPath, runFolder, trace, cancellationToken);
            // A manually labeled subset may contain unknown object states, but may not omit, add,
            // or replace any *preselected frame*. Prevents cherry-picking already favorable detections.
            if (labels.Frames.Count != assignment.FrameSteps.Count ||
                !labels.Frames.Select(f => f.Step).Order().SequenceEqual(assignment.FrameSteps.Order()))
                throw new InvalidDataException("Human annotations must retain every preselected frame and no others.");

            var playerReviewed = labels.Frames.Count(f => f.Player.State != "unknown");
            var threatReviewed = labels.Frames.Count(f => f.Threats.State != "unknown");
            if (playerReviewed + threatReviewed == 0)
            {
                results.Add(new StudyRunQuality(assignment.RunId, assignment.Partition,
                    assignment.FrameSteps.Count, "NOT_EVALUATED", 0, 0, null));
                continue;
            }

            var score = await PerceptionScoring.ScoreAsync(runFolder, labels, trace, radius, cancellationToken);
            var status = playerReviewed == assignment.FrameSteps.Count && threatReviewed == assignment.FrameSteps.Count
                ? "HUMAN_LABELS_PRESENT" : "PARTIALLY_REVIEWED";
            results.Add(new StudyRunQuality(assignment.RunId, assignment.Partition,
                assignment.FrameSteps.Count, status, playerReviewed, threatReviewed, score));
        }

        var partitions = new[] { "development", "held-out" }.Select(partition =>
        {
            var part = results.Where(r => r.Partition == partition).ToArray();
            return new StudyPartitionQuality(partition, part.Length,
                part.Count(r => r.Perception is not null),
                part.Sum(r => r.SelectedFrames), part.Sum(r => r.HumanReviewedPlayerFrames),
                part.Sum(r => r.HumanReviewedThreatFrames),
                Distribution(part.Select(r => r.Perception?.Player.Precision)),
                Distribution(part.Select(r => r.Perception?.Player.Recall)),
                Distribution(part.Select(r => r.Perception?.Threats.Precision)),
                Distribution(part.Select(r => r.Perception?.Threats.Recall)));
        }).ToArray();

        var overallStatus = results.All(r => r.Perception is null) ? "NOT_EVALUATED"
            : results.All(r => r.Status == "HUMAN_LABELS_PRESENT") ? "HUMAN_LABELS_PRESENT"
            : "PARTIALLY_REVIEWED";
        return new StudyQualityReport(1, expected.Seed, radius, overallStatus, results, partitions);
    }

    public static string RenderMarkdown(StudyQualityReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# ARGUS — blinded run-level visual reference quality").AppendLine()
            .AppendLine($"> Status: **{report.Status}**. Fixed sample seed: `{report.Seed.Replace("`", "")}`. " +
                $"Radius: {report.MatchingRadiusPixels.ToString("0.#", CultureInfo.InvariantCulture)} px.")
            .AppendLine("> Labels must come from independent human review of original pixels; this tool cannot verify who annotated a file.")
            .AppendLine()
            .AppendLine("| Run | Partition | Selected | Player reviewed | Threat reviewed | Status | Player precision | Threat precision | Threat evidence unavailable |")
            .AppendLine("| --- | --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |");
        foreach (var run in report.Runs)
            b.AppendLine($"| {Safe(run.RunId)} | {run.Partition} | {run.SelectedFrames} | " +
                $"{run.HumanReviewedPlayerFrames} | {run.HumanReviewedThreatFrames} | {run.Status} | " +
                $"{Pct(run.Perception?.Player.Precision)} | {Pct(run.Perception?.Threats.Precision)} | " +
                $"{(run.Perception is null ? "N/A" : run.Perception.ThreatEvidenceUnavailableFrames.ToString(CultureInfo.InvariantCulture))} |");

        b.AppendLine().AppendLine("## Partition summaries — unweighted per-run metrics").AppendLine()
            .AppendLine("| Partition | Runs reviewed | Player frames | Threat frames | Median player precision | Median player recall | Median threat precision | Median threat recall |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var group in report.Partitions)
            b.AppendLine($"| {group.Partition} | {group.RunsWithHumanReview}/{group.Runs} | " +
                $"{group.HumanReviewedPlayerFrames}/{group.SelectedFrames} | " +
                $"{group.HumanReviewedThreatFrames}/{group.SelectedFrames} | " +
                $"{Pct(group.PlayerPrecision.Median)} | {Pct(group.PlayerRecall.Median)} | " +
                $"{Pct(group.ThreatPrecision.Median)} | {Pct(group.ThreatRecall.Median)} |");

        b.AppendLine()
            .AppendLine("**NOT_EVALUATED** means no reviewed labels were supplied; it never means 0% accuracy. " +
                "Partial labels, unknown object states and missing candidate coordinates remain explicit.")
            .AppendLine("Runs are separate units of comparison; adjacent frames within one run are correlated. " +
                "No game victories, player realism or learning claims follow from screen-space precision/recall.")
            .AppendLine("Do not revise the detector after viewing held-out quality and then present the same held-out runs as an untouched test.");
        return b.ToString();
    }

    private static StudyRateDistribution Distribution(IEnumerable<double?> rates)
    {
        var samples = rates.Where(value => value.HasValue).Select(value => value!.Value).Order().ToArray();
        if (samples.Length == 0) return new StudyRateDistribution(0, null, null, null);
        var middle = samples.Length / 2;
        var median = samples.Length % 2 == 1 ? samples[middle] : (samples[middle - 1] + samples[middle]) / 2;
        return new StudyRateDistribution(samples.Length, median, samples[0], samples[^1]);
    }

    private static string Pct(double? number) => number.HasValue
        ? (number.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A";
    private static string Safe(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    private static bool IsLink(FileSystemInfo info) => info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
        info.LinkTarget is not null;
}
