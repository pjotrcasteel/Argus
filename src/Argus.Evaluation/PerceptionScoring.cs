using System.Text;
using System.Text.Json;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record Counts(int TruePositive, int FalsePositive, int FalseNegative, int TrueNegative, int Unknown);
public sealed record QualityMetrics(Counts Counts, double? Precision, double? Recall, double? F1);
public sealed record PerceptionReport(string RunId, int TotalFrames, int LabeledFrames, QualityMetrics Player,
    QualityMetrics Threats, int ThreatEvidenceUnavailableFrames, float MatchingRadiusPixels)
{
    public int PlayerEvidenceUnavailableFrames { get; init; }
    public int LegacyFinalUnverifiedFrames { get; init; }
    // Human-visible threat points in reviewed frames with no comparable detector evidence.
    // These are not classified as false negatives: the agent did not produce an observation.
    public int ThreatCentersOnUnavailableFrames { get; init; }
    public int ThreatFramesWithHumanCentersAndUnavailableEvidence { get; init; }
}

public static class PerceptionScoring
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Scores actual recorded decisions against explicit annotations. Never re-runs a newer detector on old frames.
    /// Old traces without threat coordinates are marked unscorable, not incorrectly scored as zero detections.
    /// </summary>
    public static async Task<PerceptionReport> ScoreAsync(string folder, LabelSet truth, ReplayTrace replay,
        float radius = 26, CancellationToken cancellationToken = default)
    {
        if (radius is < 1 or > 200 || !float.IsFinite(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
        GroundTruth.Validate(truth, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);

        var player = new Accumulator();
        var threat = new Accumulator();
        var unavailable = 0;
        var playerUnavailable = 0;
        var legacyFinalUnavailable = 0;
        var unavailableThreatCenters = 0;
        var unavailableThreatFramesWithHumanCenters = 0;
        var annotations = truth.Frames.ToDictionary(f => f.Step);
        var tracePath = Path.Combine(folder, "trace.jsonl");

        await using var stream = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        var step = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0) continue;
            if (annotations.TryGetValue(step, out var label))
            {
                using var doc = JsonDocument.Parse(line);
                var record = doc.RootElement;
                var combat = record.TryGetProperty("evidence", out var evidence) &&
                    evidence.ValueKind == JsonValueKind.Object &&
                    evidence.TryGetProperty("combat", out var combatValue) &&
                    combatValue.ValueKind == JsonValueKind.Object ? combatValue : default;

                var frame = replay.Frames[step];
                var legacy = TraceEvidenceReliability.IsUnverifiedLegacyFinal(record, step, replay.Frames.Count);
                var detected = frame.PlayerX is not null && frame.PlayerY is not null
                    ? new VisualCandidatePoint(frame.PlayerX.Value, frame.PlayerY.Value) : null;
                // A missing attempted measurement and a legacy last-frame stale observation
                // must never appear as genuine detector misses or false positives.
                var perceptionAttempted = combat.ValueKind == JsonValueKind.Object ||
                    evidence.ValueKind == JsonValueKind.Object &&
                    evidence.TryGetProperty("phase", out var phase) &&
                    phase.ValueKind == JsonValueKind.String && phase.GetString() == "exploring";
                if (legacy) legacyFinalUnavailable++;
                if (label.Player.State != "unknown" && (legacy || !perceptionAttempted))
                {
                    player.Unknown++;
                    playerUnavailable++;
                }
                else ScorePlayer(label.Player, detected, radius, player);

                if (label.Threats.State == "unknown")
                {
                    threat.Unknown++;
                }
                else if (legacy || combat.ValueKind != JsonValueKind.Object ||
                    !combat.TryGetProperty("threatLocations", out var list) || list.ValueKind != JsonValueKind.Array)
                {
                    unavailable++;
                    threat.Unknown++;
                    // Do not silently make a recall denominator look better by hiding
                    // positively identified human threats on menu/transition or
                    // exploring frames that never emitted recorded candidate locations.
                    if (label.Threats.Centers!.Count > 0)
                    {
                        unavailableThreatFramesWithHumanCenters++;
                        unavailableThreatCenters += label.Threats.Centers.Count;
                    }
                }
                else
                {
                    var candidates = ParseCandidatePositions(list, frame);
                    ScoreThreats(label.Threats.Centers!, candidates, radius, threat);
                }
            }

            step++;
        }

        return new PerceptionReport(truth.RunId, replay.Frames.Count, truth.Frames.Count,
            player.Metrics(), threat.Metrics(), unavailable, radius)
        {
            PlayerEvidenceUnavailableFrames = playerUnavailable,
            LegacyFinalUnverifiedFrames = legacyFinalUnavailable,
            ThreatCentersOnUnavailableFrames = unavailableThreatCenters,
            ThreatFramesWithHumanCentersAndUnavailableEvidence = unavailableThreatFramesWithHumanCenters
        };
    }

    public static string RenderMarkdown(PerceptionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ARGUS perception quality — annotated visible frames").AppendLine()
            .AppendLine($"> Run: `{report.RunId.Replace("`", "")}`. Matched radius: {report.MatchingRadiusPixels:0.#} px. This measures pixel candidate agreement, **not combat competence**.")
            .AppendLine()
            .AppendLine($"Frames in trace: {report.TotalFrames}. Annotated: {report.LabeledFrames}. " +
                $"Player evidence unavailable: {report.PlayerEvidenceUnavailableFrames}. " +
                $"Threat evidence unavailable: {report.ThreatEvidenceUnavailableFrames}. " +
                $"Legacy final frames excluded: {report.LegacyFinalUnverifiedFrames}.")
            .AppendLine($"Human-marked threat centers without comparable detector evidence: " +
                $"{report.ThreatCentersOnUnavailableFrames} across " +
                $"{report.ThreatFramesWithHumanCentersAndUnavailableEvidence} reviewed frames. " +
                "These are UNAVAILABLE, not silently zero false negatives or true positives.")
            .AppendLine()
            .AppendLine("| Object | TP | FP | FN | TN | Unknown | Precision | Recall | F1 |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        Append("Player", report.Player);
        Append("Threat candidates", report.Threats);
        sb.AppendLine().AppendLine("Unknown and missing evidence are **not** treated as absence or correct identification. Labeling must be done on real visible screenshots, never from internal game state.");
        return sb.ToString();

        void Append(string name, QualityMetrics m)
        {
            var c = m.Counts;
            sb.AppendLine($"| {name} | {c.TruePositive} | {c.FalsePositive} | {c.FalseNegative} | {c.TrueNegative} | {c.Unknown} | {Pct(m.Precision)} | {Pct(m.Recall)} | {Pct(m.F1)} |");
        }
    }

    private static void ScorePlayer(PlayerLabel expected, VisualCandidatePoint? detected, float radius, Accumulator target)
    {
        if (expected.State == "unknown") { target.Unknown++; return; }
        if (expected.State == "absent")
        {
            if (detected is null) target.Tn++;
            else target.Fp++;
            return;
        }

        if (detected is null) { target.Fn++; return; }
        if (Distance(expected.Center!, detected) <= radius) target.Tp++;
        else { target.Fp++; target.Fn++; }
    }

    private static void ScoreThreats(IReadOnlyList<VisualCandidatePoint> expected,
        IReadOnlyList<VisualCandidatePoint> detected, float radius, Accumulator target)
    {
        var matches = ThreatMatching.Match(expected, detected, radius);
        target.Tp += matches.TruePositives;
        target.Fp += matches.FalsePositives.Count;
        target.Fn += matches.FalseNegatives.Count;
        if (expected.Count == 0 && detected.Count == 0) target.Tn++;
    }

    private static IReadOnlyList<VisualCandidatePoint> ParseCandidatePositions(JsonElement list, ReplayFrame frame)
    {
        if (list.GetArrayLength() > 100) throw new InvalidDataException("Too many recorded candidates in one frame.");
        var points = new List<VisualCandidatePoint>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("x", out var x) || !x.TryGetSingle(out var cx) ||
                !item.TryGetProperty("y", out var y) || !y.TryGetSingle(out var cy) ||
                !float.IsFinite(cx) || !float.IsFinite(cy) || cx < 0 || cy < 0 || cx >= frame.Width || cy >= frame.Height)
                throw new InvalidDataException("Invalid recorded candidate pixel position.");
            points.Add(new VisualCandidatePoint(cx, cy));
        }
        return points;
    }

    private static float Distance(VisualCandidatePoint a, VisualCandidatePoint b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string Pct(double? number) => number is null ? "N/A" : (number.Value * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private sealed class Accumulator
    {
        public int Tp, Fp, Fn, Tn, Unknown;
        public QualityMetrics Metrics()
        {
            double? precision = Tp + Fp == 0 ? null : (double)Tp / (Tp + Fp);
            double? recall = Tp + Fn == 0 ? null : (double)Tp / (Tp + Fn);
            double? f1 = precision is null || recall is null || precision + recall == 0
                ? null : 2 * precision * recall / (precision + recall);
            return new QualityMetrics(new Counts(Tp, Fp, Fn, Tn, Unknown), precision, recall, f1);
        }
    }
}
