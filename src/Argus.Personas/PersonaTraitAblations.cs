using Argus.Core;
using Argus.Replay;

namespace Argus.Personas;

public sealed record TraitAblationResult(string Trait, int TotalFrames, int BothProposedFrames,
    int DifferentKinds, int BothMovingFrames, int DifferentDirections, int SameDirectionDifferentDurations,
    int DifferentActions, double? DirectionDifferenceRate, double? FullActionDifferenceRate);

public sealed record TraitAblationReport(string BasePersonaId, int Seed, string RandomStreamKey,
    int GameplayFrames, int FramesWithUnknownThreatEvidence, IReadOnlyList<TraitAblationResult> Traits);

/// <summary>
/// Controlled interventions: change exactly one normalized dimension between 0 and 1, replay the SAME
/// observations and identical random stream. No counterfactual game state, learning or executed actions.
/// </summary>
public static class PersonaTraitAblations
{
    private static readonly string[] Traits = ["riskAppetite", "exploration", "patience", "attention", "motorPrecision"];

    public static TraitAblationReport Analyze(ReplayTrace replay, PersonaProfile baseline, MovementBindings keys, int seed)
    {
        baseline.Validate();
        keys.Validate();
        if (replay.Frames.Count == 0) throw new InvalidDataException("Ablations require at least one recorded frame.");

        // One random stream for BOTH variations of EVERY trait. The differing persona IDs remain display-only.
        const string stream = "controlled-ablation-v1";
        var results = new List<TraitAblationResult>();
        foreach (var trait in Traits)
        {
            var low = Replace(baseline, trait, 0) with { Id = baseline.Id + "-low" };
            var high = Replace(baseline, trait, 1) with { Id = baseline.Id + "-high" };
            var both = 0;
            var kinds = 0;
            var moving = 0;
            var directions = 0;
            var durations = 0;
            var different = 0;

            foreach (var frame in replay.Frames)
            {
                var a = PersonaShadowPolicy.Propose(frame, low, keys, seed, stream).ProposedAction;
                var b = PersonaShadowPolicy.Propose(frame, high, keys, seed, stream).ProposedAction;
                if (a is null || b is null) continue;
                both++;
                if (a.Kind != b.Kind) kinds++;
                if (!Equals(a, b)) different++;
                if (a.Kind != GameActionKind.HoldKey || b.Kind != GameActionKind.HoldKey) continue;
                moving++;
                if (!string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)) directions++;
                else if (a.DurationMs != b.DurationMs) durations++;
            }

            results.Add(new TraitAblationResult(trait, replay.Frames.Count, both, kinds, moving, directions,
                durations, different, Rate(directions, moving), Rate(different, both)));
        }

        return new TraitAblationReport(baseline.Id, seed, stream,
            replay.Frames.Count(f => f.Phase == "exploring"),
            replay.Frames.Count(f => f.Phase == "exploring" && f.ThreatLocations is null), results);
    }

    private static PersonaProfile Replace(PersonaProfile original, string trait, float value) => trait switch
    {
        "riskAppetite" => original with { RiskAppetite = value },
        "exploration" => original with { Exploration = value },
        "patience" => original with { Patience = value },
        "attention" => original with { Attention = value },
        "motorPrecision" => original with { MotorPrecision = value },
        _ => throw new ArgumentOutOfRangeException(nameof(trait))
    };

    private static double? Rate(int changed, int eligible) => eligible == 0 ? null : (double)changed / eligible;
}
