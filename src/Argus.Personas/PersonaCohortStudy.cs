using System.Globalization;
using System.Text;
using Argus.Replay;

namespace Argus.Personas;

public sealed record PersonaSessionContext(string? GameVersion = null, string? GameProfile = null, string? Viewport = null,
    string? PolicyVersion = null);

public sealed record PersonaCohortSession(string RunId, ReplayTrace Replay, PersonaSessionContext Context);
public sealed record PersonaCohortRun(string RunId, PersonaSessionContext Context, PersonaContrastReport Contrast,
    TraitAblationReport Ablations);

/// <summary>Unweighted across RECORDED RUNS, not an independence claim for screenshots inside a run.</summary>
public sealed record RunRateDistribution(int TotalRuns, int EligibleRuns, double? Min, double? Median,
    double? Max, double? UnweightedMean);

public sealed record PersonaCohortPair(string FirstPersona, string SecondPersona,
    RunRateDistribution DirectionDifferences, RunRateDistribution FullActionDifferences);

public sealed record PersonaCohortTrait(string Trait, RunRateDistribution FullActionDifferences,
    RunRateDistribution DirectionDifferences);

public sealed record PersonaCohortReport(int SchemaVersion, int Seed, int RunCount, bool MatchedContext,
    IReadOnlyList<PersonaCohortRun> Runs, IReadOnlyList<PersonaCohortPair> Pairs,
    IReadOnlyList<PersonaCohortTrait> Traits);

