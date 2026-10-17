using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class FrameDecisionCaptureTests
{
    [TestMethod]
    public async Task CaptureAsync_StepLimit_UsesCurrentFrameEvidenceButSuppressesInput()
    {
        var agent = new FrameTaggedAgent();
        var first = new FrameObservation(DateTimeOffset.UnixEpoch, 390, 844, [41]);
        var last = new FrameObservation(DateTimeOffset.UnixEpoch.AddSeconds(1), 390, 844, [42]);

        var executed = await FrameDecisionCapture.CaptureAsync(agent, first, false, TestContext.CancellationToken);
        var observedOnly = await FrameDecisionCapture.CaptureAsync(agent, last, true, TestContext.CancellationToken);

        Assert.IsNotNull(executed.Action);
        Assert.IsFalse(executed.InputSuppressedAtStepLimit);
        Assert.AreEqual(41f, executed.Evidence!.Combat!.PlayerX);
        Assert.IsNull(observedOnly.Action);
        Assert.IsTrue(observedOnly.InputSuppressedAtStepLimit);
        Assert.AreEqual(42f, observedOnly.Evidence!.Combat!.PlayerX);
        Assert.AreEqual(2, agent.Calls);
    }

    [TestMethod]
    public async Task CaptureAsync_ObservationOnlyCandidate_DoesNotCountAsExecutedAvoidance()
    {
        var agent = new FrameTaggedAgent();
        var frame = new FrameObservation(DateTimeOffset.UnixEpoch, 390, 844, [42]);
        var capture = await FrameDecisionCapture.CaptureAsync(agent, frame, true, TestContext.CancellationToken);
        var collector = new RunEvidenceCollector();

        collector.Record(capture.Action, capture.Evidence);

        Assert.AreEqual(1, collector.Snapshot().PlayerLocatedFrames);
        Assert.AreEqual(0, collector.Snapshot().CandidateAvoidanceFrames);
        Assert.AreEqual(0, collector.Snapshot().DistinctMovementKeys);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class FrameTaggedAgent : IVisualAgent, IAgentEvidenceProvider
    {
        public AgentEvidence? LastEvidence { get; private set; }
        public int Calls { get; private set; }

        public ValueTask<GameAction?> DecideAsync(FrameObservation observation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            var x = observation.PngBytes[0];
            LastEvidence = new AgentEvidence("exploring", "synthetic screenshot", 0.8f, [],
                Combat: new CombatScreenEvidence(x, 420, 1, 25, "candidate-avoidance"));
            return ValueTask.FromResult<GameAction?>(new GameAction(GameActionKind.HoldKey, Key: "d", DurationMs: 300));
        }
    }
}
