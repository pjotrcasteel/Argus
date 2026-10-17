using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class AnnotationPageBuilderTests
{
    [TestMethod]
    public void Render_UnknownTemplate_ProducesClickableOfflineHumanAnnotation()
    {
        var trace = new ReplayTrace([
            Frame(0, "exploring", "Select an actual player", 195, 422),
            Frame(1, "exploring", "A busy spell effect", null, null),
            Frame(2, "terminal", "The hunt ended", null, null)
        ]);
        var template = GroundTruth.CreateTemplate("test-run", trace, sampleCount: 2);

        var page = AnnotationPageBuilder.Render(trace, template);

        Assert.HasCount(2, template.Frames);
        Assert.IsTrue(template.Frames.All(l => l.Player.State == "unknown" && l.Threats.State == "unknown"));
        StringAssert.Contains(page, "ARGUS / Perception Ground Truth");
        StringAssert.Contains(page, "clickToPoint(clientX, clientY)");
        StringAssert.Contains(page, "Download labels.json");
        StringAssert.Contains(page, "Import labels.json");
        StringAssert.Contains(page, "Math.hypot");
        StringAssert.Contains(page, "threat-none");
        StringAssert.Contains(page, "threat-unknown");
        StringAssert.Contains(page, "No threats visible");
        StringAssert.Contains(page, "frame-0000.png");
        StringAssert.Contains(page, "frame-0001.png");
        StringAssert.Contains(page, "createObjectURL(blob)");
        Assert.IsFalse(page.Contains("fetch(", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Render_MaliciousReason_AlwaysEscapesScriptAndDoesNotInjectHtml()
    {
        const string payload = "</script><script>window.attacked=true</script>";
        var trace = new ReplayTrace([Frame(0, "exploring", payload, 195, 422)]);
        var template = GroundTruth.CreateTemplate("test-run", trace);

        var page = AnnotationPageBuilder.Render(trace, template);

        Assert.IsFalse(page.Contains(payload, StringComparison.Ordinal));
        StringAssert.Contains(page, "window.attacked");
        StringAssert.Contains(page, "textContent");
        Assert.AreEqual(1, page.Split("<script>", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(1, page.Split("</script>", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void Render_PredictedThreatMarkers_ArePresentButLabelsRemainUnknown()
    {
        var trace = new ReplayTrace([
            Frame(0, "exploring", "Potential hazard", 190, 420)
                with { ThreatLocations = [new VisualCandidatePoint(88, 300), new VisualCandidatePoint(260, 480)] }
        ]);
        var template = GroundTruth.CreateTemplate("test-run", trace);

        var page = AnnotationPageBuilder.Render(trace, template);

        StringAssert.Contains(page, "threatLocations");
        StringAssert.Contains(page, "88");
        StringAssert.Contains(page, "260");
        Assert.AreEqual("unknown", template.Frames[0].Threats.State);
        Assert.IsNull(template.Frames[0].Threats.Centers);
    }

    [TestMethod]
    public void Render_MismatchedTemplate_RejectsUnrecognizedFrame()
    {
        var trace = new ReplayTrace([Frame(0, "exploring", "one", 195, 420)]);
        var wrong = new LabelSet(1, "test-run", [
            new FrameLabel(5, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))
        ]);

        Assert.ThrowsExactly<InvalidDataException>(() => AnnotationPageBuilder.Render(trace, wrong));
    }

    [TestMethod]
    public void Validate_HumanExportWithExplicitAbsentAndEmptyThreats_ConformsToScorerSchema()
    {
        var trace = new ReplayTrace([Frame(0, "exploring", "one", 195, 420), Frame(1, "exploring", "two", null, null)]);
        var humanLabels = new LabelSet(1, "test-run", [
            new FrameLabel(0, new PlayerLabel("visible", new VisualCandidatePoint(195, 420)),
                new ThreatLabel("known", [new VisualCandidatePoint(92, 300)])),
            new FrameLabel(1, new PlayerLabel("absent", null), new ThreatLabel("known", []))
        ]);

        GroundTruth.Validate(humanLabels, "test-run", trace);

        Assert.AreEqual("absent", humanLabels.Frames[1].Player.State);
        Assert.HasCount(0, humanLabels.Frames[1].Threats.Centers!);
    }

    private static ReplayFrame Frame(int step, string phase, string reason, float? x, float? y) =>
        new(step, $"frame-{step:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00+00:00"),
            390, 844, phase, reason, 0.5f, null, x, y, 2);
}
