using Argus.Agents.Visual;
using Argus.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class GameplayHudWitnessTests
{
    [TestMethod]
    public void IsVisible_LowConfidenceCompleteHealthFractionInTopHud_IsRecognized()
    {
        var labels = new[] { new VisibleText("HP 100/100 WV 1", 19, 57, 94, 17, 28.56f) };
        Assert.IsTrue(GameplayHudWitness.IsVisible(labels, 390, 844));
    }

    [DataTestMethod]
    [DataRow("HP")]
    [DataRow("HP 100")]
    [DataRow("HEALTH")]
    [DataRow("HP 120/100")]
    [DataRow("HP 100/0")]
    [DataRow("Tap HP 100/100")]
    public void IsVisible_InsufficientOrImpossibleHudText_Abstains(string label)
    {
        Assert.IsFalse(GameplayHudWitness.IsVisible(
            [new VisibleText(label, 19, 57, 110, 17, 30)], 390, 844));
    }

    [TestMethod]
    public void IsVisible_NumericHudInPageBody_Abstains()
    {
        Assert.IsFalse(GameplayHudWitness.IsVisible(
            [new VisibleText("HP 100/100", 30, 450, 110, 14, 95)], 390, 844));
    }

    [TestMethod]
    public void FindFirstTitleOnly_ActualOriginalUpgradeOcr_PrefersRealCardTitle()
    {
        var lines = new[]
        {
            new VisibleText("LEVEL 2 + REFINEMENT", 32, 32, 250, 9, 95),
            new VisibleText("Refine your magic", 33, 56, 230, 22, 95),
            new VisibleText("FATEWEAVE @@00 2/4", 43, 97, 180, 8, 92),
            new VisibleText("Fleet Step", 93, 196, 93, 15, 96),
            new VisibleText("+0% + +4% movement speed", 93, 224, 245, 9, 80),
            new VisibleText("Casting Rhythm", 93, 478, 144, 15, 96),
            new VisibleText("Life Thread", 93, 761, 102, 12, 95)
        };
        var candidate = ChoiceCardMatcher.FindFirstTitleOnly(lines, 45, 390, 844);

        Assert.IsNotNull(candidate);
        Assert.AreEqual("Fleet Step", candidate.Text);
        Assert.AreEqual(139.5f, candidate.CenterX);
        Assert.AreEqual(203.5f, candidate.CenterY);
    }

    [TestMethod]
    public void FindFirstTitleOnly_OnlyInstructionsAndPageHeading_Abstains()
    {
        var labels = new[]
        {
            new VisibleText("Refine your magic", 33, 56, 245, 22, 95),
            new VisibleText("FATEWEAVE 2/4", 43, 97, 180, 8, 92),
            new VisibleText("+4% movement speed per rank", 92, 235, 200, 11, 95),
            new VisibleText("A small Storm trail reward", 92, 276, 240, 12, 95)
        };
        Assert.IsNull(ChoiceCardMatcher.FindFirstTitleOnly(labels, 45, 390, 844));
    }
}
