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
    int UnresolvedVisible, int UnknownOrUnreviewed);

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
            report.Frames.Count(f => f.HumanReference == "VISIBLE" && f.WitnessStatus == "EMPTY_PATCH_VETO"),
            report.Frames.Count(f => f.HumanReference == "VISIBLE" && f.WitnessStatus == "INDETERMINATE"),
            report.Frames.Count(f => f.HumanReference is "UNKNOWN" or "UNREVIEWED"));

        if (summary.CorrectlyVetoedAbsent != report.VetoedOverHumanAbsent ||
            summary.FalseVetoedVisible != report.VetoedOverHumanVisible ||
            summary.UnresolvedVisible != report.UnresolvedOverHumanVisible ||
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
        var sb = new StringBuilder("run_id,step,raw_bridge,player_palette_pixels,max_rgb_stddev,strong_edge_fraction,witness,reviewed_player_label\n");
        foreach (var row in report.Frames.OrderBy(x => x.Step))
        {
            var label = row.HumanReference switch
            {
                "VISIBLE" => "1",
                "ABSENT" => "0",
                _ => ""
            };
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
            .AppendLine($"| VISIBLE, wrongly vetoed | {s.FalseVetoedVisible} |")
            .AppendLine($"| VISIBLE, correctly left uncertain | {s.UnresolvedVisible} |")
            .AppendLine($"| UNKNOWN or unreviewed (not labeled negative) | {s.UnknownOrUnreviewed} |")
            .AppendLine()
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
