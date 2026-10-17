using System.Text;
using Argus.Agents.Visual;
using Argus.Core;
using Argus.Replay;
using StbImageSharp;

namespace Argus.Evaluation;

public sealed record PlayerCurrentFrameVetoEvidence(int Step, string Phase, string RawBridgeStatus,
    string WitnessStatus, string HumanReference, float? HumanDistancePixels,
    int PlayerPalettePixels, float MaximumRgbChannelStdDev, float StrongGradientFraction);

public sealed record PlayerCurrentFrameVetoReport(string RunId, int RawBridgeProposals,
    int VetoedProposals, int UnresolvedProposals, int VetoedOverHumanAbsent, int VetoedOverHumanVisible,
    int UnresolvedOverHumanAbsent, int UnresolvedOverHumanVisible, int UnknownOrUnreviewedProposals,
    IReadOnlyList<PlayerCurrentFrameVetoEvidence> Frames);

/// <summary>
/// Post-processes the already-computed, causally measured M2.10 shadow hypotheses
/// with only CURRENT original PNG pixels near each stale measured location.
/// A nonuniform patch remains INDETERMINATE: it never confirms a sprite or game input.
/// </summary>
public static class PlayerCurrentFrameVetoStudy
{
    public static async Task<PlayerCurrentFrameVetoReport> AnalyzeAsync(string folder, ReplayTrace replay,
        PlayerOcclusionShadowReport shadow, CombatVisualSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shadow);
        settings.Validate();
        if (shadow.ObservedFrames != replay.Frames.Count ||
            shadow.Frames.Count != replay.Frames.Count ||
            !shadow.Frames.Select(f => f.Step).SequenceEqual(Enumerable.Range(0, replay.Frames.Count)))
            throw new InvalidDataException("Shadow hypotheses must match the complete, ordered original screenshot trace.");

        var findings = new List<PlayerCurrentFrameVetoEvidence>();
        foreach (var proposal in shadow.Frames.Where(f => f.Status is "BRIDGE_ONE" or "BRIDGE_TWO"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (proposal.ProposedX is null || proposal.ProposedY is null ||
                replay.Frames[proposal.Step].Phase != "exploring")
                throw new InvalidDataException("No fabricated player coordinates or noncombat bridge may be audited.");

            var frame = replay.Frames[proposal.Step];
            var png = await File.ReadAllBytesAsync(Path.Combine(folder, frame.Screenshot), cancellationToken);
            var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
            var witness = PlayerCurrentFrameWitnessProbe.InspectRgb(image.Data, image.Width, image.Height, settings,
                new VisualCandidatePoint(proposal.ProposedX.Value, proposal.ProposedY.Value));
            findings.Add(new PlayerCurrentFrameVetoEvidence(proposal.Step, proposal.Phase, proposal.Status,
                witness.Status, proposal.HumanReference, proposal.HumanDistancePixels,
                witness.PlayerPalettePixels, witness.MaximumRgbChannelStdDev, witness.StrongGradientFraction));
        }

        var vetoed = findings.Where(x => x.WitnessStatus == "EMPTY_PATCH_VETO").ToArray();
        var unresolved = findings.Where(x => x.WitnessStatus == "INDETERMINATE").ToArray();
        if (findings.Count != shadow.ProposedBridgeFrames)
            throw new InvalidDataException("Every original shadow proposal requires current screenshot evidence.");

        return new PlayerCurrentFrameVetoReport(shadow.RunId, findings.Count, vetoed.Length, unresolved.Length,
            vetoed.Count(x => x.HumanReference == "ABSENT"), vetoed.Count(x => x.HumanReference == "VISIBLE"),
            unresolved.Count(x => x.HumanReference == "ABSENT"), unresolved.Count(x => x.HumanReference == "VISIBLE"),
            findings.Count(x => x.HumanReference is "UNKNOWN" or "UNREVIEWED"), findings);
    }

    public static string RenderMarkdown(PlayerCurrentFrameVetoReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# ARGUS M2.11 — Current-image veto of stale player-memory guesses").AppendLine()
            .AppendLine($"Development run `{report.RunId.Replace("`", "")}`. Raw M2.10 proposals: " +
                $"{report.RawBridgeProposals}; empty-patch vetoes: {report.VetoedProposals}; " +
                $"uncertain proposals: {report.UnresolvedProposals}.")
            .AppendLine($"Among manually reviewed frames, correct absence vetoes: {report.VetoedOverHumanAbsent}; " +
                $"**visible-player false vetoes: {report.VetoedOverHumanVisible}**; " +
                $"**absent-player proposals still unresolved: {report.UnresolvedOverHumanAbsent}**; " +
                $"visible-player proposals unresolved: {report.UnresolvedOverHumanVisible}. " +
                $"Unknown/unreviewed: {report.UnknownOrUnreviewedProposals}.")
            .AppendLine("**INDETERMINATE is NOT a player detection or positive identity.** A textured decoration, " +
                "particle or a blank-but-textured region can remain undecidable. " +
                "EMPTY_PATCH_VETO is only a conservative negative cue, NOT proof of player absence. " +
                "No gameplay action uses these results.")
            .AppendLine()
            .AppendLine("| Frame | Raw bridge | Current pixels | Player palette pixels | RGB variation | Edge fraction | Human state | Human distance |")
            .AppendLine("| ---: | --- | --- | ---: | ---: | ---: | --- | ---: |");
        foreach (var item in report.Frames)
            b.AppendLine($"| {item.Step} | {item.RawBridgeStatus} | {item.WitnessStatus} | " +
                $"{item.PlayerPalettePixels} | {item.MaximumRgbChannelStdDev:0.0} | " +
                $"{item.StrongGradientFraction:0.000} | {item.HumanReference} | " +
                $"{(item.HumanDistancePixels.HasValue ? item.HumanDistancePixels.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "N/A")} |");

        b.AppendLine().AppendLine("Human labels were used only to **audit** the outcome, not to choose an " +
            "image region, infer player identity, select thresholds per frame or steer the agent. " +
            "This synthetic/dev-only result is NOT prospective held-out accuracy.");
        return b.ToString();
    }
}
