using System.Globalization;
using System.Text;
using Argus.Agents.Visual;

namespace Argus.Evaluation;

/// <summary>
/// Research dataset QA, not a classifier and not independent accuracy statistics.
/// Each report is one correlated play session, with manual labels used for evaluation only.
/// </summary>
public sealed record PlayerShadowHardNegativeSummary(string RunId, int TotalBridgeProposals,
    int ReviewedAbsentProposals, int CorrectlyVetoedAbsent, int UnresolvedAbsentTexturedWithoutPalette,
    int UnresolvedAbsentWithPalette, int UnresolvedAbsentOther, int FalseVetoedVisible,
    int UnresolvedVisible, int UnknownOrUnreviewed)
{
    /// <summary>A visible player ELSEWHERE is a candidate-location NEGATIVE, not a positive training label.</summary>
    public int VisibleElsewhereCandidates { get; init; }
    public int VetoedFarVisible { get; init; }
    public int UnresolvedFarVisible { get; init; }
}

public static class PlayerShadowHardNegativeEvidence
{
    public static PlayerShadowHardNegativeSummary Summarize(PlayerCurrentFrameVetoReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Frames is null || report.Frames.Count != report.RawBridgeProposals ||
            report.Frames.Any(frame => frame.WitnessStatus is not ("EMPTY_PATCH_VETO" or "INDETERMINATE") ||
                frame.HumanReference is not ("VISIBLE" or "ABSENT" or "UNKNOWN" or "UNREVIEWED") ||
                frame.PlayerPalettePixels < 0 || !float.IsFinite(frame.MaximumRgbChannelStdDev) ||
                !float.IsFinite(frame.StrongGradientFraction) || frame.StrongGradientFraction is < 0 or > 1))
            throw new InvalidDataException("Malformed original-image bridge evidence.");

        // Validate all reference distances and compute LOCAL target, never global visibility.
        var local = report.Frames.Select(PlayerShadowLocalTruth.ReviewedLocalLabel).ToArray();
        var absent = report.Frames.Where(f => f.HumanReference == "ABSENT").ToArray();
        var unresolved = absent.Where(f => f.WitnessStatus == "INDETERMINATE").ToArray();
        var summary = new PlayerShadowHardNegativeSummary(report.RunId, report.RawBridgeProposals,
            absent.Length, absent.Length - unresolved.Length,
            unresolved.Count(f => f.PlayerPalettePixels == 0 &&
                (f.MaximumRgbChannelStdDev >= PlayerCurrentFrameWitnessProbe.MaxEmptyChannelStdDev ||
                 f.StrongGradientFraction >= PlayerCurrentFrameWitnessProbe.MaxEmptyGradientFraction)),
            unresolved.Count(f => f.PlayerPalettePixels > 0),
            unresolved.Count(f => f.PlayerPalettePixels == 0 &&
                f.MaximumRgbChannelStdDev < PlayerCurrentFrameWitnessProbe.MaxEmptyChannelStdDev &&
                f.StrongGradientFraction < PlayerCurrentFrameWitnessProbe.MaxEmptyGradientFraction),
            report.Frames.Where((f, i) => local[i] == 1 && f.WitnessStatus == "EMPTY_PATCH_VETO").Count(),
            report.Frames.Where((f, i) => local[i] == 1 && f.WitnessStatus == "INDETERMINATE").Count(),
            report.Frames.Count(f => f.HumanReference is "UNKNOWN" or "UNREVIEWED"))
        {
            VisibleElsewhereCandidates = report.Frames.Where((f, i) =>
                f.HumanReference == "VISIBLE" && local[i] == 0).Count(),
            VetoedFarVisible = report.Frames.Where((f, i) =>
                f.HumanReference == "VISIBLE" && local[i] == 0 && f.WitnessStatus == "EMPTY_PATCH_VETO").Count(),
            UnresolvedFarVisible = report.Frames.Where((f, i) =>
                f.HumanReference == "VISIBLE" && local[i] == 0 && f.WitnessStatus == "INDETERMINATE").Count()
        };

