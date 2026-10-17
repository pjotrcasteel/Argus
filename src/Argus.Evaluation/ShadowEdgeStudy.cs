using System.Globalization;
using System.Text;
using System.Text.Json;
using Argus.Agents.Visual;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record ShadowThreatCounts(int TruePositive, int FalsePositive, int FalseNegative)
{
    public double? Precision => TruePositive + FalsePositive == 0 ? null : (double)TruePositive / (TruePositive + FalsePositive);
    public double? Recall => TruePositive + FalseNegative == 0 ? null : (double)TruePositive / (TruePositive + FalseNegative);
}

public sealed record ShadowEdgeFrame(int Step, string Status, ShadowThreatCounts? Baseline, ShadowThreatCounts? Expanded,
    IReadOnlyList<VisualCandidatePoint> AdditionalCandidates)
{
    public ShadowThreatCounts? Temporal { get; init; }
    public int TemporallyConfirmed { get; init; }
    public int Pending { get; init; }
    public int Stationary { get; init; }
    public ShadowThreatCounts? PersistentStationaryTrial { get; init; }
    public int PersistentStationaryProposals { get; init; }
}

public sealed record ShadowEdgeStudyReport(string RunId, int ReviewedFrames, int ScoredFrames, int UnscoredFrames,
    float MatchingRadius, ShadowThreatCounts Baseline, ShadowThreatCounts Expanded, IReadOnlyList<ShadowEdgeFrame> Frames)
{
    public ShadowThreatCounts? Temporal { get; init; }
    public ShadowThreatCounts? PersistentStationaryTrial { get; init; }
}

/// <summary>
/// Replays existing screenshot pixels twice against the same human marks: untouched active detector
/// versus a shadow-only extended scan. Never sends actions to a game or treats stored predictions as truth.
/// </summary>
public static class ShadowEdgeStudy
{
    public static async Task<ShadowEdgeStudyReport> CompareAsync(string folder, LabelSet labels, ReplayTrace replay,
        CombatVisualSettings settings, float radius = 26, CancellationToken cancellationToken = default)
    {
        if (!float.IsFinite(radius) || radius is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(radius));
        settings.Validate();
        GroundTruth.Validate(labels, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);

        var entries = new List<ShadowEdgeFrame>();
        var byStep = labels.Frames.ToDictionary(f => f.Step);
        var tracker = new ShadowTemporalCandidateTracker();
        using var reader = new StreamReader(Path.Combine(folder, "trace.jsonl"));
        string? line;
        var step = 0;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var frame = replay.Frames[step];
            using var document = JsonDocument.Parse(line);
            var legacy = TraceEvidenceReliability.IsUnverifiedLegacyFinal(document.RootElement, step, replay.Frames.Count);

            // Observe EVERY sequential screenshot, not just sparse human-labeled frames.
            // A later frame is never used to inform an earlier decision. Reset between phases.
            ShadowThreatObservation? observation = null;
            ShadowTemporalObservation? temporal = null;
            if (frame.Phase == "exploring" && !legacy)
            {
                var pixels = await File.ReadAllBytesAsync(Path.Combine(folder, frame.Screenshot), cancellationToken);
                observation = CombatPixelPerception.ObserveShadow(pixels, settings);
                temporal = tracker.Observe(observation);
            }
            else
            {
                tracker.Reset();
            }

            if (byStep.TryGetValue(step, out var human))
            {
                if (legacy)
                {
                    entries.Add(new ShadowEdgeFrame(step, "LEGACY_FINAL_UNVERIFIED", null, null, []));
                }
                else if (human.Threats.State == "unknown")
                {
                    entries.Add(new ShadowEdgeFrame(step, "UNKNOWN_REFERENCE", null, null, []));
                }
                else if (frame.Phase != "exploring")
                {
                    entries.Add(new ShadowEdgeFrame(step, "NOT_COMBAT_FRAME", null, null, []));
                }
                else if (observation?.Baseline.Player is null)
                {
                    entries.Add(new ShadowEdgeFrame(step, "PLAYER_NOT_LOCATED", null, null, []));
                }
                else
                {
                    var original = Match(human.Threats.Centers!, observation.Baseline.ThreatCandidates, radius);
                    var expanded = Match(human.Threats.Centers!, observation.CombinedCandidates, radius);
                    var temporalCandidates = observation.Baseline.ThreatCandidates.Concat(temporal!.Confirmed).ToArray();
                    var stable = Match(human.Threats.Centers!, temporalCandidates, radius);
                    var persistentCandidates = temporalCandidates.Concat(temporal.PersistentStationaryCandidates).ToArray();
                    var stationaryTrial = Match(human.Threats.Centers!, persistentCandidates, radius);
                    var additions = observation.AddedCandidates.Select(p => new VisualCandidatePoint(p.X, p.Y)).ToArray();
                    entries.Add(new ShadowEdgeFrame(step, "SCORED", original, expanded, additions)
                    {
                        Temporal = stable,
                        TemporallyConfirmed = temporal.Confirmed.Count,
                        Pending = temporal.PendingCandidates,
                        Stationary = temporal.StationaryCandidates,
                        PersistentStationaryTrial = stationaryTrial,
                        PersistentStationaryProposals = temporal.PersistentStationaryCandidates.Count
                    });
                }
            }
            step++;
        }

