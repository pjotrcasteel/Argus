using System.Globalization;
using System.Text;
using Argus.Core;
using Argus.Replay;

namespace Argus.Personas;

/// <summary>Each denominator is explicit; none of these measurements is a game outcome or human realism score.</summary>
public sealed record PersonaActionMetrics(string PersonaId, int ComparedFrames, int SuggestedWaits, int SuggestedMoves,
    int FramesWithoutProposal, double? MeanMoveDurationMs, IReadOnlyDictionary<string, int> MovementByKey);

/// <summary>
/// Movement direction is evaluated only when BOTH profiles propose movement.
/// Equality of action kinds, direction and duration is reported separately to avoid false 'diversity'.
/// </summary>
public sealed record PersonaPairMetrics(string FirstPersona, string SecondPersona, int BothProposedFrames,
    int DifferentActionKinds, int BothMovingFrames, int DifferentDirections, int SameDirectionDifferentDurations,
    int DifferentFullActions, double? FullActionDifferenceRate, double? DirectionDifferenceRate);

public sealed record PersonaContrastReport(string RunId, int Seed, int TotalFrames, int GameplayFrames,
    int GameplayFramesWithPlayerLocation, int GameplayFramesWithConfidence, int GameplayFramesWithCandidateLocations,
    int GameplayFramesWithCandidateLocationsUnknown, int GameplayFramesWithCandidateObjects,
    IReadOnlyList<PersonaActionMetrics> Personas, IReadOnlyList<PersonaPairMetrics> Pairs);

public static class PersonaContrastAnalyzer
{
    public static PersonaContrastReport Analyze(string runId, ReplayTrace replay, IReadOnlyList<PersonaProfile> personas,
        MovementBindings keys, int seed)
    {
        if (string.IsNullOrWhiteSpace(runId) || runId.Length > 128 || replay.Frames.Count == 0 ||
            personas.Count is < 2 or > 16)
            throw new ArgumentException("A nonempty run and 2–16 distinct personas are required.");
        keys.Validate();
        foreach (var persona in personas) persona.Validate();
        if (personas.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != personas.Count)
            throw new ArgumentException("Persona IDs must be unique, ignoring case.", nameof(personas));

        var proposals = personas.ToDictionary(p => p.Id,
            p => replay.Frames.Select(f => PersonaShadowPolicy.Propose(f, p, keys, seed)).ToArray(), StringComparer.Ordinal);
        var gameplay = replay.Frames.Where(f => f.Phase == "exploring").ToArray();

        var statistics = personas.Select(persona =>
        {
            var actions = proposals[persona.Id].Select(p => p.ProposedAction).ToArray();
            var moves = actions.Where(IsMove).Select(a => a!).ToArray();
            var byKey = moves.GroupBy(a => a.Key!, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            return new PersonaActionMetrics(persona.Id, actions.Count(a => a is not null),
                actions.Count(a => a?.Kind == GameActionKind.Wait), moves.Length, actions.Count(a => a is null),
                moves.Length == 0 ? null : moves.Average(a => a.DurationMs), byKey);
        }).ToArray();

        var pairs = new List<PersonaPairMetrics>();
        for (var i = 0; i < personas.Count; i++)
        {
            for (var j = i + 1; j < personas.Count; j++)
            {
                var a = proposals[personas[i].Id];
                var b = proposals[personas[j].Id];
                var both = 0;
                var kinds = 0;
                var bothMove = 0;
                var directions = 0;
                var durations = 0;
                var different = 0;
                for (var k = 0; k < a.Length; k++)
                {
                    var left = a[k].ProposedAction;
                    var right = b[k].ProposedAction;
                    if (left is null || right is null) continue;
                    both++;
                    if (left.Kind != right.Kind) kinds++;
                    if (!Equals(left, right)) different++;
                    if (!IsMove(left) || !IsMove(right)) continue;
                    bothMove++;
                    if (!string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase)) directions++;
                    else if (left.DurationMs != right.DurationMs) durations++;
                }

                pairs.Add(new PersonaPairMetrics(personas[i].Id, personas[j].Id, both, kinds, bothMove,
                    directions, durations, different, Rate(different, both), Rate(directions, bothMove)));
            }
        }

        return new PersonaContrastReport(runId, seed, replay.Frames.Count, gameplay.Length,
            gameplay.Count(f => f.PlayerX is not null && f.PlayerY is not null),
            gameplay.Count(f => f.Confidence is not null),
            gameplay.Count(f => f.ThreatLocations is not null),
            gameplay.Count(f => f.ThreatLocations is null),
            gameplay.Count(f => f.ThreatLocations is { Count: > 0 }),
            statistics, pairs);
    }

