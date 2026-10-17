using System.Globalization;
using System.Text;
using System.Text.Json;
using Argus.Agents.Visual;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record AppearanceAuditCandidate(int Step, string Source, bool MatchedHuman, VisualAppearanceDescriptor Features);
public sealed record AppearanceAuditFrame(int Step, string Status, int MatchedCandidates, int UnmatchedCandidates, int MissedHumanCenters);
public sealed record AppearanceAuditGroup(string Source, string MatchStatus, int Count, float MeanContrast,
    float MeanTexture, float MeanSaturation, float MeanLuminance, float MeanVariation);
public sealed record AppearanceAuditReport(string RunId, int LabeledFrames, int ScoredFrames, int MissedHumanCenters,
    IReadOnlyList<AppearanceAuditFrame> Frames, IReadOnlyList<AppearanceAuditCandidate> Candidates,
    IReadOnlyList<AppearanceAuditGroup> Groups);

/// <summary>
/// Audits shape, texture and immediate background contrast of actual screen-pixel candidates.
/// Labels are used ONLY after feature extraction to evaluate associations, never to generate features.
/// This is not a trained model or a source of decisions for the playing agent.
/// </summary>
public static class AppearanceAudit
{
    public static async Task<AppearanceAuditReport> AnalyzeAsync(string folder, LabelSet labels, ReplayTrace replay,
        CombatVisualSettings settings, float radius = 26, CancellationToken cancellationToken = default)
    {
        if (!float.IsFinite(radius) || radius is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(radius));
        settings.Validate();
        GroundTruth.Validate(labels, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);

        var rows = new List<AppearanceAuditCandidate>();
        var frames = new List<AppearanceAuditFrame>();
        var byStep = labels.Frames.ToDictionary(f => f.Step);
        VisualBlob? previousPlayer = null;
        using var reader = new StreamReader(Path.Combine(folder, "trace.jsonl"));
        var step = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var recorded = replay.Frames[step];
            // Sample all preceding frames, including those NOT labeled by a human. Without
            // this causal history, a tracked player at the viewport edge disappears when
            // the study visits only its sparse preselected screenshots.
            using var document = JsonDocument.Parse(line);
            var legacy = TraceEvidenceReliability.IsUnverifiedLegacyFinal(document.RootElement, step, replay.Frames.Count);
            AppearanceFrame? observation = null;
            if (recorded.Phase == "exploring" && !legacy)
            {
                var png = await File.ReadAllBytesAsync(Path.Combine(folder, recorded.Screenshot), cancellationToken);
                observation = VisualAppearanceDescriptors.ObservePng(png, settings, previousPlayer);
                previousPlayer = observation.Player;
            }
            else
            {
                previousPlayer = null;
            }

            if (byStep.TryGetValue(step, out var human))
            {
                // Human annotations are used exclusively for matching *after* the pixels
                // have been observed. Unknown player truth may coexist with scored threats.
                var status = legacy ? "LEGACY_FINAL_UNVERIFIED"
                    : human.Threats.State == "unknown" ? "UNKNOWN_REFERENCE"
                    : recorded.Phase != "exploring" ? "NOT_COMBAT_FRAME" : "SCORED";
                if (status != "SCORED")
                {
                    frames.Add(new AppearanceAuditFrame(step, status, 0, 0, 0));
                }
                else if (observation is null || !observation.PlayerLocated)
                {
                    frames.Add(new AppearanceAuditFrame(step, "PLAYER_NOT_LOCATED", 0, 0, 0));
                }
                else
                {
                    var predictions = observation.Candidates
                        .Select(c => new VisualCandidatePoint(c.Features.X, c.Features.Y)).ToArray();
                    var matched = ThreatMatching.Match(human.Threats.Centers!, predictions, radius);
                    var matchedIndices = matched.MatchedPredictionIndices.ToHashSet();
                    rows.AddRange(observation.Candidates.Select((c, i) =>
                        new AppearanceAuditCandidate(step, c.Source, matchedIndices.Contains(i), c.Features)));
                    frames.Add(new AppearanceAuditFrame(step, "SCORED", matched.TruePositives,
                        matched.FalsePositives.Count, matched.FalseNegatives.Count));
                }
            }
            step++;
        }

