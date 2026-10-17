using System.Text.Json;
using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class StudyQualityEvaluationTests
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [TestMethod]
    public async Task EvaluateAsync_NoHumanLabels_ReturnsNotEvaluatedForBothRealPartitions()
    {
        var root = await FixtureAsync();
        try
        {
            var quality = await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual("NOT_EVALUATED", quality.Status);
            Assert.HasCount(2, quality.Runs);
            Assert.IsTrue(quality.Runs.All(run => run.Status == "NOT_EVALUATED" && run.Perception is null));
            Assert.AreEqual(1, quality.Partitions.Single(p => p.Partition == "development").Runs);
            Assert.AreEqual(1, quality.Partitions.Single(p => p.Partition == "held-out").Runs);
            Assert.IsTrue(quality.Partitions.All(p => p.PlayerPrecision.Median is null &&
                p.ThreatPrecision.Median is null && p.RunsWithHumanReview == 0));
            StringAssert.Contains(StudyQualityEvaluation.RenderMarkdown(quality), "NOT_EVALUATED");
            StringAssert.Contains(StudyQualityEvaluation.RenderMarkdown(quality), "N/A");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_DevelopmentOnlyPlan_RequiresRealHumanReviewWithoutSyntheticHeldOut()
    {
        var root = await FixtureAsync();
        try
        {
            var runs = new List<StudyRun>();
            foreach (var name in new[] { "real-a", "real-b" })
                runs.Add(new StudyRun(name, await ReplayTraceReader.ReadAsync(Path.Combine(root, name),
                    TestContext.CancellationToken)));
            var plan = StudyPlanner.CreateDevelopment(runs, "fresh-development-only", 2);
            await File.WriteAllTextAsync(Path.Combine(root, "study-plan.json"), JsonSerializer.Serialize(plan, Json),
                TestContext.CancellationToken);

            var report = await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual("NOT_EVALUATED", report.Status);
            Assert.HasCount(2, report.Runs);
            Assert.IsTrue(report.Runs.All(r => r.Partition == "development" && r.Perception is null));
            Assert.AreEqual(2, report.Partitions.Single(g => g.Partition == "development").Runs);
            Assert.AreEqual(0, report.Partitions.Single(g => g.Partition == "held-out").Runs);
            Assert.IsNull(report.Partitions.Single(g => g.Partition == "held-out").ThreatPrecision.Median);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_AlteredDevelopmentSample_RejectsCherryPicking()
    {
        var root = await FixtureAsync();
        try
        {
            var runs = new List<StudyRun>();
            foreach (var name in new[] { "real-a", "real-b" })
                runs.Add(new StudyRun(name, await ReplayTraceReader.ReadAsync(Path.Combine(root, name),
                    TestContext.CancellationToken)));
            var plan = StudyPlanner.CreateDevelopment(runs, "fresh-development-only", 2);
            var changed = plan with
            {
                Assignments = plan.Assignments.Select((a, i) => i != 0 ? a :
                    a with { FrameSteps = a.FrameSteps.Reverse().ToArray() }).ToArray()
            };
            await File.WriteAllTextAsync(Path.Combine(root, "study-plan.json"), JsonSerializer.Serialize(changed, Json),
                TestContext.CancellationToken);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_OnePartiallyHumanReviewedRun_PreservesUnknownAndOtherRunNotEvaluated()
    {
        var root = await FixtureAsync();
        try
        {
            var plan = await ReadPlanAsync(root);
            var first = plan.Assignments[0];
            var frames = first.FrameSteps.Order().Select((step, index) =>
                new FrameLabel(step,
                    index == 0 ? new PlayerLabel("visible", Point(195, 420)) : new PlayerLabel("unknown", null),
                    index == 0 ? new ThreatLabel("known", [Point(80, 300)]) : new ThreatLabel("unknown", null))).ToArray();
            await SaveLabelsAsync(root, first.RunId, frames);

            var report = await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken);
            var reviewed = report.Runs.Single(run => run.RunId == first.RunId);
            var untouched = report.Runs.Single(run => run.RunId != first.RunId);

            Assert.AreEqual("PARTIALLY_REVIEWED", report.Status);
            Assert.AreEqual("PARTIALLY_REVIEWED", reviewed.Status);
            Assert.AreEqual(1, reviewed.HumanReviewedPlayerFrames);
            Assert.AreEqual(1, reviewed.HumanReviewedThreatFrames);
            Assert.IsNotNull(reviewed.Perception);
            Assert.AreEqual(1, reviewed.Perception.Player.Counts.TruePositive);
            Assert.AreEqual(1, reviewed.Perception.Player.Counts.Unknown);
            Assert.AreEqual(1, reviewed.Perception.Threats.Counts.TruePositive);
            Assert.AreEqual(1d, reviewed.Perception.Player.Precision);
            Assert.AreEqual("NOT_EVALUATED", untouched.Status);
            Assert.IsNull(untouched.Perception);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_HumanAnnotatedSubsetWithoutAllPreselectedFrames_RejectsCherryPicking()
    {
        var root = await FixtureAsync();
        try
        {
            var chosen = (await ReadPlanAsync(root)).Assignments[0];
            await SaveLabelsAsync(root, chosen.RunId, [
                new FrameLabel(chosen.FrameSteps[0], new PlayerLabel("visible", Point(195, 420)),
                    new ThreatLabel("known", [Point(80, 300)]))
            ]);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_ModifiedHeldOutAssignment_RejectsRepartition()
    {
        var root = await FixtureAsync();
        try
        {
            var old = await ReadPlanAsync(root);
            var changed = old with
            {
                Assignments = old.Assignments.Select(a => a with
                {
                    Partition = a.Partition == "held-out" ? "development" : "held-out"
                }).ToArray()
            };
            await File.WriteAllTextAsync(Path.Combine(root, "study-plan.json"), JsonSerializer.Serialize(changed, Json));

            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task EvaluateAsync_AllUnknownImportedTemplate_DoesNotManufactureMetrics()
    {
        var root = await FixtureAsync();
        try
        {
            var plan = await ReadPlanAsync(root);
            var chosen = plan.Assignments[0];
            await SaveLabelsAsync(root, chosen.RunId, chosen.FrameSteps.Select(step =>
                new FrameLabel(step, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))).ToArray());

            var report = await StudyQualityEvaluation.EvaluateAsync(root, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual("NOT_EVALUATED", report.Status);
            Assert.IsNull(report.Runs.Single(r => r.RunId == chosen.RunId).Perception);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static VisualCandidatePoint Point(float x, float y) => new(x, y);

    private static async Task SaveLabelsAsync(string root, string runId, IReadOnlyList<FrameLabel> frames)
    {
        var labels = new LabelSet(1, runId, frames);
        await File.WriteAllTextAsync(Path.Combine(root, runId, "labels.json"), JsonSerializer.Serialize(labels, Json));
    }

    private static async Task<StudyPlan> ReadPlanAsync(string root) =>
        JsonSerializer.Deserialize<StudyPlan>(await File.ReadAllTextAsync(Path.Combine(root, "study-plan.json")),
            Json) ?? throw new InvalidDataException();

    private static async Task<string> FixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "argus-blind-quality-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runs = new List<StudyRun>();
        foreach (var id in new[] { "real-a", "real-b" })
        {
            var folder = Path.Combine(root, id);
            Directory.CreateDirectory(folder);
            var lines = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                await File.WriteAllBytesAsync(Path.Combine(folder, $"frame-{i:D4}.png"), Png);
                lines.Add(JsonSerializer.Serialize(new
                {
                    step = i, timestampUtc = "2026-10-17T10:00:00Z", width = 390, height = 844,
                    screenshot = $"frame-{i:D4}.png",
                    evidence = new
                    {
                        phase = "exploring", reason = "synthetic fixture " + id,
                        confidence = 0.9f,
                        combat = new
                        {
                            playerX = 195, playerY = 420, threatCandidates = 1,
                            threatLocations = new[] { new { x = 80, y = 300 } }
                        }
                    }
                }));
            }
            await File.WriteAllLinesAsync(Path.Combine(folder, "trace.jsonl"), lines);
            runs.Add(new StudyRun(id, await ReplayTraceReader.ReadAsync(folder)));
        }

        var plan = StudyPlanner.Create(runs, "locked-study-fixture", framesPerRun: 2);
        await File.WriteAllTextAsync(Path.Combine(root, "study-plan.json"), JsonSerializer.Serialize(plan, Json));
        return root;
    }
}
