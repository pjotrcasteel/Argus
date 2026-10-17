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
    public void FindFirst_RefinementCard_SelectsVisibleTitleNotInstruction()
    {
        var labels = new[]
        {
            new VisibleText("Refine your magic", 33, 56, 290, 28, 96),
            new VisibleText("REFINEMENT", 64, 128, 78, 8, 89),
            new VisibleText("Steady Force", 56, 211, 130, 18, 96),
            new VisibleText("+6% spell damage per rank", 56, 265, 189, 13, 94),
            new VisibleText("REFINEMENT", 64, 444, 78, 8, 88),
            new VisibleText("Casting Rhythm", 56, 527, 160, 18, 91)
        };

        var result = ChoiceCardMatcher.FindFirst(labels, ["REFINEMENT"], 45);

        Assert.IsNotNull(result);
        Assert.AreEqual("Steady Force", result.Text);
        Assert.AreEqual(121f, result.CenterX);
    }

    [TestMethod]
    public void FindFirst_NoRecognizedCardTitle_DoesNotInventLocation()
    {
        var labels = new[] { new VisibleText("REFINEMENT", 64, 128, 78, 8, 90) };

        Assert.IsNull(ChoiceCardMatcher.FindFirst(labels, ["REFINEMENT"], 45));
    }

    [TestMethod]
    public void FindBest_GenericWordInsideInstruction_DoesNotClick()
    {
        var profile = new VisualGameProfile { MenuActions = ["Choose"] };
        var heading = new VisibleText("Choose your primary magic", 79, 240, 222, 20, 95);

        Assert.IsNull(MenuTextMatcher.FindBest([heading], profile));
    }

    [TestMethod]
    public void FindBest_RealSpellOption_ChoosesButtonNotHeading()
    {
        var profile = new VisualGameProfile { MenuActions = ["Arcane Orb", "Choose"] };
        var lines = new[]
        {
            new VisibleText("Choose your primary magic", 79, 240, 222, 20, 95),
            new VisibleText("Arcane Orb", 60, 410, 120, 30, 87)
        };

        Assert.AreEqual("Arcane Orb", MenuTextMatcher.FindBest(lines, profile)?.Text);
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
    public async Task DecideAsync_VisibleResultOverlay_StopsWithoutSendingMovement()
    {
        var profile = new VisualGameProfile { TerminalScreenCues = ["THE ARCANE ATLAS"] };
        var agent = new VisualGameAgent(
            new FakeTextReader([new VisibleText("THE ARCANE ATLAS AWAKENS", 70, 200, 250, 30, 97)]), profile);
        var frame = new FrameObservation(DateTimeOffset.UtcNow, 390, 844, []);

        var action = await agent.DecideAsync(frame, TestContext.CancellationToken);

        Assert.IsNull(action);
        Assert.AreEqual("terminal", agent.LastEvidence?.Phase);
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
    public void DetectRgb_VisibleLavenderRing_LocatesCircleWithoutCoordinates()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 3;
                var distance = Math.Sqrt((x - 155) * (x - 155) + (y - 475) * (y - 475));
                var ring = Math.Abs(distance - 18) <= 2;
                rgb[p] = ring ? (byte)190 : (byte)30;
                rgb[p + 1] = ring ? (byte)156 : (byte)26;
                rgb[p + 2] = ring ? (byte)228 : (byte)42;
            }
        }

        var result = RouteCircleDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.IsTrue(Math.Abs(result.X - 155) <= 5);
        Assert.IsTrue(Math.Abs(result.Y - 475) <= 5);
        Assert.IsTrue(result.Confidence >= 0.6f);
    }

    [TestMethod]
    public void DetectRgb_CyanOutlinedTrail_LocatesNodeAcrossBiomes()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 3;
                var radius = Math.Sqrt((x - 224) * (x - 224) + (y - 476) * (y - 476));
                var ring = Math.Abs(radius - 18) <= 2;
                rgb[p] = ring ? (byte)110 : (byte)30;
                rgb[p + 1] = ring ? (byte)137 : (byte)26;
                rgb[p + 2] = ring ? (byte)156 : (byte)42;
            }
        }

        var result = RouteCircleDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.IsTrue(Math.Abs(result.X - 224) <= 6);
        Assert.IsTrue(Math.Abs(result.Y - 476) <= 6);
    }

    [TestMethod]
    public void DetectRgb_EnabledFilledButton_FindsActionArea()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 3;
                var active = x >= 16 && x < 374 && y >= 786 && y < 829;
                rgb[p] = active ? (byte)104 : (byte)32;
                rgb[p + 1] = active ? (byte)68 : (byte)22;
                rgb[p + 2] = active ? (byte)129 : (byte)43;
            }
        }

        var result = FilledActionButtonDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.AreEqual(195f, result.X);
        Assert.IsTrue(result.Y >= 786 && result.Y < 829);
        Assert.IsTrue(result.Confidence > 0.8f);
    }

    [TestMethod]
    public void DetectRgb_DisabledDarkButton_DoesNotClick()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 48;
            rgb[i + 1] = 34;
            rgb[i + 2] = 59;
        }

        Assert.IsNull(FilledActionButtonDetector.DetectRgb(rgb, width, height));
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