        var grouped = rows.GroupBy(r => (r.Source, r.MatchedHuman))
            .OrderBy(g => g.Key.Source, StringComparer.Ordinal).ThenByDescending(g => g.Key.MatchedHuman)
            .Select(g => new AppearanceAuditGroup(g.Key.Source, g.Key.MatchedHuman ? "MATCHED_HUMAN" : "UNMATCHED",
                g.Count(), Average(g, r => r.Features.ContrastToSurroundings),
                Average(g, r => r.Features.GradientDensity), Average(g, r => r.Features.MeanSaturation),
                Average(g, r => r.Features.MeanLuminance), Average(g, r => r.Features.LuminanceStdDev)))
            .ToArray();
        return new AppearanceAuditReport(labels.RunId, labels.Frames.Count,
            frames.Count(f => f.Status == "SCORED"), frames.Sum(f => f.MissedHumanCenters), frames, rows, grouped);
    }

    public static string RenderMarkdown(AppearanceAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ARGUS M2.6a — Appearance evidence audit (offline only)").AppendLine()
            .AppendLine($"Run: `{report.RunId.Replace("`", "")}`. Human-reviewed frames: {report.LabeledFrames}; " +
                $"scored: {report.ScoredFrames}; no comparable measurements: {report.LabeledFrames - report.ScoredFrames}.")
            .AppendLine($"Unmatched human threat centers on scored frames: {report.MissedHumanCenters}.")
            .AppendLine("Matches mean **within 26 pixels of a human-reviewed mark**, not independently verified actor identity.")
            .AppendLine("No model was trained; all features are calculated before reading candidate truth assignments.")
            .AppendLine("Causal player tracking uses preceding screenshot pixels, even on unreviewed frames. " +
                "A noncombat frame or missing visible player resets the pixel-only prior.")
            .AppendLine()
            .AppendLine("| Source | Match status | Count | Contrast to ring | Edge density | Saturation | Luminance | Variation |")
            .AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var g in report.Groups)
            sb.AppendLine($"| {g.Source} | {g.MatchStatus} | {g.Count} | {Format(g.MeanContrast)} | " +
                $"{Format(g.MeanTexture)} | {Format(g.MeanSaturation)} | {Format(g.MeanLuminance)} | {Format(g.MeanVariation)} |");
        sb.AppendLine().AppendLine("| Frame | Status | Matched | Unmatched | Missed human centers |")
            .AppendLine("| ---: | --- | ---: | ---: | ---: |");
        foreach (var f in report.Frames)
            sb.AppendLine($"| {f.Step} | {f.Status} | {f.MatchedCandidates} | " +
                $"{f.UnmatchedCandidates} | {f.MissedHumanCenters} |");
        sb.AppendLine().AppendLine("All numeric patch features are normalized to 0..1. " +
            "Mean luminance and saturation are computed inside each predicted bounding box; " +
            "contrast compares that patch to a four-pixel halo, and edge density counts adjacent luminance jumps. " +
            "Clipped sprites are explicitly flagged in JSON. An empty group is not evidence of a perfect classifier.")
            .AppendLine("**Research restriction:** this result describes the current development screenshots only. " +
                "No feature weight or classification threshold is chosen here; moving scenery, HUD art and effects may " +
                "look like enemies. Keep held-out runs untouched and do not change active steering.");
        return sb.ToString();
    }

    private static string Format(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static float Average(IEnumerable<AppearanceAuditCandidate> items, Func<AppearanceAuditCandidate, float> get) =>
        (float)items.Average(r => get(r));
}
