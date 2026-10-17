using System.Text.Json;
using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PerceptionScoringTests
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly byte[] PngStub = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];

    [TestMethod]
    public async Task ScoreAsync_VisiblePlayerAndThreats_CountsCorrectlyWithFalsePositives()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [
                Row(0, (195, 410), [(90, 300), (150, 300)]),
                Row(1, (212, 410), [(80, 300)]),
                Row(2, null, [])
            ]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("visible", Point(195, 411)), new ThreatLabel("known", [Point(91, 300)])),
                new FrameLabel(1, new PlayerLabel("visible", Point(50, 50)), new ThreatLabel("known", [Point(80, 300), Point(145, 320)])),
                new FrameLabel(2, new PlayerLabel("absent", null), new ThreatLabel("known", []))
            ]);

            var report = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, report.Player.Counts.TruePositive);
            Assert.AreEqual(1, report.Player.Counts.FalsePositive);
            Assert.AreEqual(1, report.Player.Counts.FalseNegative);
            Assert.AreEqual(1, report.Player.Counts.TrueNegative);
            Assert.AreEqual(2, report.Threats.Counts.TruePositive);
            Assert.AreEqual(1, report.Threats.Counts.FalsePositive);
            Assert.AreEqual(1, report.Threats.Counts.FalseNegative);
            Assert.AreEqual(0, report.Threats.Counts.TrueNegative);
            Assert.AreEqual(1, report.Threats.Counts.Unknown);
            Assert.AreEqual(1, report.ThreatEvidenceUnavailableFrames);
            Assert.IsTrue(Math.Abs(report.Threats.Precision!.Value - 2d / 3) < 0.001);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_OldTraceWithoutCandidatePositions_IsUnknownNotTrueNegative()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, (195, 410), null)]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("visible", Point(195, 410)), new ThreatLabel("known", []))
            ]);

            var report = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, report.ThreatEvidenceUnavailableFrames);
            Assert.AreEqual(1, report.Threats.Counts.Unknown);
            Assert.AreEqual(0, report.Threats.Counts.TrueNegative);
            Assert.IsNull(report.Threats.Precision);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_UnknownLabels_AreNotNegatives()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, (195, 410), [])]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = GroundTruth.CreateTemplate(folder, replay);

            var report = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, report.Player.Counts.Unknown);
            Assert.AreEqual(1, report.Threats.Counts.Unknown);
            Assert.AreEqual(0, report.Player.Counts.TrueNegative);
            Assert.IsNull(report.Player.Precision);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_TransitionAndExplorationWithoutDetectedPlayer_DistinguishesMissingEvidenceFromMiss()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, null, null), Row(1, null, null)]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("visible", Point(186, 431)), new ThreatLabel("known", [Point(154, 106)])),
                new FrameLabel(1, new PlayerLabel("visible", Point(194, 420)), new ThreatLabel("known", []))
            ]);

            var report = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, report.PlayerEvidenceUnavailableFrames);
            Assert.AreEqual(1, report.Player.Counts.Unknown);
            Assert.AreEqual(1, report.Player.Counts.FalseNegative);
            Assert.AreEqual(2, report.ThreatEvidenceUnavailableFrames);
            Assert.AreEqual(2, report.Threats.Counts.Unknown);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ReadAsync_DuplicateLabelsAndInvalidCoordinate_Rejects()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, null, [])]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var first = new FrameLabel(0, new PlayerLabel("visible", Point(900, 410)), new ThreatLabel("known", []));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                GroundTruth.Validate(new LabelSet(1, Path.GetFileName(folder), [first]), Path.GetFileName(folder), replay));

            var normal = new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                GroundTruth.Validate(new LabelSet(1, Path.GetFileName(folder), [normal, normal]), Path.GetFileName(folder), replay));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task CreateTemplate_OnlyUnknownFrames_NeverInventsHumanLabels()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, null, []), Row(1, (200, 410), []), Row(2, (210, 410), [])]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);

            var template = GroundTruth.CreateTemplate(folder, replay);

            Assert.HasCount(2, template.Frames);
            Assert.IsTrue(template.Frames.All(f => f.Player.State == "unknown" && f.Threats.State == "unknown"));
            Assert.IsTrue(template.Frames.All(f => f.Player.Center is null && f.Threats.Centers is null));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_DoubleDetectionCannotCountAsTwoTruePositives()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, (195, 410), [(80, 300), (82, 300)])]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("visible", Point(195, 410)), new ThreatLabel("known", [Point(80, 300)]))
            ]);

            var report = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, report.Threats.Counts.TruePositive);
            Assert.AreEqual(1, report.Threats.Counts.FalsePositive);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_LegacyFinalScreenshot_DoesNotCountStalePredictionsAsQuality()
    {
        var folder = FreshFolder();
        try
        {
            var last = "{\"action\":null," + Row(1, (335, 421), [(130, 390)])[1..];
            await WriteTrace(folder, [Row(0, null, []), last]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null)),
                new FrameLabel(1, new PlayerLabel("visible", Point(371, 421)), new ThreatLabel("known", [Point(130, 390)]))
            ]);

            var score = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);
            var failures = await PerceptionFailureAnalysis.AnalyzeAsync(folder, labels, replay,
                cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, score.LegacyFinalUnverifiedFrames);
            Assert.AreEqual(1, score.PlayerEvidenceUnavailableFrames);
            Assert.AreEqual(1, score.ThreatEvidenceUnavailableFrames);
            Assert.AreEqual(0, score.Threats.Counts.TruePositive);
            Assert.AreEqual(2, score.Threats.Counts.Unknown);
            Assert.AreEqual(0, failures.ScoredThreatFrames);
            Assert.AreEqual("LEGACY_FINAL_UNVERIFIED", failures.Frames[1].ThreatStatus);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task ScoreAsync_ExplicitFreshFinalScreenshot_IsMeasuredEvenWhenInputSuppressed()
    {
        var folder = FreshFolder();
        try
        {
            var last = "{\"action\":null,\"inputSuppressedAtStepLimit\":true," + Row(1, (371, 421), [(130, 390)])[1..];
            await WriteTrace(folder, [Row(0, null, []), last]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null)),
                new FrameLabel(1, new PlayerLabel("visible", Point(371, 421)), new ThreatLabel("known", [Point(130, 390)]))
            ]);

            var score = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);
            var failures = await PerceptionFailureAnalysis.AnalyzeAsync(folder, labels, replay,
                cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(0, score.LegacyFinalUnverifiedFrames);
            Assert.AreEqual(1, score.Player.Counts.TruePositive);
            Assert.AreEqual(1, score.Threats.Counts.TruePositive);
            Assert.AreEqual(1, failures.ScoredThreatFrames);
            Assert.AreEqual("SCORED", failures.Frames[1].ThreatStatus);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task AnalyzeAsync_RealisticMisses_ProducesOneToOneCountsAndLegacyRoiDiagnostics()
    {
        var folder = FreshFolder();
        try
        {
            await WriteTrace(folder, [Row(0, null, []), Row(1, (195, 420), [(80, 300), (210, 315)]),
                Row(2, (196, 418), [(100, 300)])]);
            var replay = await ReplayTraceReader.ReadAsync(folder, TestContext.CancellationToken);
            var labels = new LabelSet(1, Path.GetFileName(folder), [
                new FrameLabel(0, new PlayerLabel("visible", Point(186, 431)), new ThreatLabel("known", [Point(150, 100)])),
                new FrameLabel(1, new PlayerLabel("visible", Point(195, 420)),
                    new ThreatLabel("known", [Point(80, 300), Point(383, 614)])),
                new FrameLabel(2, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))
            ]);

            var failures = await PerceptionFailureAnalysis.AnalyzeAsync(folder, labels, replay,
                cancellationToken: TestContext.CancellationToken);
            var score = await PerceptionScoring.ScoreAsync(folder, labels, replay, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(1, failures.ScoredThreatFrames);
            Assert.AreEqual(1, failures.ThreatTruePositives);
            Assert.AreEqual(1, failures.ThreatFalsePositives);
            Assert.AreEqual(1, failures.ThreatFalseNegatives);
            Assert.AreEqual(1, failures.MissedOutsideLegacySearchArea);
            Assert.AreEqual("NOT_OBSERVED", failures.Frames[0].ThreatStatus);
            Assert.AreEqual("MATCH", failures.Frames[1].PlayerStatus);
            Assert.AreEqual("UNKNOWN_REFERENCE", failures.Frames[2].ThreatStatus);
            Assert.AreEqual(383f, failures.Frames[1].MissedThreatCenters.Single().X);
            Assert.AreEqual(score.Threats.Counts.TruePositive, failures.ThreatTruePositives);
            Assert.AreEqual(score.Threats.Counts.FalsePositive, failures.ThreatFalsePositives);
            Assert.AreEqual(score.Threats.Counts.FalseNegative, failures.ThreatFalseNegatives);
            Assert.IsTrue(PerceptionFailureAnalysis.RenderMarkdown(failures).Contains("Frame 1"));
        }
        finally { Directory.Delete(folder, true); }
    }

    private static VisualCandidatePoint Point(float x, float y) => new(x, y);

    private static string FreshFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "argus-quality-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Row(int step, (float X, float Y)? player, (float X, float Y)[]? candidates)
    {
        var combat = player is null ? null : new
        {
            playerX = player.Value.X,
            playerY = player.Value.Y,
            threatCandidates = candidates?.Length ?? 0,
            threatLocations = candidates?.Select(p => new { x = p.X, y = p.Y }).ToArray()
        };
        return JsonSerializer.Serialize(new
        {
            step,
            timestampUtc = "2026-10-17T10:00:00+00:00",
            width = 390,
            height = 844,
            screenshot = $"frame-{step:D4}.png",
            evidence = new { phase = step == 0 ? "menu" : "exploring", reason = "fixture", confidence = 0.7f, combat }
        });
    }

    private static async Task WriteTrace(string folder, string[] rows)
    {
        await File.WriteAllLinesAsync(Path.Combine(folder, "trace.jsonl"), rows);
        for (var i = 0; i < rows.Length; i++)
            await File.WriteAllBytesAsync(Path.Combine(folder, $"frame-{i:D4}.png"), PngStub);
    }
}
