using Argus.Adapters.Browser;
using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class VisualAgentTests
{
    [TestMethod]
    public void FindBest_MultipleLabels_PrefersHighestPriorityAction()
    {
        var profile = new VisualGameProfile();
        var texts = new[]
        {
            new VisibleText("Play", 50, 10, 60, 30, 96),
            new VisibleText("Begin Hunt", 100, 200, 150, 40, 85)
        };

        var result = MenuTextMatcher.FindBest(texts, profile);

        Assert.IsNotNull(result);
        Assert.AreEqual("Begin Hunt", result.Text);
        Assert.AreEqual(175f, result.CenterX);
    }

    [TestMethod]
    public void FindBest_InterveningWords_RecognizesRealVisibleHuntLabel()
    {
        var result = MenuTextMatcher.FindBest([new VisibleText("BEGIN FIRST HUNT", 16, 322, 358, 56, 94)],
            new VisualGameProfile { MenuActions = ["Begin Hunt"] });

        Assert.IsNotNull(result);
        Assert.AreEqual("BEGIN FIRST HUNT", result.Text);
        Assert.AreEqual(195f, result.CenterX);
    }

    [TestMethod]
    public void FindBest_LowConfidence_DoesNotClick()
    {
        var result = MenuTextMatcher.FindBest([new VisibleText("Begin Hunt", 0, 0, 100, 20, 10)], new VisualGameProfile());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DecideAsync_VisibleMenuButton_ClicksRecognizedCoordinates()
    {
        var agent = new VisualGameAgent(new FakeTextReader([new VisibleText("Begin Hunt", 120, 400, 150, 40, 94)]),
            new VisualGameProfile());
        var frame = new FrameObservation(DateTimeOffset.UtcNow, 390, 844, []);

        var action = await agent.DecideAsync(frame, TestContext.CancellationToken);

        Assert.IsNotNull(action);
        Assert.AreEqual(GameActionKind.Click, action.Kind);
        Assert.AreEqual(195f, action.X);
        Assert.AreEqual(420f, action.Y);
        Assert.AreEqual("menu", agent.LastEvidence?.Phase);
    }

    [TestMethod]
    public async Task DecideAsync_UnknownScreen_DoesNotGuessClick()
    {
        var agent = new VisualGameAgent(new FakeTextReader([]), new VisualGameProfile());
        var frame = new FrameObservation(DateTimeOffset.UtcNow, 390, 844, []);

        var action = await agent.DecideAsync(frame, TestContext.CancellationToken);

        Assert.IsNotNull(action);
        Assert.AreEqual(GameActionKind.Wait, action.Kind);
        Assert.AreEqual("unrecognized", agent.LastEvidence?.Phase);
    }

    [TestMethod]
    public void ParseTsv_LineWords_CombinesTextAndBoundingBoxes()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t1\t1\t1\t1\t100\t200\t50\t20\t95.3\tBegin\n" +
            "5\t1\t1\t1\t1\t2\t155\t200\t45\t20\t90.2\tHunt\n";

        var result = TesseractTsvParser.Parse(tsv);

        Assert.HasCount(1, result);
        Assert.AreEqual("Begin Hunt", result[0].Text);
        Assert.AreEqual(100, result[0].X);
        Assert.AreEqual(100, result[0].Width);
    }

    [TestMethod]
    public void ChooseDirection_IdenticalActivity_RotatesEveryFourActions()
    {
        var empty = new float[4];

        Assert.AreEqual(0, VisualMotion.ChooseDirection(empty, 0));
        Assert.AreEqual(1, VisualMotion.ChooseDirection(empty, 4));
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class FakeTextReader(IReadOnlyList<VisibleText> lines) : IFrameTextReader
    {
        public Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default) =>
            Task.FromResult(lines);
    }
}