public static class PersonaCohortStudy
{
    public static PersonaCohortReport Analyze(IEnumerable<PersonaCohortSession> input, int seed,
        MovementBindings keys, PersonaProfile? ablationBaseline = null)
    {
        var sessions = input.OrderBy(r => r.RunId, StringComparer.Ordinal).ToArray();
        if (sessions.Length is < 2 or > 128)
            throw new InvalidDataException("Analyze 2–128 independently recorded run folders.");
        if (sessions.Any(r => string.IsNullOrWhiteSpace(r.RunId) || r.RunId.Length > 64 || r.Replay.Frames.Count == 0) ||
            sessions.Select(r => r.RunId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sessions.Length)
            throw new InvalidDataException("Run names must be nonempty, unique and have recorded frames.");

        keys.Validate();
        var baseline = ablationBaseline ?? PersonaPresets.Default[1];
        var studies = sessions.Select(session => new PersonaCohortRun(session.RunId, session.Context,
            PersonaContrastAnalyzer.Analyze(session.RunId, session.Replay, PersonaPresets.Default, keys, seed),
            PersonaTraitAblations.Analyze(session.Replay, baseline, keys, seed))).ToArray();

        var first = studies[0];
        var pairs = first.Contrast.Pairs.Select(pair =>
            new PersonaCohortPair(pair.FirstPersona, pair.SecondPersona,
                Distribution(studies.Select(s => s.Contrast.Pairs.Single(p =>
                    p.FirstPersona == pair.FirstPersona && p.SecondPersona == pair.SecondPersona).DirectionDifferenceRate)),
                Distribution(studies.Select(s => s.Contrast.Pairs.Single(p =>
                    p.FirstPersona == pair.FirstPersona && p.SecondPersona == pair.SecondPersona).FullActionDifferenceRate))))
            .ToArray();

        var traits = first.Ablations.Traits.Select(trait =>
            new PersonaCohortTrait(trait.Trait,
                Distribution(studies.Select(s => s.Ablations.Traits.Single(t => t.Trait == trait.Trait).FullActionDifferenceRate)),
                Distribution(studies.Select(s => s.Ablations.Traits.Single(t => t.Trait == trait.Trait).DirectionDifferenceRate))))
            .ToArray();

        var matched = sessions.All(r => r.Context is
        {
            GameVersion: { Length: > 0 }, GameProfile: { Length: > 0 },
            Viewport: { Length: > 0 }, PolicyVersion: { Length: > 0 }
        }) && sessions.Skip(1).All(r => r.Context == sessions[0].Context);

        return new PersonaCohortReport(1, seed, sessions.Length, matched, studies, pairs, traits);
    }

    public static RunRateDistribution Distribution(IEnumerable<double?> input)
    {
        var all = input.ToArray();
        var values = all.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        if (values.Any(v => !double.IsFinite(v) || v is < 0 or > 1))
            throw new InvalidDataException("Inconsistent rate was found in the independent-run cohort.");
        if (values.Length == 0) return new RunRateDistribution(all.Length, 0, null, null, null, null);
        var middle = values.Length / 2;
        var median = values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
        return new RunRateDistribution(all.Length, values.Length, values[0], median, values[^1], values.Average());
    }
}

public static class PersonaCohortMarkdown
{
    public static string Render(PersonaCohortReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# ARGUS — independent-run persona shadow research")
            .AppendLine()
            .AppendLine("> **DESCRIPTIVE, SHADOW ONLY.** No proposed actions were executed. Individual frames within a run are correlated.")
            .AppendLine()
            .AppendLine($"Recorded run folders: {report.RunCount}; random seed: {report.Seed}. " +
                $"Matched version/profile/viewport/policy metadata: {(report.MatchedContext ? "YES" : "NO / UNKNOWN")}.")
            .AppendLine("Even matching metadata and nonidentical traces do not prove session independence, observed player realism or balance.")
            .AppendLine()
            .AppendLine("## Run-by-run direction differences")
            .AppendLine()
            .AppendLine("| Run | Total frames | Gameplay frames | Unknown candidate coords | Pair | Direction changes / both-moving | Action changes / both-proposed |")
            .AppendLine("| --- | ---: | ---: | ---: | --- | ---: | ---: |");

        foreach (var run in report.Runs)
            foreach (var pair in run.Contrast.Pairs)
                b.AppendLine($"| {Cell(run.RunId)} | {run.Contrast.TotalFrames} | {run.Contrast.GameplayFrames} | " +
                    $"{run.Contrast.GameplayFramesWithCandidateLocationsUnknown} | {Cell(pair.FirstPersona)} vs {Cell(pair.SecondPersona)} | " +
                    $"{pair.DifferentDirections}/{pair.BothMovingFrames} | {pair.DifferentFullActions}/{pair.BothProposedFrames} |");

        b.AppendLine().AppendLine("## Per-run rate distributions (unweighted)")
            .AppendLine()
            .AppendLine("| Pair | Eligible runs | Median direction divergence | Range | Median any-action divergence |")
            .AppendLine("| --- | ---: | ---: | --- | ---: |");
        foreach (var pair in report.Pairs)
            b.AppendLine($"| {Cell(pair.FirstPersona)} vs {Cell(pair.SecondPersona)} | " +
                $"{pair.DirectionDifferences.EligibleRuns}/{report.RunCount} | " +
                $"{Rate(pair.DirectionDifferences.Median)} | {Rate(pair.DirectionDifferences.Min)}–{Rate(pair.DirectionDifferences.Max)} | " +
                $"{Rate(pair.FullActionDifferences.Median)} |");

        b.AppendLine().AppendLine("## Isolated trait ablations (0 versus 1, same random stream)")
            .AppendLine()
            .AppendLine("| Trait | Eligible runs | Median action difference | Median direction difference |")
            .AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var trait in report.Traits)
            b.AppendLine($"| {Cell(trait.Trait)} | {trait.FullActionDifferences.EligibleRuns}/{report.RunCount} | " +
                $"{Rate(trait.FullActionDifferences.Median)} | {Rate(trait.DirectionDifferences.Median)} |");

        b.AppendLine()
            .AppendLine("An unchanged trait within these recordings may mean the necessary situation was never observed. " +
                "Low/missing detection confidence and candidate positions are measurement limitations; the tool does not certify enemies.")
            .AppendLine("All median/range values summarize **per-run** proportions; no frame-pooled statistical significance is implied. " +
                "N/A means no eligible run-level denominator, not a zero effect.")
            .AppendLine("Independent human-labeled held-out screenshots and confirmed game outcomes are required before claiming realism, learning or balance.");
        return b.ToString();
    }

    private static string Rate(double? value) => value.HasValue
        ? (value.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A";
    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Replace("`", "");
}
