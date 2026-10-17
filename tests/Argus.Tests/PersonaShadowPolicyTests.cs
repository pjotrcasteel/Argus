using Argus.Core;
using Argus.Personas;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PersonaShadowPolicyTests
{
    private static readonly MovementBindings Keys = new("w", "a", "s", "d");

    [TestMethod]
    public void Propose_SameVisualThreat_ConservativeRetreatsWhileDaredevilApproaches()
    {
        var frame = Combat(7, confidence: 0.95f, new VisualCandidatePoint(100, 420));
        var conservative = PersonaShadowPolicy.Propose(frame, PersonaPresets.Default[0], Keys, 42);
        var daredevil = PersonaShadowPolicy.Propose(frame, PersonaPresets.Default[2], Keys, 42);

        Assert.AreEqual(GameActionKind.HoldKey, conservative.ProposedAction?.Kind);
        Assert.AreEqual(GameActionKind.HoldKey, daredevil.ProposedAction?.Kind);
        Assert.AreEqual("d", conservative.ProposedAction?.Key);
        Assert.AreEqual("a", daredevil.ProposedAction?.Key);
        StringAssert.Contains(conservative.Reason, "unverified");
        StringAssert.Contains(daredevil.Reason, "unverified");
    }

    [TestMethod]
    public void Propose_IdenticalInputsAndSeed_ReproduceExactProposals()
    {
        var frame = Combat(13, 0.84f);
        foreach (var preset in PersonaPresets.Default)
        {
            var first = PersonaShadowPolicy.Propose(frame, preset, Keys, 818);
            var again = PersonaShadowPolicy.Propose(frame, preset, Keys, 818);
            Assert.AreEqual(first, again);
        }
    }

    [TestMethod]
    public void Propose_UncertainPosition_AllPersonasWaitInsteadOfActingBlindly()
    {
        var frame = Combat(2, 0.08f) with { PlayerX = null, PlayerY = null, ThreatLocations = null };
        foreach (var p in PersonaPresets.Default)
        {
            var action = PersonaShadowPolicy.Propose(frame, p, Keys, 123);
            Assert.AreEqual(GameActionKind.Wait, action.ProposedAction?.Kind);
            Assert.AreEqual(0.08f, action.EvidenceConfidence);
        }
    }

    [TestMethod]
    public void Propose_MenuAndTerminal_NeverSuggestMovementOrClicks()
    {
        foreach (var phase in new[] { "menu", "choice", "route", "terminal", "unknown" })
        {
            var frame = Combat(3, 0.99f, new VisualCandidatePoint(100, 420)) with { Phase = phase };
            foreach (var p in PersonaPresets.Default)
                Assert.IsNull(PersonaShadowPolicy.Propose(frame, p, Keys, 19).ProposedAction);
        }
    }

    [TestMethod]
    public void Propose_MotorPrecision_AffectsProposalHoldDurationNotHiddenGameState()
    {
        var frame = Combat(7, 0.95f, new VisualCandidatePoint(100, 420));
        var unskilled = PersonaPresets.Default[2] with { MotorPrecision = 0 };
        var practiced = PersonaPresets.Default[2] with { MotorPrecision = 1 };

        var slow = PersonaShadowPolicy.Propose(frame, unskilled, Keys, 25);
        var fast = PersonaShadowPolicy.Propose(frame, practiced, Keys, 25);

        Assert.AreEqual(420, slow.ProposedAction?.DurationMs);
        Assert.AreEqual(120, fast.ProposedAction?.DurationMs);
    }

    [TestMethod]
    public void Validate_InvalidTraitsAndDuplicateMovementKeys_Rejects()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            (PersonaPresets.Default[0] with { RiskAppetite = float.NaN }).Validate());
        Assert.ThrowsExactly<ArgumentException>(() =>
            (PersonaPresets.Default[0] with { Attention = 1.1f }).Validate());
        Assert.ThrowsExactly<ArgumentException>(() => new MovementBindings("w", "w", "s", "d").Validate());
    }

    [TestMethod]
    public void Propose_UnknownThreatLocations_NeverClaimsNoEnemies()
    {
        var frame = Combat(7, 0.95f) with { ThreatLocations = null };
        foreach (var p in PersonaPresets.Default)
        {
            var proposal = PersonaShadowPolicy.Propose(frame, p, Keys, 37);
            Assert.IsFalse(proposal.Reason.Contains("no enemies", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(proposal.Reason.Contains("zero threats", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static ReplayFrame Combat(int step, float confidence, params VisualCandidatePoint[] threats) =>
        new(step, $"frame-{step:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"),
            390, 844, "exploring", "visual evidence only", confidence, "holdKey (w)", 195, 420, threats.Length)
        {
            ThreatLocations = threats
        };
}
