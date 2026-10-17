using Argus.Core;
using Argus.Personas;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PersonaCohortStudyTests
{
    private static readonly MovementBindings Keys = new("w", "a", "s", "d");

    [TestMethod]
    public void Analyze_WholeRuns_ProducesUnweightedPerRunMedians()
    {
        var first = Session("run-a", [Scene(0), Scene(1), Scene(2)]);
        var second = Session("run-b", [Scene(0) with { Phase = "menu" }, Scene(1) with { Phase = "terminal" }]);
        var report = PersonaCohortStudy.Analyze([second, first], 42, Keys);

        Assert.AreEqual(2, report.RunCount);
        Assert.AreEqual("run-a", report.Runs[0].RunId);
        Assert.AreEqual("run-b", report.Runs[1].RunId);
        Assert.IsFalse(report.MatchedContext);
        Assert.HasCount(3, report.Pairs);
        Assert.HasCount(5, report.Traits);
        var extreme = report.Pairs.Single(p => p.FirstPersona == "conservative" && p.SecondPersona == "daredevil");
        Assert.AreEqual(2, extreme.DirectionDifferences.TotalRuns);
        Assert.AreEqual(1, extreme.DirectionDifferences.EligibleRuns);
        Assert.AreEqual(1d, extreme.DirectionDifferences.Median);
        Assert.AreEqual(1d, extreme.DirectionDifferences.Min);
        Assert.AreEqual(1d, extreme.DirectionDifferences.Max);
        Assert.IsNull(report.Runs[1].Contrast.Pairs.Single(p =>
            p.FirstPersona == "conservative" && p.SecondPersona == "daredevil").DirectionDifferenceRate);
        StringAssert.Contains(PersonaCohortMarkdown.Render(report), "N/A");
    }

    [TestMethod]
    public void Distribution_ThreeRunRates_ComputesMedianRatherThanPooledFrames()
    {
        var x = PersonaCohortStudy.Distribution([0.1, 0.9, 0.2]);

        Assert.AreEqual(3, x.TotalRuns);
        Assert.AreEqual(3, x.EligibleRuns);
        Assert.AreEqual(0.1, x.Min);
        Assert.AreEqual(0.2, x.Median);
        Assert.AreEqual(0.9, x.Max);
        Assert.IsTrue(Math.Abs(x.UnweightedMean!.Value - 0.4) < 1e-9);
    }

    [TestMethod]
    public void Distribution_NoEligibleRun_RetainsUnknownRatherThanZero()
    {
        var none = PersonaCohortStudy.Distribution([null, null]);

        Assert.AreEqual(0, none.EligibleRuns);
        Assert.IsNull(none.Median);
        Assert.IsNull(none.Min);
        Assert.IsNull(none.UnweightedMean);
    }

    [TestMethod]
    public void Analyze_MatchedContextRequiresCompleteIdenticalMetadata()
    {
        var metadata = new PersonaSessionContext("v1", "visual-mobile", "390x844", "sha-1");
        var runs = new[] { Session("a", [Scene(0)]) with { Context = metadata },
            Session("b", [Scene(0)]) with { Context = metadata } };

        Assert.IsTrue(PersonaCohortStudy.Analyze(runs, 42, Keys).MatchedContext);
        Assert.IsFalse(PersonaCohortStudy.Analyze([
            runs[0], runs[1] with { Context = metadata with { Viewport = null } }
        ], 42, Keys).MatchedContext);
        Assert.IsFalse(PersonaCohortStudy.Analyze([
            runs[0], runs[1] with { Context = metadata with { GameVersion = "v2" } }
        ], 42, Keys).MatchedContext);
    }

    [TestMethod]
    public void Analyze_DuplicateRunNamesIgnoringCase_RejectsClaimOfTwoSamples()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PersonaCohortStudy.Analyze([
            Session("run", [Scene(0)]), Session("RUN", [Scene(0)])
        ], 42, Keys));
    }

    [TestMethod]
    public void Analyze_OneRun_RejectsImproperCohort()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PersonaCohortStudy.Analyze([Session("one", [Scene(0)])], 42, Keys));
    }

    [TestMethod]
    public void TraitAblations_RiskAndMotorPrecision_HaveDistinctDirectionalAndTimingEffects()
    {
        var trace = new ReplayTrace([Scene(0)]);
        var baseProfile = PersonaPresets.Default[1] with { Attention = 0, Patience = 0 };

        var report = PersonaTraitAblations.Analyze(trace, baseProfile, Keys, 42);
        var risk = report.Traits.Single(x => x.Trait == "riskAppetite");
        var motor = report.Traits.Single(x => x.Trait == "motorPrecision");
        var patience = report.Traits.Single(x => x.Trait == "patience");

        Assert.AreEqual(1, risk.BothMovingFrames);
        Assert.AreEqual(1, risk.DifferentDirections);
        Assert.AreEqual(1, motor.SameDirectionDifferentDurations);
        Assert.AreEqual(0, motor.DifferentDirections);
        Assert.AreEqual(1, motor.DifferentActions);
        Assert.AreEqual(0, patience.DifferentActions); // no patience decisions in close-threat branch
    }

    [TestMethod]
    public void TraitAblations_SameRandomStream_PreventsDisplayNameFromInfluencingEffect()
    {
        var frames = new ReplayTrace(Enumerable.Range(0, 18).Select(i =>
            Scene(i) with { ThreatLocations = [], ThreatCandidates = 0 }).ToArray());
        var baseProfile = PersonaPresets.Default[1] with { Attention = 0, Patience = 0 };
        var renamed = baseProfile with { Id = "a-completely-different-visible-name" };

        var first = PersonaTraitAblations.Analyze(frames, baseProfile, Keys, 1337);
        var second = PersonaTraitAblations.Analyze(frames, renamed, Keys, 1337);

        CollectionAssert.AreEqual(first.Traits.ToArray(), second.Traits.ToArray());
        Assert.AreEqual("controlled-ablation-v1", first.RandomStreamKey);
    }

    [TestMethod]
    public void TraitAblations_AttentionAndExploration_AreObservableOnlyWhenRelevant()
    {
        var frames = new ReplayTrace(Enumerable.Range(0, 30).Select(i =>
            Scene(i) with { ThreatLocations = [], ThreatCandidates = 0, Confidence = 0.4f }).ToArray());
        var baseProfile = PersonaPresets.Default[1] with { Patience = 0 };
        var report = PersonaTraitAblations.Analyze(frames, baseProfile, Keys, 29);

        Assert.IsTrue(report.Traits.Single(t => t.Trait == "attention").DifferentKinds > 0);
        Assert.IsTrue(report.Traits.Single(t => t.Trait == "exploration").DifferentDirections > 0);
        Assert.AreEqual(0, report.Traits.Single(t => t.Trait == "riskAppetite").DifferentDirections);
    }

    [TestMethod]
    public void Propose_ControlledRandomStream_IsIndependentOfProfileId()
    {
        var profile = PersonaPresets.Default[1] with { Patience = 0, Attention = 0 };
        var a = PersonaShadowPolicy.Propose(Scene(11) with { ThreatLocations = [] },
            profile with { Id = "alpha" }, Keys, 99, "fixed");
        var b = PersonaShadowPolicy.Propose(Scene(11) with { ThreatLocations = [] },
            profile with { Id = "beta" }, Keys, 99, "fixed");

        Assert.AreEqual(a.ProposedAction, b.ProposedAction);
        Assert.AreNotEqual(a.PersonaId, b.PersonaId);
    }

    private static PersonaCohortSession Session(string id, ReplayFrame[] frames) =>
        new(id, new ReplayTrace(frames), new PersonaSessionContext());

    private static ReplayFrame Scene(int step) =>
        new(step, $"frame-{step:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"),
            390, 844, "exploring", "candidate unverified", 0.95f, "holdKey (a)", 195, 420, 1)
        {
            ThreatLocations = [new VisualCandidatePoint(100, 420)]
        };
}
