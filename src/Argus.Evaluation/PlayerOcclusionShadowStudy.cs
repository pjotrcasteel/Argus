using System.Text;
using System.Text.Json;
using Argus.Agents.Visual;
using Argus.Core;
using Argus.Replay;

namespace Argus.Evaluation;

public sealed record PlayerOcclusionShadowEvidence(int Step, string Phase, string Status, int GapAge,
    float Confidence, float? ProposedX, float? ProposedY, string HumanReference, float? HumanDistancePixels);
public sealed record PlayerOcclusionShadowReport(string RunId, int ObservedFrames, int ProposedBridgeFrames,
    int HumanReviewedBridgeFrames, int BridgeNearVisibleHuman, int BridgeFarFromVisibleHuman,
    int BridgeOverAbsentHuman, int BridgeUnknownHumanReference, int UnreviewedBridgeFrames,
    IReadOnlyList<PlayerOcclusionShadowEvidence> Frames);

/// <summary>
/// Measures ONLY pixel-causal shadow hypotheses on all consecutive original screenshots.
/// Human labels enter strictly after each observation and never seed the tracker or detector.
/// This research result is not a verified player identity or an approved game input.
/// </summary>
public static class PlayerOcclusionShadowStudy
{
    public static async Task<PlayerOcclusionShadowReport> AnalyzeAsync(string folder, LabelSet truth,
        ReplayTrace replay, CombatVisualSettings settings, float matchingRadius = 26,
        CancellationToken cancellationToken = default)
    {
        if (!float.IsFinite(matchingRadius) || matchingRadius is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(matchingRadius));
        settings.Validate();
        GroundTruth.Validate(truth, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);

        var references = truth.Frames.ToDictionary(frame => frame.Step);
        var tracker = new PlayerOcclusionShadowTracker();
        VisualBlob? previousMeasuredPlayer = null;
        var frames = new List<PlayerOcclusionShadowEvidence>();
        var lines = await File.ReadAllLinesAsync(Path.Combine(folder, "trace.jsonl"), cancellationToken);
        if (lines.Length != replay.Frames.Count)
            throw new InvalidDataException("Trace lines must match screenshot replay frames exactly.");

        for (var step = 0; step < lines.Length; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = replay.Frames[step];
            using var document = JsonDocument.Parse(lines[step]);
            var exploring = frame.Phase == "exploring" &&
                !TraceEvidenceReliability.IsUnverifiedLegacyFinal(document.RootElement, step, replay.Frames.Count);
            VisualBlob? detectedPlayer = null;
            if (exploring)
            {
                var png = await File.ReadAllBytesAsync(Path.Combine(folder, frame.Screenshot), cancellationToken);
                // Only a immediately PRECEDING actual pixel observation seeds the original detector.
                // Hypothesized missing positions are never fed back as fabricated detections.
                var observation = CombatPixelPerception.Observe(png, settings, previousMeasuredPlayer);
                detectedPlayer = observation.Player;
            }

            previousMeasuredPlayer = detectedPlayer;
            var shadow = tracker.Observe(detectedPlayer, exploring);
            var known = references.TryGetValue(step, out var human) ? human!.Player : null;
            var reference = known?.State switch
            {
                "visible" => "VISIBLE",
                "absent" => "ABSENT",
                "unknown" => "UNKNOWN",
                _ => "UNREVIEWED"
            };
            float? humanDistance = null;
            if (shadow.IsBridgeHypothesis && known?.State == "visible" && shadow.LastKnownPosition is not null)
            {
                var dx = known.Center!.X - shadow.LastKnownPosition.X;
                var dy = known.Center.Y - shadow.LastKnownPosition.Y;
                humanDistance = MathF.Sqrt(dx * dx + dy * dy);
            }

            frames.Add(new PlayerOcclusionShadowEvidence(step, frame.Phase, shadow.Status,
                shadow.ConsecutiveMissingFrames, shadow.HypothesisConfidence,
                shadow.LastKnownPosition?.X, shadow.LastKnownPosition?.Y, reference, humanDistance));
        }

        var bridge = frames.Where(frame => frame.Status is "BRIDGE_ONE" or "BRIDGE_TWO").ToArray();
        var reviewed = bridge.Where(frame => frame.HumanReference is "VISIBLE" or "ABSENT").ToArray();
        return new PlayerOcclusionShadowReport(truth.RunId, frames.Count, bridge.Length,
            reviewed.Length, reviewed.Count(frame => frame.HumanReference == "VISIBLE" &&
                frame.HumanDistancePixels <= matchingRadius),
            reviewed.Count(frame => frame.HumanReference == "VISIBLE" &&
                frame.HumanDistancePixels > matchingRadius),
            reviewed.Count(frame => frame.HumanReference == "ABSENT"),
            bridge.Count(frame => frame.HumanReference == "UNKNOWN"),
            bridge.Count(frame => frame.HumanReference == "UNREVIEWED"), frames);
    }

    public static string RenderMarkdown(PlayerOcclusionShadowReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ARGUS M2.10 — Offline-only player occlusion hypothesis").AppendLine()
            .AppendLine($"Development run `{report.RunId.Replace("`", "")}`: {report.ObservedFrames} actual screenshot frames; " +
                $"{report.ProposedBridgeFrames} unverified shadow proposals.")
            .AppendLine($"Human-reviewed proposals: {report.HumanReviewedBridgeFrames}; within the reference radius: " +
                $"{report.BridgeNearVisibleHuman}; outside: {report.BridgeFarFromVisibleHuman}; " +
                $"human absent: {report.BridgeOverAbsentHuman}. " +
                $"Unknown reference: {report.BridgeUnknownHumanReference}; not manually sampled: {report.UnreviewedBridgeFrames}.")
            .AppendLine("Hypothesis coordinates are **stale pixel observations**; they are NOT current detections or " +
                "verified player identities. A nearby coordinate alone does not prove that decor was rejected. " +
                "No predicted position is used by live navigation, avoidance, threat detection or game input.")
            .AppendLine()
            .AppendLine("| Frame | Phase | Shadow status | Gap age | Prior X | Prior Y | Reference | Pixel distance |")
            .AppendLine("| ---: | --- | --- | ---: | ---: | ---: | --- | ---: |");
        foreach (var frame in report.Frames)
        {
            if (frame.Status is not ("BRIDGE_ONE" or "BRIDGE_TWO" or "REAPPEARED_NEAR_PRIOR" or
                "OBSERVED_DISCONTINUITY" or "BRIDGE_EXPIRED" or "NOT_COMBAT_PHASE") &&
                frame.HumanReference == "UNREVIEWED") continue;

            sb.AppendLine($"| {frame.Step} | {frame.Phase.Replace("|", "/")} | {frame.Status} | {frame.GapAge} | " +
                $"{Format(frame.ProposedX)} | {Format(frame.ProposedY)} | {frame.HumanReference} | " +
                $"{Format(frame.HumanDistancePixels)} |");
        }

        sb.AppendLine().AppendLine("Human labels were used **only for retrospective evaluation**. " +
            "Adjacent frames of the same run are not independent trials; this is an exploratory " +
            "development study, not prospective held-out quality or safe deployment evidence.");
        return sb.ToString();
    }

    private static string Format(float? value) =>
        value?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A";
}
