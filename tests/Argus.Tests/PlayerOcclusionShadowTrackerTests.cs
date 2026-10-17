using Argus.Agents.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PlayerOcclusionShadowTrackerTests
{
    [TestMethod]
    public void Observe_TwoMeasuredScreenshotsBeforeOcclusion_ProvidesOnlyTwoLowConfidenceProposals()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        Assert.AreEqual("OBSERVED_UNARMED", tracker.Observe(Blob(194), true).Status);
        Assert.AreEqual("OBSERVED_ARMED", tracker.Observe(Blob(196), true).Status);

        var first = tracker.Observe(null, true);
        Assert.AreEqual("BRIDGE_ONE", first.Status);
        Assert.IsTrue(first.IsBridgeHypothesis);
        Assert.AreEqual(196f, first.LastKnownPosition!.X);
        Assert.AreEqual(0.35f, first.HypothesisConfidence);
        Assert.IsNull(first.ObservedPlayer);

        var second = tracker.Observe(null, true);
        Assert.AreEqual("BRIDGE_TWO", second.Status);
        Assert.AreEqual(0.15f, second.HypothesisConfidence);
        Assert.IsNull(second.ObservedPlayer);

        var expired = tracker.Observe(null, true);
        Assert.AreEqual("BRIDGE_EXPIRED", expired.Status);
        Assert.IsNull(expired.LastKnownPosition);
        Assert.IsFalse(expired.IsBridgeHypothesis);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", tracker.Observe(null, true).Status);
    }

    [TestMethod]
    public void Observe_SingleDetectionThenMissing_NeverArmsOrGuesses()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(195), true);
        var gap = tracker.Observe(null, true);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", gap.Status);
        Assert.IsNull(gap.LastKnownPosition);
        Assert.AreEqual("OBSERVED_UNARMED", tracker.Observe(Blob(195), true).Status);
    }

    [TestMethod]
    public void Observe_MenuTransitionImmediatelyInvalidatesStalePosition()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(195), true);
        tracker.Observe(Blob(194), true);
        Assert.AreEqual("BRIDGE_ONE", tracker.Observe(null, true).Status);
        Assert.AreEqual("NOT_COMBAT_PHASE", tracker.Observe(null, false).Status);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", tracker.Observe(null, true).Status);
    }

    [TestMethod]
    public void Observe_FarReappearanceResetsTrustRatherThanConfirmingDecorAsSameActor()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(195), true);
        tracker.Observe(Blob(194), true);
        tracker.Observe(null, true);
        var decoy = tracker.Observe(Blob(310), true);
        Assert.AreEqual("OBSERVED_DISCONTINUITY", decoy.Status);
        Assert.IsNull(decoy.LastKnownPosition);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", tracker.Observe(null, true).Status);
    }

    [TestMethod]
    public void Observe_LargeColorBlobReappearanceCannotBeConfirmedAsPreviousSprite()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(194), true);
        tracker.Observe(Blob(195), true);
        tracker.Observe(null, true);
        var strange = tracker.Observe(Blob(196) with { Width = 60 }, true);
        Assert.AreEqual("OBSERVED_DISCONTINUITY", strange.Status);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", tracker.Observe(null, true).Status);
    }

    [TestMethod]
    public void Observe_NearReappearanceDoesNotImmediatelyAuthorizeAnotherGap()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(194), true);
        tracker.Observe(Blob(195), true);
        tracker.Observe(null, true);
        var returned = tracker.Observe(Blob(197), true);
        Assert.AreEqual("REAPPEARED_NEAR_PRIOR", returned.Status);
        Assert.IsNull(returned.LastKnownPosition);
        Assert.AreEqual("NO_CONFIRMED_PRIOR", tracker.Observe(null, true).Status);
    }

    [TestMethod]
    public void Observe_AbsentActualPlayerCanStillCauseFalseShadowProposals_ProvesMustNotActivate()
    {
        var tracker = new PlayerOcclusionShadowTracker();
        tracker.Observe(Blob(195), true);
        tracker.Observe(Blob(196), true);

        // No pixels from a player actually exist on these frames. This heuristic
        // CANNOT know that: both proposals are WRONG against external human truth.
        var noPlayerFirst = tracker.Observe(null, true);
        var noPlayerSecond = tracker.Observe(null, true);
        Assert.IsTrue(noPlayerFirst.IsBridgeHypothesis);
        Assert.IsTrue(noPlayerSecond.IsBridgeHypothesis);
        Assert.IsNull(noPlayerFirst.ObservedPlayer);
        Assert.IsNull(noPlayerSecond.ObservedPlayer);
        Assert.AreEqual("BRIDGE_EXPIRED", tracker.Observe(null, true).Status);
    }

    private static VisualBlob Blob(float x) => new(x, 420, 20, 30, 220, 0.75f);
}
