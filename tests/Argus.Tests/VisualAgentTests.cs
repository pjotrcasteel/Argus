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
    public async Task DecideAsync_ExternalTitleStartGame_ReadableVisualButton_ClicksActualOcrCenter()
    {
        // The updated external title screen uses small black text on gold. The
        // real M2.38 390x844 screenshot OCR with sparse orientation mode
        // yielded START (115,759,58,10) and GAME (184,759,52,10).
        // No screen coordinate may be encoded into the navigation policy.
        var profile = new VisualGameProfile { MenuActions = ["Start Game", "Begin Hunt"] };
        var visible = new VisibleText("START GAME", 115, 759, 121, 10, 96);
        var agent = new VisualGameAgent(new FakeTextReader([visible]), profile);

        var action = await agent.DecideAsync(new FrameObservation(DateTimeOffset.UtcNow, 390, 844, []),
            TestContext.CancellationToken);

        Assert.IsNotNull(action);
        Assert.AreEqual(GameActionKind.Click, action.Kind);
        Assert.AreEqual(175.5f, action.X);
        Assert.AreEqual(764f, action.Y);
        Assert.AreEqual("menu", agent.LastEvidence?.Phase);
        StringAssert.Contains(agent.LastEvidence!.Reason, "START GAME");
    }

    [TestMethod]
    public void ParseTsv_NewTitleSparseOcr_WordsMergeIntoClickableLine()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t2\t1\t1\t1\t115\t759\t58\t10\t96.55\tSTART\n" +
            "5\t1\t2\t1\t1\t2\t184\t759\t52\t10\t96.88\tGAME\n";
        var lines = TesseractTsvParser.Parse(tsv);
        var detected = MenuTextMatcher.FindBest(lines, new VisualGameProfile { MenuActions = ["Start Game"] });

        Assert.IsNotNull(detected);
        Assert.AreEqual("START GAME", detected.Text);
        Assert.AreEqual(115, detected.X);
        Assert.AreEqual(759, detected.Y);
    }

    [TestMethod]
    public void FindBest_RedesignedTitleWithArrow_MatchesSourceVisibleStartGame()
    {
        // WyrmForge #122 changed the public title while the first live
        // regression was running. Automatic-page OCR on original pixels
        // sees START (117,697,56,10) and GAME (183,697,51,10).
        var lines = new[]
        {
            new VisibleText("WYRMFORGE", 55, 106, 280, 30, 91),
            new VisibleText("START GAME >", 117, 697, 146, 10, 86),
            new VisibleText("Settings", 140, 770, 89, 15, 93)
        };
        var result = MenuTextMatcher.FindBest(lines,
            new VisualGameProfile { MenuActions = ["Start Game", "Begin First Hunt"] });

        Assert.IsNotNull(result);
        Assert.AreEqual("START GAME >", result.Text);
        Assert.AreEqual(190f, result.CenterX);
        Assert.AreEqual(702f, result.CenterY);
    }

    [TestMethod]
    public void FindBest_TownFirstHuntSplitOcr_ClicksObservedPartialButtonText()
    {
        // The new Refuge title uses a dark filled control. Original screenshot
        // OCR sees BE and FIRST HUNT as separate lines, not BEGIN FIRST HUNT.
        var labels = new[]
        {
            new VisibleText("BE", 116, 704, 19, 9, 96),
            new VisibleText("FIRST HUNT", 174, 704, 100, 9, 96)
        };
        var profile = new VisualGameProfile { MenuActions = ["Begin First Hunt", "First Hunt"] };

        var best = MenuTextMatcher.FindBest(labels, profile);

        Assert.IsNotNull(best);
        Assert.AreEqual("FIRST HUNT", best.Text);
        Assert.AreEqual(224f, best.CenterX);
        Assert.AreEqual(708.5f, best.CenterY);
    }

    [TestMethod]
    public async Task DecideAsync_TownAfterTitleClick_NoCombatCue_NeverSendsMovement()
    {
        var profile = new VisualGameProfile { MenuActions = ["Start Game"], GameplayCues = ["HP", "Wave"] };
        var screen = new FrameObservation(DateTimeOffset.UtcNow, 390, 844, []);
        var texts = new SequencedTextReader(
        [
            [new VisibleText("START GAME", 115, 759, 121, 10, 96)],
            [new VisibleText("WYRMFORGE", 56, 124, 271, 24, 90)],
            [new VisibleText("PUBLIC NICKNAME", 7, 52, 100, 16, 90)],
            [new VisibleText("FIRST HUNT", 174, 704, 100, 9, 96)],
            []
        ]);
        var agent = new VisualGameAgent(texts, profile);
        var first = await agent.DecideAsync(screen, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Click, first?.Kind);

        for (var step = 0; step < 4; step++)
        {
            var action = await agent.DecideAsync(screen, TestContext.CancellationToken);
            Assert.AreEqual(GameActionKind.Wait, action?.Kind, $"Town step {step} must not move.");
            Assert.AreNotEqual("exploring", agent.LastEvidence?.Phase);
        }
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
    public async Task DecideAsync_CombatTextSampling_DetectsLaterTerminalWithinThreeFrames()
    {
        // Only previously established gameplay may defer expensive OCR. Even when
        // the image contains no detectable player, each frame is still observed.
        const int width = 390;
        const int height = 844;
        var screenshot = BmpRgb(new byte[width * height * 3], width, height);
        var frame = new FrameObservation(DateTimeOffset.UtcNow, width, height, screenshot);
        var reader = new SequencedTextReader(
        [
            [new VisibleText("HP", 20, 12, 25, 14, 90)],
            [new VisibleText("VICTORY", 40, 120, 130, 32, 95)]
        ]);
        var profile = new VisualGameProfile { GameplayCues = ["HP"], TerminalScreenCues = ["VICTORY"] };
        var agent = new VisualGameAgent(reader, profile);

        var first = await agent.DecideAsync(frame, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.HoldKey, first?.Kind);
        for (var i = 0; i < 2; i++)
        {
            var deferred = await agent.DecideAsync(frame, TestContext.CancellationToken);
            Assert.AreEqual(GameActionKind.HoldKey, deferred?.Kind);
            StringAssert.Contains(agent.LastEvidence!.Reason, "text scan deferred");
        }

        var fourth = await agent.DecideAsync(frame, TestContext.CancellationToken);
        Assert.IsNull(fourth);
        Assert.AreEqual("terminal", agent.LastEvidence?.Phase);
    }

    [TestMethod]
    public async Task DecideAsync_TownAfterStart_UsesOneUnverifiedPixelProbeOnlyWhenOptedIn()
    {
        const int width = 390;
        const int height = 844;
        var townRgb = new byte[width * height * 3];
        static void Light(byte[] pixels, int left, int top, int lightWidth)
        {
            for (var y = top; y < top + 32; y++)
            {
                for (var x = left; x < left + lightWidth; x++)
                {
                    var pixel = (y * width + x) * 3;
                    pixels[pixel] = 220;
                    pixels[pixel + 1] = 140;
                    pixels[pixel + 2] = 40;
                }
            }
        }

        Light(townRgb, 49, 199, 20);
        Light(townRgb, 192, 196, 26);
        Light(townRgb, 325, 198, 21);
        var title = new FrameObservation(DateTimeOffset.UtcNow, width, height,
            BmpRgb(new byte[width * height * 3], width, height));
        var town = new FrameObservation(DateTimeOffset.UtcNow, width, height, BmpRgb(townRgb, width, height));
        var reader = new SequencedTextReader([[new VisibleText("START GAME", 115, 759, 121, 10, 96)], []]);
        var agent = new VisualGameAgent(reader,
            new VisualGameProfile { MenuActions = ["Start Game"], EnableIlluminatedEntryProbe = true });

        Assert.AreEqual(GameActionKind.Click, (await agent.DecideAsync(title, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual(GameActionKind.Wait, (await agent.DecideAsync(town, TestContext.CancellationToken))?.Kind);
        var proposal = await agent.DecideAsync(town, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Click, proposal?.Kind);
        Assert.AreEqual("entry-probe", agent.LastEvidence?.Phase);
        Assert.IsTrue(proposal?.X is > 195f and < 215f);
        Assert.IsTrue(proposal?.Y is > 195f and < 230f);

        Assert.AreEqual(GameActionKind.Wait, (await agent.DecideAsync(town, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual(GameActionKind.Wait, (await agent.DecideAsync(town, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual("transition", agent.LastEvidence?.Phase);
    }

    [TestMethod]
    public async Task DecideAsync_NoEntryProbeOptIn_WaitsWithoutSpeculativeTownClick()
    {
        const int width = 390;
        const int height = 844;
        var frame = new FrameObservation(DateTimeOffset.UtcNow, width, height,
            BmpRgb(new byte[width * height * 3], width, height));
        var reader = new SequencedTextReader([[new VisibleText("START GAME", 115, 759, 121, 10, 96)], []]);
        var agent = new VisualGameAgent(reader, new VisualGameProfile { MenuActions = ["Start Game"] });

        Assert.AreEqual(GameActionKind.Click, (await agent.DecideAsync(frame, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual(GameActionKind.Wait, (await agent.DecideAsync(frame, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual(GameActionKind.Wait, (await agent.DecideAsync(frame, TestContext.CancellationToken))?.Kind);
        Assert.AreEqual("transition", agent.LastEvidence?.Phase);
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
    public async Task DecideAsync_RouteDetailHidesOcrHeading_ConfirmsOnlyVisibleEnabledButton()
    {
        const int width = 390;
        const int height = 844;
        var map = new byte[width * height * 3];
        var selected = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = (y * width + x) * 3;
                var ring = Math.Abs(Math.Sqrt((x - 155) * (x - 155) + (y - 475) * (y - 475)) - 18) <= 2;
                map[idx] = ring ? (byte)190 : (byte)30;
                map[idx + 1] = ring ? (byte)156 : (byte)26;
                map[idx + 2] = ring ? (byte)228 : (byte)42;
                var enabled = x >= 16 && x < 374 && y >= 786 && y < 830;
                selected[idx] = enabled ? (byte)104 : (byte)32;
                selected[idx + 1] = enabled ? (byte)68 : (byte)22;
                selected[idx + 2] = enabled ? (byte)129 : (byte)43;
            }
        }

        var reader = new SequencedTextReader(
        [
            [new VisibleText("Choose your trail", 30, 70, 220, 20, 96)],
            [new VisibleText("REACHABLE NOW", 20, 700, 150, 18, 97)]
        ]);
        var agent = new VisualGameAgent(reader,
            new VisualGameProfile { RouteCues = ["Choose your trail"] });
        var initial = new FrameObservation(DateTimeOffset.UtcNow, width, height, BmpRgb(map, width, height));
        var detail = new FrameObservation(DateTimeOffset.UtcNow, width, height, BmpRgb(selected, width, height));

        var choose = await agent.DecideAsync(initial, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Click, choose?.Kind);
        Assert.AreEqual("route", agent.LastEvidence?.Phase);

        var settling = await agent.DecideAsync(detail, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Wait, settling?.Kind);

        var confirm = await agent.DecideAsync(detail, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Click, confirm?.Kind);
        Assert.AreEqual(195f, confirm?.X);
        Assert.IsTrue(confirm?.Y is >= 786f and < 830f);
        Assert.AreEqual("route", agent.LastEvidence?.Phase);
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
    public void DetectRgb_GoldOutlinedConnectedTrail_LocatesVisibleCircle()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = (y * width + x) * 3;
                var distance = Math.Sqrt((x - 155) * (x - 155) + (y - 475) * (y - 475));
                var ring = Math.Abs(distance - 18) <= 2;
                rgb[pixel] = ring ? (byte)223 : (byte)39;
                rgb[pixel + 1] = ring ? (byte)208 : (byte)33;
                rgb[pixel + 2] = ring ? (byte)128 : (byte)45;
            }
        }

        var result = RouteCircleDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.IsTrue(Math.Abs(result.X - 155) <= 6);
        Assert.IsTrue(Math.Abs(result.Y - 475) <= 6);
    }

    [TestMethod]
    public void DetectRgb_WarmConnectedTrailAndCoolDecor_PrefersActualRouteNode()
    {
        const int width = 390;
        const int height = 844;
        var rgb = new byte[width * height * 3];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 3;
                var warmDistance = Math.Sqrt((x - 230) * (x - 230) + (y - 475) * (y - 475));
                var coolDistance = Math.Sqrt((x - 312) * (x - 312) + (y - 545) * (y - 545));

                if (Math.Abs(warmDistance - 18) <= 2)
                {
                    rgb[p] = 238; rgb[p + 1] = 146; rgb[p + 2] = 118;
                }
                else if (Math.Abs(coolDistance - 18) <= 2)
                {
                    rgb[p] = 118; rgb[p + 1] = 150; rgb[p + 2] = 177;
                }
                else
                {
                    rgb[p] = 39; rgb[p + 1] = 32; rgb[p + 2] = 45;
                }
            }
        }

        var result = RouteCircleDetector.DetectRgb(rgb, width, height);

        Assert.IsNotNull(result);
        Assert.IsTrue(Math.Abs(result.X - 230) <= 7);
        Assert.IsTrue(Math.Abs(result.Y - 475) <= 7);
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

    [TestMethod]
    public async Task DecideAsync_UpgradeWithoutHudDuringDeferredOcr_PausesAndRechecksInsteadOfMoving()
    {
        const int width = 390;
        const int height = 844;
        var activePixels = new byte[width * height * 3];
        for (var x = 20; x < 160; x++)
        {
            var i = (46 * width + x) * 3;
            activePixels[i] = 102;
            activePixels[i + 1] = 164;
            activePixels[i + 2] = 102;
        }
        var combatFrame = new FrameObservation(DateTimeOffset.UtcNow, width, height,
            BmpRgb(activePixels, width, height));
        var upgradeFrame = new FrameObservation(DateTimeOffset.UtcNow, width, height,
            BmpRgb(new byte[width * height * 3], width, height));
        var profile = new VisualGameProfile
        {
            GameplayCues = ["HP"],
            ChoiceScreenCues = ["Refine your magic"],
            CombatHudBar = new VisualHudBarSettings
            {
                MinimumConsecutivePixels = 14,
                FillPalettes = [new VisualColorRule
                {
                    MinGreen = 54, MinGreenMinusRed = 6, MinGreenMinusBlue = 6
                }]
            }
        };
        var labels = new SequencedTextReader(
        [
            [new VisibleText("HP 100/100", 19, 57, 100, 17, 95)],
            [
                new VisibleText("Refine your magic", 35, 56, 230, 25, 97),
                new VisibleText("REFINEMENT", 44, 142, 100, 12, 94),
                new VisibleText("Fleet Step", 93, 196, 140, 21, 95)
            ]
        ]);
        var agent = new VisualGameAgent(labels, profile);

        Assert.AreEqual(GameActionKind.HoldKey,
            (await agent.DecideAsync(combatFrame, TestContext.CancellationToken))?.Kind);
        var intercepted = await agent.DecideAsync(upgradeFrame, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Wait, intercepted?.Kind);
        Assert.AreEqual("hud-hidden", agent.LastEvidence?.Phase);
        Assert.IsNull(agent.LastEvidence?.Combat);

        var resumed = await agent.DecideAsync(upgradeFrame, TestContext.CancellationToken);
        Assert.AreEqual(GameActionKind.Click, resumed?.Kind);
        Assert.AreEqual("choice", agent.LastEvidence?.Phase);
    }

    public TestContext TestContext { get; set; } = null!;

    private static byte[] BmpRgb(byte[] rgb, int width, int height)
    {
        var stride = (width * 3 + 3) & ~3;
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + stride * height);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(stride * height);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    var idx = (y * width + x) * 3;
                    writer.Write(rgb[idx + 2]);
                    writer.Write(rgb[idx + 1]);
                    writer.Write(rgb[idx]);
                }
                for (var padding = width * 3; padding < stride; padding++)
                    writer.Write((byte)0);
            }
        }
        return output.ToArray();
    }

    private sealed class SequencedTextReader(IReadOnlyList<IReadOnlyList<VisibleText>> screens) : IFrameTextReader
    {
        private int _index;

        public Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default)
        {
            var next = _index < screens.Count ? screens[_index++] : Array.Empty<VisibleText>();
            return Task.FromResult(next);
        }
    }

    private sealed class FakeTextReader(IReadOnlyList<VisibleText> lines) : IFrameTextReader
    {
        public Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default) =>
            Task.FromResult(lines);
    }
}