        if (summary.CorrectlyVetoedAbsent != report.VetoedOverHumanAbsent ||
            summary.FalseVetoedVisible + summary.VetoedFarVisible != report.VetoedOverHumanVisible ||
            summary.UnresolvedVisible + summary.UnresolvedFarVisible != report.UnresolvedOverHumanVisible ||
            unresolved.Length != report.UnresolvedOverHumanAbsent ||
            summary.UnknownOrUnreviewed != report.UnknownOrUnreviewedProposals ||
            report.VetoedProposals + report.UnresolvedProposals != report.RawBridgeProposals ||
            summary.UnresolvedAbsentTexturedWithoutPalette + summary.UnresolvedAbsentWithPalette +
                summary.UnresolvedAbsentOther != unresolved.Length)
            throw new InvalidDataException("Bridge summary disagrees with reference-level counts.");

        return summary;
    }

    /// <summary>
    /// Trainer-ready rows, but NOT a fitted model. RunId is a mandatory grouping key for a future
    /// leave-one-run-out evaluation; frame rows must never be randomly split into training and test.
    /// UNKNOWN/UNREVIEWED have no label rather than being silently interpreted as absence.
    /// </summary>
    public static string RenderFeatureCsv(PlayerCurrentFrameVetoReport report)
    {
        _ = Summarize(report);
        // Explicit schema V2: reject previously exported global-visibility training labels.
        var sb = new StringBuilder("run_id,step,raw_bridge,player_palette_pixels,max_rgb_stddev,strong_edge_fraction,witness,reviewed_candidate_local_label_v2\n");
        foreach (var row in report.Frames.OrderBy(x => x.Step))
        {
            var localLabel = PlayerShadowLocalTruth.ReviewedLocalLabel(row);
            var label = localLabel?.ToString(CultureInfo.InvariantCulture) ?? "";
            sb.Append(EscapeCsv(report.RunId)).Append(',').Append(row.Step).Append(',')
                .Append(EscapeCsv(row.RawBridgeStatus)).Append(',')
                .Append(row.PlayerPalettePixels).Append(',')
                .Append(row.MaximumRgbChannelStdDev.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.StrongGradientFraction.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(row.WitnessStatus)).Append(',')
                .Append(label).Append('\n');
        }

        return sb.ToString();
    }

    public static string RenderMarkdown(PlayerShadowHardNegativeSummary s)
    {
        var b = new StringBuilder();
        b.AppendLine("# M2.12 — Hard-negative stale-player evidence").AppendLine()
            .AppendLine($"Run `{s.RunId.Replace("`", "")}`. **{s.TotalBridgeProposals}** shadow guesses " +
                "from the original current-screenshot evidence, NOT live player detections.")
            .AppendLine()
            .AppendLine("| Human-reviewed group | Proposals |")
            .AppendLine("| --- | ---: |")
            .AppendLine($"| Human ABSENT (all) | {s.ReviewedAbsentProposals} |")
            .AppendLine($"| ABSENT, correctly vetoed empty patch | {s.CorrectlyVetoedAbsent} |")
            .AppendLine($"| ABSENT, **unresolved textured without player palette** | {s.UnresolvedAbsentTexturedWithoutPalette} |")
            .AppendLine($"| ABSENT, **unresolved player-palette lookalike** | {s.UnresolvedAbsentWithPalette} |")
            .AppendLine($"| ABSENT, unresolved other | {s.UnresolvedAbsentOther} |")
            .AppendLine($"| VISIBLE at stale candidate, wrongly vetoed | {s.FalseVetoedVisible} |")
            .AppendLine($"| VISIBLE at stale candidate, still uncertain | {s.UnresolvedVisible} |")
            .AppendLine($"| VISIBLE elsewhere (>26px from stale candidate): LOCAL NEGATIVE | {s.VisibleElsewhereCandidates} |")
            .AppendLine($"| Far-visible candidate vetoed / unresolved | {s.VetoedFarVisible} / {s.UnresolvedFarVisible} |")
            .AppendLine($"| UNKNOWN or unreviewed (not labeled negative) | {s.UnknownOrUnreviewed} |")
            .AppendLine()
            .AppendLine("The CSV schema is **v2 LOCAL POSITION TRUTH**: globally visible ELSEWHERE " +
                "is a negative for the stale coordinate, not a positive. Radius 26px (frozen).")
            .AppendLine("The adjacent CSV is **not a trained AI**. Its targets come exclusively " +
                "from independent human-reviewed original screenshots. Unreviewed frames have an EMPTY target. " +
                "One run is one correlated group: do not split neighboring frames randomly. " +
                "A colored lookalike cannot be accepted as evidence of a player. " +
                "This benchmark requires separate original-image human reference and fresh, untouched held-out tests.");
        return b.ToString();
    }

    private static string EscapeCsv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
