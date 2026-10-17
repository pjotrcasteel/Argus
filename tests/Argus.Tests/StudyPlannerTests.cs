using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class StudyPlannerTests
{
    [TestMethod]
    public void Create_SeparateEntireRuns_PreservesDeterministicPartitionAndFrameSelection()
    {
        var recordings = new[] { Run("b", 41), Run("a", 57), Run("d", 19), Run("c", 65) };

        var first = StudyPlanner.Create(recordings, "study-2026-a", 12);
        var second = StudyPlanner.Create(recordings.Reverse(), "study-2026-a", 12);

        Assert.AreEqual(1, first.SchemaVersion);
        Assert.HasCount(4, first.Assignments);
        Assert.HasCount(2, first.Assignments.Where(r => r.Partition == "held-out").ToArray());
        Assert.HasCount(2, first.Assignments.Where(r => r.Partition == "development").ToArray());
        for (var i = 0; i < first.Assignments.Count; i++)
        {
            var actual = first.Assignments[i];
            var repeat = second.Assignments[i];
            Assert.AreEqual(actual.RunId, repeat.RunId);
            Assert.AreEqual(actual.Partition, repeat.Partition);
            CollectionAssert.AreEqual(actual.FrameSteps.ToArray(), repeat.FrameSteps.ToArray());
            Assert.HasCount(12, actual.FrameSteps);
            Assert.AreEqual(12, actual.FrameSteps.Distinct().Count());
        }
    }

    [TestMethod]
    public void Create_ChronologicalBins_SpreadsSamplesAcrossWholeSession()
    {
        var plan = StudyPlanner.Create([Run("alpha", 100), Run("beta", 100)], "seed", 10);

        foreach (var run in plan.Assignments)
        {
            for (var i = 0; i < 10; i++)
            {
                Assert.IsTrue(run.FrameSteps[i] >= 10 * i);
                Assert.IsTrue(run.FrameSteps[i] < 10 * (i + 1));
            }
        }
    }

    [TestMethod]
    public void Create_IdenticalRunIdIgnoringCase_FailsRatherThanDoubleCount()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            StudyPlanner.Create([Run("session1", 10), Run("SESSION1", 10)], "seed"));
    }

    [TestMethod]
    public void Create_NotEnoughIndependentRuns_Fails()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => StudyPlanner.Create([Run("single", 10)], "seed"));
    }

    [TestMethod]
    public void Create_InvalidRunId_Fails()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            StudyPlanner.Create([Run("../escape", 10), Run("valid", 10)], "seed"));
    }

    [TestMethod]
    public void Template_AllHumanLabelsStartUnknown_AndCanBeValidated()
    {
        var inputs = new[] { Run("alpha", 8), Run("beta", 13) };
        var plan = StudyPlanner.Create(inputs, "fixed-seed", 6);

        foreach (var assignment in plan.Assignments)
        {
            var trace = inputs.Single(r => r.RunId == assignment.RunId).Trace;
            var labels = StudyPlanner.Template(assignment, trace);
            GroundTruth.Validate(labels, assignment.RunId, trace);
            Assert.IsTrue(labels.Frames.All(f => f.Player.State == "unknown" &&
                f.Player.Center is null && f.Threats.State == "unknown" && f.Threats.Centers is null));
        }
    }

    [TestMethod]
    public void Render_BlindMode_OmitsRecordedAgentPredictionsAndReasonsFromEmbeddedHtml()
    {
        const string secretReason = "secret-prediction-always-steer-right";
        var trace = new ReplayTrace([
            Frame(0, secretReason) with
            {
                PlayerX = 371,
                PlayerY = 813,
                ThreatCandidates = 99,
                ThreatLocations = [new VisualCandidatePoint(337, 711)]
            }
        ]);
        var labels = new LabelSet(1, "sample", [
            new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))
        ]);

        var blind = AnnotationPageBuilder.Render(trace, labels, blind: true);
        var ordinary = AnnotationPageBuilder.Render(trace, labels);

        StringAssert.Contains(blind, "BLIND REVIEW");
        StringAssert.Contains(blind, "Agent evidence is hidden");
        Assert.IsFalse(blind.Contains(secretReason, StringComparison.Ordinal));
        Assert.IsFalse(blind.Contains("\"playerX\":371", StringComparison.Ordinal));
        Assert.IsFalse(blind.Contains("\"threatLocations\":[", StringComparison.Ordinal));
        Assert.IsFalse(blind.Contains("\"confidence\":0.5", StringComparison.Ordinal));
        StringAssert.Contains(ordinary, secretReason);
        StringAssert.Contains(ordinary, "\"playerX\":371");
        Assert.AreEqual("unknown", labels.Frames[0].Player.State);
    }

    private static StudyRun Run(string id, int count) =>
        new(id, new ReplayTrace(Enumerable.Range(0, count).Select(i => Frame(i, "unknown agent reason")).ToArray()));

    private static ReplayFrame Frame(int index, string reason) =>
        new(index, $"frame-{index:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"),
            390, 844, index % 2 == 0 ? "exploring" : "menu", reason, 0.5f, "right", 200, 400, 1);
}
