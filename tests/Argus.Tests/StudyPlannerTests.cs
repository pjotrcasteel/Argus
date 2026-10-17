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
    public void CreateDevelopment_IndependentRuns_AllDevelopmentWithStableTemporalSamples()
    {
        var runs = new[] { Run("dev-b", 43), Run("dev-a", 51) };
        var first = StudyPlanner.CreateDevelopment(runs, "m26-predeclared", 12);
        var second = StudyPlanner.CreateDevelopment(runs.Reverse(), "m26-predeclared", 12);

        Assert.AreEqual("development-only", first.Mode);
        Assert.HasCount(2, first.Assignments);
        Assert.IsTrue(first.Assignments.All(a => a.Partition == "development"));
        Assert.IsTrue(first.Assignments.All(a => a.FrameSteps.Count == 12));
        for (var i = 0; i < first.Assignments.Count; i++)
        {
            CollectionAssert.AreEqual(first.Assignments[i].FrameSteps.ToArray(), second.Assignments[i].FrameSteps.ToArray());
            var trace = runs.Single(run => run.RunId == first.Assignments[i].RunId).Trace;
            var labels = StudyPlanner.Template(first.Assignments[i], trace);
            Assert.IsTrue(labels.Frames.All(f => f.Player.State == "unknown" && f.Threats.State == "unknown"));
            for (var j = 0; j < 12; j++)
            {
                var low = j * trace.Frames.Count / 12;
                var high = (j + 1) * trace.Frames.Count / 12;
                Assert.IsTrue(first.Assignments[i].FrameSteps[j] >= low);
                Assert.IsTrue(first.Assignments[i].FrameSteps[j] < high);
            }
        }
    }

    [TestMethod]
    public void CreateDevelopment_ThreeIndependentRuns_UsesFrozenDevelopmentOnlyTemporalBins()
    {
        StudyRun[] runs = [Run("dev-e", 49), Run("dev-c", 45), Run("dev-d", 44)];
        var planned = StudyPlanner.CreateDevelopment(runs, "argus-m27-independent-development-frozen-20261017", 12);
        var repeated = StudyPlanner.CreateDevelopment(runs.Reverse(), "argus-m27-independent-development-frozen-20261017", 12);

        Assert.AreEqual("development-only", planned.Mode);
        Assert.HasCount(3, planned.Assignments);
        CollectionAssert.AreEqual(new[] { "dev-c", "dev-d", "dev-e" },
            planned.Assignments.Select(a => a.RunId).ToArray());
        for (var i = 0; i < 3; i++)
        {
            var actual = planned.Assignments[i];
            Assert.AreEqual("development", actual.Partition);
            Assert.HasCount(12, actual.FrameSteps);
            CollectionAssert.AreEqual(actual.FrameSteps.ToArray(), repeated.Assignments[i].FrameSteps.ToArray());
            var trace = runs.Single(run => run.RunId == actual.RunId).Trace;
            var reference = StudyPlanner.Template(actual, trace);
            Assert.IsTrue(reference.Frames.All(f => f.Player.State == "unknown" && f.Threats.State == "unknown"));
            Assert.IsTrue(actual.FrameSteps.SequenceEqual(actual.FrameSteps.Order()));
        }
    }

    [TestMethod]
    public void CreateDevelopment_UnsafeAndRepeatedRunIds_RejectsWithoutTouchingHeldOut()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            StudyPlanner.CreateDevelopment([Run("same", 10), Run("SAME", 9)], "m26", 6));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            StudyPlanner.CreateDevelopment([Run("../escape", 10), Run("dev-b", 10)], "m26", 6));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            StudyPlanner.CreateDevelopment([Run("dev-a", 10)], "m26", 6));
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
