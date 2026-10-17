using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class AnnotationEnhancementTests
{
    [TestMethod]
    public void Render_RecordedThreatPositions_ShowsPredictionOverlayWithoutMakingGroundTruth()
    {
        var trace = new ReplayTrace([
            new ReplayFrame(0, "frame-0000.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"),
                390, 844, "exploring", "Candidate hypotheses", 0.7f, "holdKey", 195, 420, 2)
            {
                ThreatLocations = [new VisualCandidatePoint(110, 300), new VisualCandidatePoint(270, 450)]
            }
        ]);
        var labels = GroundTruth.CreateTemplate("real-run", trace);

        var html = AnnotationPageBuilder.Render(trace, labels);

        StringAssert.Contains(html, "frame.threatLocations.forEach");
        StringAssert.Contains(html, "predicted-threat");
        StringAssert.Contains(html, "110");
        StringAssert.Contains(html, "270");
        Assert.AreEqual("unknown", labels.Frames[0].Player.State);
        Assert.AreEqual("unknown", labels.Frames[0].Threats.State);
        Assert.IsNull(labels.Frames[0].Threats.Centers);
    }

    [TestMethod]
    public void Render_HumanLabelImport_ProvidesLocalValidationAndExportControls()
    {
        var trace = new ReplayTrace([
            new ReplayFrame(0, "frame-0000.png", DateTimeOffset.UtcNow,
                390, 844, "exploring", "Visible screenshot", 0.6f, null, 195, 420, 0)
        ]);
        var labels = GroundTruth.CreateTemplate("real-run", trace);

        var html = AnnotationPageBuilder.Render(trace, labels);

        StringAssert.Contains(html, "Import previous labels.json");
        StringAssert.Contains(html, "validImport(json)");
        StringAssert.Contains(html, "file.text()");
        StringAssert.Contains(html, "beforeunload");
        StringAssert.Contains(html, "structuredClone");
        StringAssert.Contains(html, "labels.json");
        StringAssert.Contains(html, "textContent");
        Assert.IsFalse(html.Contains("fetch(", StringComparison.OrdinalIgnoreCase));
    }
}
