using System.Text.Json;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class ReplayTraceReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    // The smallest accepted PNG signature is sufficient for reader safety tests; no image decoding is performed.
    private static readonly byte[] TestPng = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];

    [TestMethod]
    public async Task ReadAsync_ValidRun_BuildsSafeReadOnlyReplay()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(0, "frame-0000.png", "exploring", "<script>alert('bad')</script>", true),
                Row(1, "frame-0001.png", "terminal", "The hunt ended", false));
            await File.WriteAllBytesAsync(Path.Combine(root, "frame-0000.png"), TestPng, TestContext.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(root, "frame-0001.png"), TestPng, TestContext.CancellationToken);

            var replay = await ReplayTraceReader.ReadAsync(root, TestContext.CancellationToken);
            var page = ReplayPageBuilder.Render(replay);

            Assert.HasCount(2, replay.Frames);
            Assert.AreEqual(195f, replay.Frames[0].PlayerX);
            Assert.IsNull(replay.Frames[1].PlayerX);
            Assert.IsFalse(page.Contains("<script>alert('bad')</script>", StringComparison.Ordinal));
            Assert.IsTrue(page.Contains("frame-0001.png", StringComparison.Ordinal));
            Assert.IsTrue(page.Contains("seek.addEventListener", StringComparison.Ordinal));
            Assert.IsTrue(page.Contains("textContent", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_PathTraversalFrame_RejectsExternalFileAccess()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(0, "../outside.png", "exploring", "bad", false));

            await AssertThrowsInvalidData(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_MissingScreenshot_RejectsRun()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(0, "frame-0000.png", "exploring", "nothing found", false));

            await AssertThrowsInvalidData(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_NonPngScreenshot_RejectsRun()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(0, "frame-0000.png", "exploring", "bad image", false));
            await File.WriteAllBytesAsync(Path.Combine(root, "frame-0000.png"), [1, 2, 3, 4, 5, 6, 7, 8],
                TestContext.CancellationToken);

            await AssertThrowsInvalidData(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_OutOfOrderSteps_RejectsRun()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(1, "frame-0001.png", "exploring", "wrong order", false));

            await AssertThrowsInvalidData(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ReadAsync_MissingPlayerPosition_DoesNotInventMarker()
    {
        var root = FreshRoot();
        try
        {
            await WriteTrace(root, Row(0, "frame-0000.png", "unrecognized", "unknown", false));
            await File.WriteAllBytesAsync(Path.Combine(root, "frame-0000.png"), TestPng, TestContext.CancellationToken);

            var replay = await ReplayTraceReader.ReadAsync(root, TestContext.CancellationToken);

            Assert.IsNull(replay.Frames[0].PlayerX);
            Assert.IsNull(replay.Frames[0].ThreatCandidates);
            Assert.AreEqual("unrecognized", replay.Frames[0].Phase);
        }
        finally { Directory.Delete(root, true); }
    }

    private async Task AssertThrowsInvalidData(string root)
    {
        var failed = false;
        try { await ReplayTraceReader.ReadAsync(root, TestContext.CancellationToken); }
        catch (InvalidDataException) { failed = true; }
        Assert.IsTrue(failed);
    }

    private static string FreshRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "argus-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Row(int step, string filename, string phase, string reason, bool player)
    {
        var o = new
        {
            step,
            timestampUtc = "2026-10-17T10:00:00+00:00",
            width = 390,
            height = 844,
            screenshot = filename,
            action = step == 0 ? new { kind = "holdKey", key = "a" } : null,
            evidence = new
            {
                phase,
                reason,
                confidence = 0.6f,
                combat = player ? new { playerX = 195f, playerY = 420f, threatCandidates = 2 } : null
            }
        };
        return JsonSerializer.Serialize(o);
    }

    private static async Task WriteTrace(string folder, params string[] lines) =>
        await File.WriteAllLinesAsync(Path.Combine(folder, "trace.jsonl"), lines);
}
