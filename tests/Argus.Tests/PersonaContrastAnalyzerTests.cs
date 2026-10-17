using Argus.Core;
using Argus.Personas;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PersonaContrastAnalyzerTests
{
    private static readonly MovementBindings Keys = new("w", "a", "s", "d");

    [TestMethod]
    public void Analyze_NearbyVisualCandidate_DetectsOpposingDirectionsAcrossIdenticalFrames()
    {
        var replay = new ReplayTrace([Game(0), Game(1), Game(2)]);
        var personas = new[] { PersonaPresets.Default[0], PersonaPresets.Default[2] };

        var report = PersonaContrastAnalyzer.Analyze("real-unknown-game", replay, personas, Keys, 42);
        var pair = report.Pairs.Single();

        Assert.AreEqual(3, report.GameplayFrames);
        Assert.AreEqual(3, report.GameplayFramesWithCandidateLocations);
        Assert.AreEqual(3, report.GameplayFramesWithCandidateObjects);
        Assert.AreEqual(3, pair.BothProposedFrames);
        Assert.AreEqual(3, pair.BothMovingFrames);
        Assert.AreEqual(3, pair.DifferentDirections);
        Assert.AreEqual(0, pair.DifferentActionKinds);
        Assert.AreEqual(0, pair.SameDirectionDifferentDurations);
        Assert.AreEqual(3, pair.DifferentFullActions);
        Assert.AreEqual(1d, pair.DirectionDifferenceRate);
        Assert.AreEqual(1d, pair.FullActionDifferenceRate);
        StringAssert.Contains(PersonaContrastMarkdown.Render(report), "3/3");
    }

    [TestMethod]
    public void Analyze_SameDirectionDifferentTiming_DoesNotInflateDirectionDiversity()
    {
        var common = PersonaPresets.Default[2];
        var profiles = new[]
        {
            common with { Id = "long-hold", MotorPrecision = 0 },
            common with { Id = "short-hold", MotorPrecision = 1 }
        };
        var replay = new ReplayTrace([Game(0)]);

        var metrics = PersonaContrastAnalyzer.Analyze("trial", replay, profiles, Keys, 8).Pairs.Single();

        Assert.AreEqual(1, metrics.BothMovingFrames);
        Assert.AreEqual(0, metrics.DifferentDirections);
        Assert.AreEqual(1, metrics.SameDirectionDifferentDurations);
        Assert.AreEqual(1, metrics.DifferentFullActions);
        Assert.AreEqual(0d, metrics.DirectionDifferenceRate);
    }

    [TestMethod]
    public void Analyze_OnlyMenuFrames_ReportsUndefinedMovementRateNotFakeZero()
    {
        var replay = new ReplayTrace([Game(0) with { Phase = "menu" }, Game(1) with { Phase = "terminal" }]);

        var metrics = PersonaContrastAnalyzer.Analyze("menus", replay,
            [PersonaPresets.Default[0], PersonaPresets.Default[2]], Keys, 2);
        var pair = metrics.Pairs.Single();

        Assert.AreEqual(0, metrics.GameplayFrames);
        Assert.AreEqual(0, pair.BothProposedFrames);
        Assert.AreEqual(0, pair.BothMovingFrames);
        Assert.IsNull(pair.FullActionDifferenceRate);
        Assert.IsNull(pair.DirectionDifferenceRate);
        StringAssert.Contains(PersonaContrastMarkdown.Render(metrics), "N/A");
    }

    [TestMethod]
    public void Analyze_UnknownCandidateLocations_AreNotConsideredEmptyThreatFreeFrames()
    {
        var replay = new ReplayTrace([
            Game(0) with { ThreatLocations = null },
            Game(1) with { ThreatLocations = [] },
            Game(2) with { ThreatLocations = [new VisualCandidatePoint(100, 420)] }
        ]);
        var report = PersonaContrastAnalyzer.Analyze("mixed", replay, PersonaPresets.Default, Keys, 2);

        Assert.AreEqual(2, report.GameplayFramesWithCandidateLocations);
        Assert.AreEqual(1, report.GameplayFramesWithCandidateLocationsUnknown);
        Assert.AreEqual(1, report.GameplayFramesWithCandidateObjects);
        Assert.AreEqual(3, report.GameplayFramesWithPlayerLocation);
    }

    [TestMethod]
    public void Analyze_DeterministicAcrossRepeatedScoring()
    {
        var frames = new ReplayTrace([Game(0), Game(1) with { ThreatLocations = [] },
            Game(2) with { PlayerX = null, Confidence = null, ThreatLocations = null }]);
        var first = PersonaContrastAnalyzer.Analyze("seeded-run", frames, PersonaPresets.Default, Keys, 3141);
        var repeat = PersonaContrastAnalyzer.Analyze("seeded-run", frames, PersonaPresets.Default, Keys, 3141);

        for (var i = 0; i < first.Pairs.Count; i++)
            Assert.AreEqual(first.Pairs[i], repeat.Pairs[i]);
        for (var i = 0; i < first.Personas.Count; i++)
        {
            Assert.AreEqual(first.Personas[i].SuggestedMoves, repeat.Personas[i].SuggestedMoves);
            Assert.AreEqual(first.Personas[i].MeanMoveDurationMs, repeat.Personas[i].MeanMoveDurationMs);
        }
    }

    [TestMethod]
    public void Analyze_DuplicatePersonaIdsIgnoringCase_RejectsAmbiguousPairing()
    {
        var duplicate = PersonaPresets.Default[0] with { Id = "CONSERVATIVE" };
        Assert.ThrowsExactly<ArgumentException>(() =>
            PersonaContrastAnalyzer.Analyze("run", new ReplayTrace([Game(0)]),
                [PersonaPresets.Default[0], duplicate], Keys, 4));
    }

    [TestMethod]
    public void Analyze_ShortObservationAndFullActionCountsRemainConsistent()
    {
        var trace = new ReplayTrace([
            Game(0) with { PlayerX = null, PlayerY = null, Confidence = null, ThreatLocations = null },
            Game(1),
            Game(2) with { Phase = "choice" }
        ]);
        var report = PersonaContrastAnalyzer.Analyze("short", trace, PersonaPresets.Default, Keys, 4);

        Assert.AreEqual(3, report.TotalFrames);
        Assert.AreEqual(2, report.GameplayFrames);
        Assert.AreEqual(1, report.GameplayFramesWithPlayerLocation);
        Assert.AreEqual(1, report.GameplayFramesWithConfidence);
        foreach (var persona in report.Personas)
        {
            Assert.AreEqual(3, persona.ComparedFrames + persona.FramesWithoutProposal);
            Assert.AreEqual(persona.ComparedFrames, persona.SuggestedWaits + persona.SuggestedMoves);
        }
    }

    private static ReplayFrame Game(int step) =>
        new(step, $"frame-{step:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"),
            390, 844, "exploring", "Unverified visual candidate on left", 0.95f, "holdKey (a)",
            195, 420, 1) { ThreatLocations = [new VisualCandidatePoint(100, 420)] };
}
