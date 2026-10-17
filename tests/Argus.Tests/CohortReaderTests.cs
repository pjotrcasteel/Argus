using System.Text.Json;
using Argus.Analysis;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class CohortReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_TwoMatchedRuns_ComputesComparableTotalsAndInterval()
    {
        var root = FreshRoot();
        try
        {
            await WriteRun(root, "run-001", Evidence(20, 10, true, 4), new RunMetadata("v1", "WyrmForge", "390x844", "heuristic-v1"));
            await WriteRun(root, "run-002", Evidence(40, 4, false, 1), new RunMetadata("v1", "WyrmForge", "390x844", "heuristic-v1"));

            var report = await CohortReader.ReadAsync(root, TestContext.CancellationToken);

            Assert.AreEqual(2, report.Totals.Runs);
            Assert.AreEqual(1, report.Totals.PassedVisualGate);
            Assert.AreEqual(60, report.Totals.Frames);
            Assert.AreEqual(14, report.Totals.PlayerLocatedFrames);
            Assert.AreEqual(5, report.Totals.CameraEstimatedFrames);
            Assert.IsTrue(report.Totals.IsMatchedCohort);
            Assert.IsTrue(Math.Abs(report.Totals.WeightedPlayerCoverage - 14.0 / 60) < 0.0001);
            Assert.IsTrue(report.Totals.VisualGateRate95.Lower < 0.5);
            Assert.IsTrue(report.Totals.VisualGateRate95.Upper > 0.5);
            StringAssert.Contains(CohortMarkdown.Render(report), "not game victories");
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_UnversionedRuns_RefusesToClaimControlledComparison()
    {
        var root = FreshRoot();
        try
        {
            await WriteRun(root, "run-001", Evidence(20, 10, true, 4));
            await WriteRun(root, "run-002", Evidence(20, 10, true, 4));

            var report = await CohortReader.ReadAsync(root, TestContext.CancellationToken);

            Assert.IsFalse(report.Totals.IsMatchedCohort);
            StringAssert.Contains(CohortMarkdown.Render(report), "descriptive only");
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_ConflictingGameVersions_RefusesMatchedCohort()
    {
        var root = FreshRoot();
        try
        {
            await WriteRun(root, "run-001", Evidence(20, 10, true, 4), new RunMetadata("v1", "WyrmForge", "390x844", "a"));
            await WriteRun(root, "run-002", Evidence(20, 10, true, 4), new RunMetadata("v2", "WyrmForge", "390x844", "a"));

            Assert.IsFalse((await CohortReader.ReadAsync(root, TestContext.CancellationToken)).Totals.IsMatchedCohort);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_ContradictoryGate_RejectsRun()
    {
        var root = FreshRoot();
        try
        {
            await WriteRun(root, "bad", Evidence(20, 10, false, 4));

            var failed = false;
            try { await CohortReader.ReadAsync(root, TestContext.CancellationToken); }
            catch (InvalidDataException) { failed = true; }
            Assert.IsTrue(failed, "Inconsistent gate status must not silently enter a cohort.");
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_AbsentRequiredMeasurement_RejectsRun()
    {
        var root = FreshRoot();
        try
        {
            var run = Path.Combine(root, "bad");
            Directory.CreateDirectory(run);
            await File.WriteAllTextAsync(Path.Combine(run, "summary.json"), "{\"frames\":20}", TestContext.CancellationToken);

            var failed = false;
            try { await CohortReader.ReadAsync(root, TestContext.CancellationToken); }
            catch (InvalidDataException) { failed = true; }
            Assert.IsTrue(failed);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_DuplicateRunFolderIds_RejectsAmbiguousCohort()
    {
        var root = FreshRoot();
        try
        {
            await WriteRun(Path.Combine(root, "group-a"), "same", Evidence(20, 10, true, 4));
            await WriteRun(Path.Combine(root, "group-b"), "same", Evidence(20, 10, true, 4));

            var failed = false;
            try { await CohortReader.ReadAsync(root, TestContext.CancellationToken); }
            catch (InvalidDataException) { failed = true; }
            Assert.IsTrue(failed);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void Wilson95_OneSuccessfulTrial_ReflectsMassiveUncertainty()
    {
        var interval = CohortStatistics.Wilson95(1, 1);
        Assert.IsTrue(interval.Lower < 0.25);
        Assert.IsTrue(interval.Upper <= 1);
    }

    private static string FreshRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "argus-cohort-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static RunEvidenceSummary Evidence(int frames, int located, bool gate, int cameras) =>
        new(frames, gate ? 1 : 0, gate ? 2 : 0, 1, located, 2, 0, 0, 4, gate)
        {
            CameraEstimatedFrames = cameras,
            FramesWithMovingCandidates = 1,
            FramesWithStationaryCandidates = 0
        };

    private static async Task WriteRun(string root, string id, RunEvidenceSummary evidence, RunMetadata? metadata = null)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await File.WriteAllTextAsync(Path.Combine(folder, "summary.json"), JsonSerializer.Serialize(evidence, options));
        if (metadata is not null)
            await File.WriteAllTextAsync(Path.Combine(folder, "experiment.json"), JsonSerializer.Serialize(metadata, options));
    }
}
