using System.Globalization;
using System.Text;
using System.Text.Json;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record FrameDetectionFinding(int Step, string Phase, string PlayerStatus, string ThreatStatus,
    int ThreatTruePositives, int ThreatFalsePositives, int ThreatFalseNegatives,
    IReadOnlyList<VisualCandidatePoint> MissedThreatCenters, IReadOnlyList<VisualCandidatePoint> FalsePositiveCenters,
    int MissedOutsideLegacySearchArea);

public sealed record PerceptionFailureReport(string RunId, float MatchingRadiusPixels,
    IReadOnlyList<FrameDetectionFinding> Frames, int ScoredThreatFrames, int LegacyFinalUnverifiedFrames,
    int ThreatTruePositives, int ThreatFalsePositives, int ThreatFalseNegatives, int MissedOutsideLegacySearchArea);

/// <summary>Read-only diagnostics against human-reviewed pixels: no game state, new predictions or fabricated labels.</summary>
public static class PerceptionFailureAnalysis
{
    public static async Task<PerceptionFailureReport> AnalyzeAsync(string folder, LabelSet labels, ReplayTrace replay,
        float radius = 26, CancellationToken cancellationToken = default)
    {
        if (!float.IsFinite(radius) || radius is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(radius));
        GroundTruth.Validate(labels, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);
        var selected = labels.Frames.ToDictionary(f => f.Step);
        var findings = new List<FrameDetectionFinding>();
        using var reader = new StreamReader(Path.Combine(folder, "trace.jsonl"));
        string? line;
        var step = 0;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0) continue;
            if (selected.TryGetValue(step, out var label))
            {
                using var json = JsonDocument.Parse(line);
                var frame = replay.Frames[step];
                var legacy = TraceEvidenceReliability.IsUnverifiedLegacyFinal(json.RootElement, step, replay.Frames.Count);
                findings.Add(AnalyzeFrame(frame, label, radius, legacy));
            }
            step++;
        }

        var measured = findings.Where(f => f.ThreatStatus == "SCORED").ToArray();
        return new PerceptionFailureReport(labels.RunId, radius, findings,
            measured.Length, findings.Count(f => f.ThreatStatus == "LEGACY_FINAL_UNVERIFIED"),
            measured.Sum(f => f.ThreatTruePositives), measured.Sum(f => f.ThreatFalsePositives),
            measured.Sum(f => f.ThreatFalseNegatives), measured.Sum(f => f.MissedOutsideLegacySearchArea));
    }

    public static string RenderMarkdown(PerceptionFailureReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# ARGUS — human-reviewed development perception failures").AppendLine()
            .AppendLine($"> Run `{Safe(report.RunId)}`, matching radius {report.MatchingRadiusPixels.ToString("0.#", CultureInfo.InvariantCulture)} px. " +
                "Only already-recorded predictions are compared; this is **not** an evaluation of a newer detector.")
            .AppendLine()
            .AppendLine($"Threat frames scored: **{report.ScoredThreatFrames}**; legacy final frames excluded: " +
                $"**{report.LegacyFinalUnverifiedFrames}**. TP/FP/FN: **{report.ThreatTruePositives}/{report.ThreatFalsePositives}/{report.ThreatFalseNegatives}**.")
            .AppendLine($"Missed annotated threat centers outside the old detector's geometric scan area: " +
                $"**{report.MissedOutsideLegacySearchArea}** (diagnostic association, not proof of cause).")
            .AppendLine()
            .AppendLine("| Frame | Agent phase | Player | Threat evidence | TP | FP | FN | Outside old ROI |")
            .AppendLine("| ---: | --- | --- | --- | ---: | ---: | ---: | ---: |");
        foreach (var f in report.Frames)
            b.AppendLine($"| {f.Step} | {Safe(f.Phase)} | {f.PlayerStatus} | {f.ThreatStatus} | " +
                $"{f.ThreatTruePositives} | {f.ThreatFalsePositives} | {f.ThreatFalseNegatives} | {f.MissedOutsideLegacySearchArea} |");

        b.AppendLine().AppendLine("## Review individual errors");
        foreach (var f in report.Frames.Where(f => f.ThreatStatus == "SCORED" &&
            (f.MissedThreatCenters.Count > 0 || f.FalsePositiveCenters.Count > 0)))
        {
            b.AppendLine($"- Frame {f.Step}: missed human centers {Centers(f.MissedThreatCenters)}; " +
                $"unmatched detector centers {Centers(f.FalsePositiveCenters)}");
        }
        b.AppendLine().AppendLine("**Measurement boundaries:** menu and transition frames without combat perception are NOT_OBSERVED; " +
            "all-unknown human labels are UNKNOWN_REFERENCE; missing candidate coordinates are NO_PREDICTION_EVIDENCE; " +
            "legacy last-step exploring frames without a fresh-observation marker are LEGACY_FINAL_UNVERIFIED. " +
            "None are treated as zero enemies or failed recognitions. This development sample must not be presented as held-out quality.");
        return b.ToString();
    }

    private static FrameDetectionFinding AnalyzeFrame(ReplayFrame frame, FrameLabel label, float radius, bool legacy)
    {
        var attempted = frame.Phase == "exploring" || frame.PlayerX is not null;
        var playerStatus = PlayerStatus(frame, label, radius, legacy, attempted);
        var threatStatus = legacy ? "LEGACY_FINAL_UNVERIFIED"
            : label.Threats.State == "unknown" ? "UNKNOWN_REFERENCE"
            : !attempted ? "NOT_OBSERVED"
            : frame.ThreatLocations is null ? "NO_PREDICTION_EVIDENCE" : "SCORED";
        if (threatStatus != "SCORED")
            return new FrameDetectionFinding(frame.Step, frame.Phase, playerStatus, threatStatus, 0, 0, 0, [], [], 0);

        var pairs = ThreatMatching.Match(label.Threats.Centers!, frame.ThreatLocations!, radius);
        var outside = pairs.FalseNegatives.Count(p => IsOutsideLegacySearch(p, frame.Width, frame.Height));
        return new FrameDetectionFinding(frame.Step, frame.Phase, playerStatus, threatStatus,
            pairs.TruePositives, pairs.FalsePositives.Count, pairs.FalseNegatives.Count,
            pairs.FalseNegatives, pairs.FalsePositives, outside);
    }

    private static string PlayerStatus(ReplayFrame frame, FrameLabel label, float radius, bool legacy, bool attempted)
    {
        if (legacy) return "LEGACY_FINAL_UNVERIFIED";
        if (label.Player.State == "unknown") return "UNKNOWN_REFERENCE";
        if (!attempted) return "NOT_OBSERVED";
        if (label.Player.State == "absent") return frame.PlayerX is null ? "ABSENT_MATCH" : "FALSE_POSITIVE";
        if (frame.PlayerX is null || frame.PlayerY is null) return "MISSED";
        var dx = frame.PlayerX.Value - label.Player.Center!.X;
        var dy = frame.PlayerY.Value - label.Player.Center.Y;
        return MathF.Sqrt(dx * dx + dy * dy) <= radius ? "MATCH" : "MISMATCH";
    }

    private static bool IsOutsideLegacySearch(VisualCandidatePoint point, int width, int height) =>
        point.X < width / 30 || point.X >= width * 29 / 30 ||
        point.Y < height * 19 / 100 || point.Y >= height * 82 / 100;

    private static string Centers(IReadOnlyList<VisualCandidatePoint> points) => points.Count == 0 ? "none" :
        string.Join(", ", points.Select(p => $"({p.X.ToString("0.#", CultureInfo.InvariantCulture)}," +
            $"{p.Y.ToString("0.#", CultureInfo.InvariantCulture)})"));
    private static string Safe(string value) => value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Replace("`", "");
}
