using Argus.Agents.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class ShadowTemporalCandidateTrackerTests
{
    [TestMethod]
    public void Observe_TwoMovingFrames_ConfirmsSupplementWithoutChangingBaseline()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        VisualBlob[] baseline = [Blob(100, 400)];
        var first = tracker.Observe(Frame([Blob(300, 170)], baseline));
        var second = tracker.Observe(Frame([Blob(309, 172)], baseline));

        Assert.HasCount(0, first.Confirmed);
        Assert.AreEqual(1, first.PendingCandidates);
        Assert.HasCount(1, second.Confirmed);
        Assert.AreEqual(309f, second.Confirmed[0].X);
        Assert.AreEqual(0, second.StationaryCandidates);
        Assert.AreEqual(100f, baseline[0].X);
    }

    [TestMethod]
    public void Observe_StationaryDecorAndTransientParticle_DoNotConfirm()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        tracker.Observe(Frame([Blob(350, 170), Blob(270, 190)]));
        var current = tracker.Observe(Frame([Blob(350, 170), Blob(210, 250)]));

        Assert.HasCount(0, current.Confirmed);
        Assert.AreEqual(1, current.StationaryCandidates);
        Assert.AreEqual(1, current.PendingCandidates);
    }

    [TestMethod]
    public void Observe_TwoPriorTracksToOneCandidate_AssociatesOnlyOnce()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        tracker.Observe(Frame([Blob(300, 165), Blob(325, 165)]));
        var result = tracker.Observe(Frame([Blob(313, 169)]));

        Assert.HasCount(1, result.Confirmed);
    }

    [TestMethod]
    public void Observe_SuddenTeleport_RequiresNewConsecutiveObservation()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        tracker.Observe(Frame([Blob(250, 165)]));
        var result = tracker.Observe(Frame([Blob(350, 165)]));

        Assert.HasCount(0, result.Confirmed);
        Assert.AreEqual(1, result.PendingCandidates);
    }

    [TestMethod]
    public void Observe_LostPlayerAndTransition_ResetTracks()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        tracker.Observe(Frame([Blob(280, 165)]));
        var lost = tracker.Observe(new ShadowThreatObservation(new CombatVisualObservation(null, []), [Blob(285, 165)]));
        var restart = tracker.Observe(Frame([Blob(292, 165)]));
        tracker.Reset();
        var menuRestart = tracker.Observe(Frame([Blob(298, 165)]));

        Assert.HasCount(0, lost.Confirmed);
        Assert.HasCount(0, restart.Confirmed);
        Assert.HasCount(0, menuRestart.Confirmed);
        Assert.AreEqual(1, menuRestart.PendingCandidates);
    }

    [TestMethod]
    public void Observe_GradualMotion_ConfirmsAfterThreeFrames()
    {
        var tracker = new ShadowTemporalCandidateTracker();
        tracker.Observe(Frame([Blob(300, 165)]));
        var slow = tracker.Observe(Frame([Blob(301, 165)]));
        var third = tracker.Observe(Frame([Blob(304, 165)]));

        Assert.HasCount(0, slow.Confirmed);
        Assert.AreEqual(1, slow.StationaryCandidates);
        Assert.HasCount(1, third.Confirmed);
    }

    private static VisualBlob Blob(float x, float y) => new(x, y, 19, 27, 110, 0.45f);

    private static ShadowThreatObservation Frame(IReadOnlyList<VisualBlob> additions,
        IReadOnlyList<VisualBlob>? baseline = null) =>
        new(new CombatVisualObservation(Blob(195, 420), baseline ?? []), additions);
}
