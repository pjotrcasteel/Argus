using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class RunEvidenceCollectorTests
{
    [TestMethod]
    public void Snapshot_ActualScreenInteractionsAndPlayerFrames_MeetsM1Gate()
    {
        var collector = new RunEvidenceCollector();
        collector.Record(new GameAction(GameActionKind.Click), Evidence("menu"));
        collector.Record(new GameAction(GameActionKind.Click), Evidence("route"));
        collector.Record(new GameAction(GameActionKind.Click), Evidence("route"));
        for (var i = 0; i < 5; i++)
        {
            collector.Record(new GameAction(GameActionKind.HoldKey, Key: i % 2 == 0 ? "a" : "d"),
                Evidence("exploring", new CombatScreenEvidence(195, 420, 2, 64, "candidate-avoidance")));
        }
        collector.Record(new GameAction(GameActionKind.Click), Evidence("choice"));
        collector.Record(null, Evidence("terminal"));

        var summary = collector.Snapshot();

        Assert.IsTrue(summary.MeetsVisualCombatGate);
        Assert.AreEqual(5, summary.PlayerLocatedFrames);
        Assert.AreEqual(1, summary.UpgradeClicks);
        Assert.AreEqual(1, summary.TerminalStops);
        Assert.AreEqual(2, summary.DistinctMovementKeys);
        Assert.AreEqual(0, summary.MissingPlayerExplorationFrames);
    }

    [TestMethod]
    public void Snapshot_OnlyMenuAndRouteClicks_DoesNotClaimCombat()
    {
        var collector = new RunEvidenceCollector();
        collector.Record(new GameAction(GameActionKind.Click), Evidence("menu"));
        collector.Record(new GameAction(GameActionKind.Click), Evidence("route"));
        collector.Record(new GameAction(GameActionKind.Click), Evidence("route"));
        collector.Record(new GameAction(GameActionKind.HoldKey, Key: "w"), Evidence("exploring"));

        var summary = collector.Snapshot();

        Assert.IsFalse(summary.MeetsVisualCombatGate);
        Assert.AreEqual(0, summary.PlayerLocatedFrames);
        Assert.AreEqual(1, summary.MissingPlayerExplorationFrames);
    }

    [TestMethod]
    public void Snapshot_BrowserLoadsButNoGameActions_DoesNotPassGate()
    {
        var collector = new RunEvidenceCollector();
        collector.Record(new GameAction(GameActionKind.Wait), Evidence("unrecognized"));

        Assert.IsFalse(collector.Snapshot().MeetsVisualCombatGate);
    }

    private static AgentEvidence Evidence(string phase, CombatScreenEvidence? combat = null) =>
        new(phase, "synthetic unit test", 0.75f, [], Combat: combat);
}