    private static bool IsMove(GameAction? action) => action?.Kind == GameActionKind.HoldKey;

    private static double? Rate(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;
}

public static class PersonaContrastMarkdown
{
    public static string Render(PersonaContrastReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ARGUS — hypothetical persona-action differences")
            .AppendLine()
            .AppendLine("> **SHADOW ONLY.** Proposals were never executed. Differences do not measure human realism, game difficulty or win rates.")
            .AppendLine()
            .AppendLine($"Run: `{Safe(report.RunId)}`. Seed: `{report.Seed}`.")
            .AppendLine($"Frames: {report.TotalFrames} total; {report.GameplayFrames} visually classified as gameplay.")
            .AppendLine($"Gameplay observation coverage: player location {report.GameplayFramesWithPlayerLocation}/{report.GameplayFrames}; " +
                $"recorded confidence {report.GameplayFramesWithConfidence}/{report.GameplayFrames}; " +
                $"candidate coordinates {report.GameplayFramesWithCandidateLocations}/{report.GameplayFrames} " +
                $"(unknown: {report.GameplayFramesWithCandidateLocationsUnknown}); " +
                $"possible candidate objects {report.GameplayFramesWithCandidateObjects}/{report.GameplayFrames}.")
            .AppendLine()
            .AppendLine("## Per-persona proposals")
            .AppendLine()
            .AppendLine("| Persona | Proposed frames | Wait | Move | No proposal | Mean movement hold (ms) |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var p in report.Personas)
            builder.AppendLine($"| {Safe(p.PersonaId)} | {p.ComparedFrames} | {p.SuggestedWaits} | {p.SuggestedMoves} | {p.FramesWithoutProposal} | {Duration(p.MeanMoveDurationMs)} |");

        builder.AppendLine().AppendLine("## Pairwise differences").AppendLine()
            .AppendLine("| Pair | Both proposed | Different action kinds | Both moving | Different directions | Same direction, different duration | Any action difference | Any action difference rate | Direction difference rate |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var pair in report.Pairs)
            builder.AppendLine($"| {Safe(pair.FirstPersona)} vs {Safe(pair.SecondPersona)} | {pair.BothProposedFrames} | " +
                $"{pair.DifferentActionKinds} | {pair.BothMovingFrames} | {pair.DifferentDirections} | " +
                $"{pair.SameDirectionDifferentDurations} | {pair.DifferentFullActions} | {Percent(pair.FullActionDifferenceRate)} | {Percent(pair.DirectionDifferenceRate)} |");

        builder.AppendLine()
            .AppendLine("**Denominators matter:** direction disagreement is computed only when both personas proposed movement. " +
                "A wait-vs-move difference appears under action-kind disagreement. Duration changes alone are not direction changes.")
            .AppendLine("**Unknown perception:** missing candidate coordinates are not evidence of a threat-free scene. " +
                "Visible sprites have not been independently confirmed as enemies.")
            .AppendLine("**No learned behavior:** fixed traits and policies create proposed actions; no reinforcement, training, or alternative game inputs occurred.");
        return builder.ToString();
    }

    private static string Safe(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Replace("`", "");
    private static string Duration(double? ms) => ms?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A";
    private static string Percent(double? value) => value is null ? "N/A" : (value.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
