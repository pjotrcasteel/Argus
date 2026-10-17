using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class AnnotationPageBuilderTests
{
    [TestMethod]
    public void Render_UnannotatedFrames_ExportsLocalManualLabelWorkflow()
    {
        var trace = new ReplayTrace([
            Frame(0, "exploring", "Look for mage"),
            Frame(1, "exploring", "Moving left")
        ]);
        var template = new LabelSet(1, "m1-live", [
            new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null)),
            new FrameLabel(1, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))
        ]);

        var html = AnnotationPageBuilder.Render(trace, template);

        StringAssert.Contains(html, "anchor.download");
        StringAssert.Contains(html, "Export labels.json");
        StringAssert.Contains(html, "frame-0001.png");
        StringAssert.Contains(html, "JSON.stringify(labels, null, 2)");
        StringAssert.Contains(html, "Mark threat");
        StringAssert.Contains(html, "Can't determine");
        StringAssert.Contains(html, "getBoundingClientRect");
        Assert.IsFalse(html.Contains("/*ARGUS_ANNOTATIONS*/", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("/*ARGUS_TRACE_FRAMES*/", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Render_UntrustedDecisionText_EscapesExecutableHtml()
    {
        const string injection = "</script><script>alert('bad')</script>";
        var trace = new ReplayTrace([Frame(0, "exploring", injection)]);
        var template = new LabelSet(1, "m1-live", [
            new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null))
        ]);

        var html = AnnotationPageBuilder.Render(trace, template);

        Assert.IsFalse(html.Contains(injection, StringComparison.Ordinal));
        StringAssert.Contains(html, "textContent");
        StringAssert.Contains(html, "\\u003C");
        Assert.IsFalse(html.Contains("innerHTML", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Render_DuplicateFrameLabels_RejectsInvalidAnnotation()
    {
        var trace = new ReplayTrace([Frame(0, "exploring", "test")]);
        var label = new FrameLabel(0, new PlayerLabel("unknown", null), new ThreatLabel("unknown", null));
        var template = new LabelSet(1, "m1-live", [label, label]);

        Assert.ThrowsExactly<InvalidDataException>(() => AnnotationPageBuilder.Render(trace, template));
    }

    [TestMethod]
    public void Render_OutOfBoundsLabel_RejectsInvalidPosition()
    {
        var trace = new ReplayTrace([Frame(0, "exploring", "test")]);
        var template = new LabelSet(1, "m1-live", [
            new FrameLabel(0, new PlayerLabel("visible", new VisualCandidatePoint(900, 420)), new ThreatLabel("unknown", null))
        ]);

        Assert.ThrowsExactly<InvalidDataException>(() => AnnotationPageBuilder.Render(trace, template));
    }

    private static ReplayFrame Frame(int step, string phase, string reason) =>
        new(step, $"frame-{step:D4}.png", DateTimeOffset.Parse("2026-10-17T10:00:00Z"), 390, 844, phase, reason,
            0.5f, "holdKey", 190, 420, 2);
}
