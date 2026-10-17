using System.Text;
using System.Text.Json;
using Argus.Agents.Visual;
using Argus.Core;
using Argus.Replay;
using StbImageSharp;

namespace Argus.Evaluation;

public sealed record PlayerVisibilityAuditFrame(int Step, string Phase, string Status,
    string PixelComponentStatus, int NearbyPaletteComponents, PlayerColorComponentEvidence? NearestComponent);

public sealed record PlayerVisibilityAuditReport(string RunId, int ReviewedFrames, int ComparablePlayerFrames,
    int RecordedPlayerFrames, int MissingRecordedPlayerFrames, int NonCombatVisiblePlayerFrames,
    IReadOnlyList<PlayerVisibilityAuditFrame> Frames);

/// <summary>
/// Post-hoc audit only. A human pixel coordinate selects a nearby component for explanation,
/// but is never passed to the detector as an identity or steering location.
/// </summary>
public static class PlayerVisibilityAudit
{
    public static async Task<PlayerVisibilityAuditReport> AnalyzeAsync(string folder, LabelSet labels,
        ReplayTrace replay, CombatVisualSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        GroundTruth.Validate(labels, Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)), replay);
        var rows = await File.ReadAllLinesAsync(Path.Combine(folder, "trace.jsonl"), cancellationToken);
        var original = labels.Frames.OrderBy(f => f.Step);
        var findings = new List<PlayerVisibilityAuditFrame>();

        foreach (var labeled in original)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = replay.Frames[labeled.Step];
            using var json = JsonDocument.Parse(rows[labeled.Step]);
            var legacy = TraceEvidenceReliability.IsUnverifiedLegacyFinal(json.RootElement, labeled.Step, replay.Frames.Count);
            var status = labeled.Player.State == "unknown" ? "UNKNOWN_REFERENCE"
                : legacy ? "LEGACY_FINAL_UNVERIFIED"
                : frame.Phase != "exploring" ? "NOT_COMBAT_PHASE"
                : labeled.Player.State == "absent" ? "HUMAN_PLAYER_ABSENT"
                : frame.PlayerX is not null && frame.PlayerY is not null ? "RECORDED_PLAYER"
                : "PLAYER_NOT_RECORDED";

            if (labeled.Player.State != "visible" || frame.Phase != "exploring" || legacy)
            {
                findings.Add(new PlayerVisibilityAuditFrame(labeled.Step, frame.Phase, status, "NOT_APPLICABLE", 0, null));
                continue;
            }

            var png = await File.ReadAllBytesAsync(Path.Combine(folder, frame.Screenshot), cancellationToken);
            var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
            var components = CombatPixelPerception.InspectPlayerColorComponentsRgb(image.Data, image.Width, image.Height,
                settings, labeled.Player.Center!);
            var nearest = components.FirstOrDefault();
            var componentStatus = nearest?.RejectionReason ?? "NO_NEARBY_PLAYER_PALETTE_COMPONENT";
            findings.Add(new PlayerVisibilityAuditFrame(labeled.Step, frame.Phase, status, componentStatus,
                components.Count, nearest));
        }

        return new PlayerVisibilityAuditReport(labels.RunId, findings.Count,
            findings.Count(f => f.Status is "RECORDED_PLAYER" or "PLAYER_NOT_RECORDED"),
            findings.Count(f => f.Status == "RECORDED_PLAYER"),
            findings.Count(f => f.Status == "PLAYER_NOT_RECORDED"),
            findings.Count(f => f.Status == "NOT_COMBAT_PHASE" && labels.Frames
                .Any(l => l.Step == f.Step && l.Player.State == "visible")),
            findings);
    }

    public static string RenderMarkdown(PlayerVisibilityAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ARGUS M2.9 — Pixel player visibility diagnostic (development only)").AppendLine()
            .AppendLine($"Run `{report.RunId.Replace("`", "")}`. {report.ReviewedFrames} human-labeled screenshots, " +
                $"{report.ComparablePlayerFrames} visible-player combat frames, " +
                $"{report.MissingRecordedPlayerFrames} frames where the original trace did not locate a visible player.")
            .AppendLine("**Important:** human positions are used only for post-hoc component selection. " +
                "The diagnostic does not recover a sprite, make an identity claim or change any game input.")
            .AppendLine()
            .AppendLine("| Step | Recorded phase | Observation | Nearest component | Matching pixels | Bounding box |")
            .AppendLine("| ---: | --- | --- | --- | ---: | --- |");

        foreach (var frame in report.Frames)
        {
            var component = frame.NearestComponent;
            sb.AppendLine($"| {frame.Step} | {frame.Phase.Replace("|", "/")} | {frame.Status} | " +
                $"{frame.PixelComponentStatus} | " +
                $"{(component is null ? "N/A" : component.MatchingPixels.ToString())} | " +
                $"{(component is null ? "N/A" : component.Width + "x" + component.Height)} |");
        }

        sb.AppendLine().AppendLine("Raw eligible component shape does not imply the live tracker must select " +
            "that blob: other image components, temporal priors and the screen-center guard matter. " +
            "A too-wide component can mean a merged spell effect; too few matching pixels can mean " +
            "occlusion or an unsuitable palette. These are geometric hypotheses, not proven game entities.");
        return sb.ToString();
    }
}