        var scored = entries.Where(f => f.Status == "SCORED").ToArray();
        return new ShadowEdgeStudyReport(labels.RunId, labels.Frames.Count, scored.Length, entries.Count - scored.Length,
            radius, Sum(scored.Select(f => f.Baseline!)), Sum(scored.Select(f => f.Expanded!)), entries)
        {
            Temporal = Sum(scored.Select(f => f.Temporal!)),
            PersistentStationaryTrial = Sum(scored.Select(f => f.PersistentStationaryTrial!))
        };
    }

    public static string RenderMarkdown(ShadowEdgeStudyReport report)
    {
        static string Pct(double? rate) => rate is null ? "N/A" :
            rate.Value.ToString("P1", CultureInfo.InvariantCulture);
        var b = new StringBuilder();
        b.AppendLine("# ARGUS — M2.5e stationary-candidate audit (shadow only)").AppendLine()
            .AppendLine($"Run: `{report.RunId.Replace("`", "")}`. Human-labeled frames: {report.ReviewedFrames}; " +
                $"scored: {report.ScoredFrames}; unscored: {report.UnscoredFrames}.")
            .AppendLine("All variants use the same current PNG; temporal association uses only preceding " +
                "contiguous combat screenshots, not human labels or later frames. Radius: " +
                $"{report.MatchingRadius.ToString("0.#", CultureInfo.InvariantCulture)} px.")
            .AppendLine("This is **development** evidence, not held-out accuracy, gameplay improvement, or proof of enemy identity.")
            .AppendLine()
            .AppendLine("| Detector | TP | FP | FN | Precision | Recall |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        AddCounts("Current baseline", report.Baseline);
        AddCounts("Extended shadow (raw)", report.Expanded);
        if (report.Temporal is not null) AddCounts("Extended shadow (moving temporal)", report.Temporal);
        if (report.PersistentStationaryTrial is not null)
            AddCounts("Experimental: temporal + persistent still", report.PersistentStationaryTrial);
        b.AppendLine().AppendLine("| Frame | Status | Baseline TP/FP/FN | Raw TP/FP/FN | Moving TP/FP/FN | Still trial TP/FP/FN | Added | Moving | Pending | Static | Stable still |")
            .AppendLine("| ---: | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var f in report.Frames)
            b.AppendLine($"| {f.Step} | {f.Status} | {Counts(f.Baseline)} | {Counts(f.Expanded)} | " +
                $"{Counts(f.Temporal)} | {Counts(f.PersistentStationaryTrial)} | {f.AdditionalCandidates.Count} | " +
                $"{f.TemporallyConfirmed} | {f.Pending} | {f.Stationary} | {f.PersistentStationaryProposals} |");
        b.AppendLine()
            .AppendLine("**Safety:** this probe does not affect `VisualGameAgent` or avoidance. New candidates are " +
                "uncertain palette matches, never verified enemies. Old bounded final-frame evidence and " +
                "non-combat/unknown references are excluded. The temporal tracker additionally requires " +
                "two consecutive sightings and at least three pixels of observed motion; it can miss " +
                "stationary or fast threats, and moving decorations can still appear as candidates. " +
                "The fourth experimental column separately restores visually stable still objects after " +
                "three consecutive sightings; static scenery may qualify and it must NEVER be treated " +
                "as confirmed enemy identity or affect player input. " +
                "The shadow scan deliberately avoids the top 11% " +
                "and bottom 7% to reduce HUD/footer contamination; other false positives remain possible.")
            .AppendLine("Do not promote this detector until new independent human-reviewed development runs confirm " +
                "whether extra true positives outweigh extra false positives, followed by a fresh untouched held-out run.");
        return b.ToString();

        void AddCounts(string label, ShadowThreatCounts c) =>
            b.AppendLine($"| {label} | {c.TruePositive} | {c.FalsePositive} | {c.FalseNegative} | " +
                $"{Pct(c.Precision)} | {Pct(c.Recall)} |");
        static string Counts(ShadowThreatCounts? c) => c is null ? "N/A" :
            $"{c.TruePositive}/{c.FalsePositive}/{c.FalseNegative}";
    }

    private static ShadowThreatCounts Match(IReadOnlyList<VisualCandidatePoint> human, IReadOnlyList<VisualBlob> detected,
        float radius)
    {
        var predicted = detected.Select(p => new VisualCandidatePoint(p.X, p.Y)).ToArray();
        var result = ThreatMatching.Match(human, predicted, radius);
        return new ShadowThreatCounts(result.TruePositives, result.FalsePositives.Count, result.FalseNegatives.Count);
    }

    private static ShadowThreatCounts Sum(IEnumerable<ShadowThreatCounts> items) =>
        items.Aggregate(new ShadowThreatCounts(0, 0, 0),
            (total, next) => new ShadowThreatCounts(total.TruePositive + next.TruePositive,
                total.FalsePositive + next.FalsePositive, total.FalseNegative + next.FalseNegative));
}
