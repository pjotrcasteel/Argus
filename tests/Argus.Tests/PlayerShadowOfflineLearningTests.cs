using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class PlayerShadowOfflineLearningTests
{
    [TestMethod]
    public void Evaluate_OnlyTwoIndependentRuns_ReportsNotReadyWithoutWeights()
    {
        var report = PlayerShadowOfflineLearning.Evaluate(Rows("dev-a", "dev-b"));
        Assert.AreEqual("NOT_READY", report.Status);
        Assert.IsNull(report.FinalResearchModel);
        Assert.HasCount(0, report.LeaveOneRunOut);
        Assert.IsTrue(report.Blockers.Any(x => x.Contains("distinct development runs", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Evaluate_MissingClassOrHardNegatives_PerRunNeverFitsModel()
    {
        var dataset = Rows("dev-a", "dev-b", "dev-c").ToList();
        dataset.RemoveAll(x => x.RunId == "dev-b" && x.Label == 0);
        var noNegative = PlayerShadowOfflineLearning.Evaluate(dataset);
        Assert.AreEqual("NOT_READY", noNegative.Status);
        Assert.IsNull(noNegative.FinalResearchModel);
        Assert.IsTrue(noNegative.Blockers.Any(x => x.StartsWith("dev-b:", StringComparison.Ordinal)));

        dataset = Rows("dev-a", "dev-b", "dev-c").Select(row =>
            row.RunId == "dev-c" && row.Label == 0 ? row with { Witness = "EMPTY_PATCH_VETO",
                PlayerPalettePixels = 0, RgbStdDev = 0, StrongEdgeFraction = 0 } : row).ToList();
        var noHardNegatives = PlayerShadowOfflineLearning.Evaluate(dataset);
        Assert.AreEqual("NOT_READY", noHardNegatives.Status);
        Assert.IsNull(noHardNegatives.FinalResearchModel);
    }

    [TestMethod]
    public void Evaluate_ThreeDistinctHumanReviewedDevelopmentRuns_ReallyFitsDeterministicLogisticModel()
    {
        var data = Rows("dev-c", "dev-a", "dev-b");
        var report = PlayerShadowOfflineLearning.Evaluate(data);
        var repeated = PlayerShadowOfflineLearning.Evaluate(data.Reverse().ToArray());

        Assert.AreEqual("RESEARCH_TRAINED", report.Status);
        Assert.IsNotNull(report.FinalResearchModel);
        Assert.AreEqual("class-balanced-l2-logistic-v1", report.FinalResearchModel.Algorithm);
        Assert.HasCount(3, report.LeaveOneRunOut);
        Assert.AreEqual(30, report.TotalLabeled);
        Assert.AreEqual(3, report.DevelopmentRuns);
        CollectionAssert.AreEqual(report.FinalResearchModel.Weights, repeated.FinalResearchModel!.Weights);
        CollectionAssert.AreEqual(report.FinalResearchModel.Means, repeated.FinalResearchModel.Means);
        Assert.IsTrue(report.FinalResearchModel.Weights.Any(w => Math.Abs(w) > 0.01));
        Assert.IsTrue(report.FinalResearchModel.Weights.All(double.IsFinite));
        foreach (var fold in report.LeaveOneRunOut)
        {
            Assert.HasCount(2, fold.TrainingRunIds);
            Assert.IsFalse(fold.TrainingRunIds.Contains(fold.HeldOutRunId));
            Assert.AreEqual(5, fold.Visible);
            Assert.AreEqual(5, fold.Absent);
            Assert.AreEqual(10, fold.ModelTruePositive + fold.ModelFalsePositive +
                fold.ModelFalseNegative + fold.ModelTrueNegative);
        }
        StringAssert.Contains(PlayerShadowOfflineLearning.RenderMarkdown(report),
            "Leave-one-*whole-run*-out evaluation");
    }

    [TestMethod]
    public void Evaluate_HeldOutOrDuplicateOrInvalidNumericData_RejectsRatherThanTrains()
    {
        var dataset = Rows("dev-a", "dev-b", "dev-c").ToList();
        dataset.Add(dataset[0]);
        Assert.ThrowsExactly<InvalidDataException>(() => PlayerShadowOfflineLearning.Evaluate(dataset));
        dataset.RemoveAt(dataset.Count - 1);
        dataset.Add(dataset[0] with { RunId = "run-01" });
        Assert.ThrowsExactly<InvalidDataException>(() => PlayerShadowOfflineLearning.Evaluate(dataset));
        dataset.RemoveAt(dataset.Count - 1);
        dataset.Add(dataset[0] with { RgbStdDev = double.NaN });
        Assert.ThrowsExactly<InvalidDataException>(() => PlayerShadowOfflineLearning.Evaluate(dataset));
    }

    [TestMethod]
    public void ReadDevelopment_OnlyReadsDevChildrenAndNeverOpensHeldOutFolder()
    {
        var folder = Directory.CreateTempSubdirectory("argus-m213-");
        try
        {
            var dev = Path.Combine(folder.FullName, "dev-a");
            var holdout = Path.Combine(folder.FullName, "run-01");
            Directory.CreateDirectory(dev);
            Directory.CreateDirectory(holdout);
            var contents = PlayerShadowLearningData.Header + "\n" +
                "\"dev-a\",1,\"BRIDGE_ONE\",63,37.5,0.25,\"INDETERMINATE\",1\n" +
                "\"dev-a\",2,\"BRIDGE_TWO\",0,0,0,\"EMPTY_PATCH_VETO\",\n";
            File.WriteAllText(Path.Combine(dev, PlayerShadowLearningData.CsvName), contents);
            // A malicious/invalid held-out file must NEVER be opened (even its contents).
            File.WriteAllText(Path.Combine(holdout, PlayerShadowLearningData.CsvName),
                "run-01,POISONED,never read");
            var loaded = PlayerShadowLearningData.ReadDevelopment(folder.FullName);
            Assert.HasCount(2, loaded);
            Assert.AreEqual(1, loaded[0].Label);
            Assert.IsNull(loaded[1].Label);
            Assert.IsTrue(loaded.All(x => x.RunId == "dev-a"));
        }
        finally { folder.Delete(true); }
    }

    [TestMethod]
    public void ReadDevelopment_CrossRunLabelsOrMalformedQuoting_Rejects()
    {
        var folder = Directory.CreateTempSubdirectory("argus-m213-");
        try
        {
            var run = Path.Combine(folder.FullName, "dev-a");
            Directory.CreateDirectory(run);
            var path = Path.Combine(run, PlayerShadowLearningData.CsvName);
            File.WriteAllText(path, PlayerShadowLearningData.Header + "\n" +
                "\"dev-b\",1,\"BRIDGE_ONE\",63,37.5,0.25,\"INDETERMINATE\",1\n");
            Assert.ThrowsExactly<InvalidDataException>(() => PlayerShadowLearningData.ReadDevelopment(folder.FullName));
            File.WriteAllText(path, PlayerShadowLearningData.Header + "\n" +
                "\"dev-a,1,\"BRIDGE_ONE\",63,37.5,0.25,\"INDETERMINATE\",1\n");
            Assert.ThrowsExactly<InvalidDataException>(() => PlayerShadowLearningData.ReadDevelopment(folder.FullName));
        }
        finally { folder.Delete(true); }
    }

    private static PlayerShadowLearningRow[] Rows(params string[] runIds) =>
        runIds.SelectMany((runId, group) =>
        {
            var data = new List<PlayerShadowLearningRow>();
            for (var i = 0; i < 5; i++)
                data.Add(new PlayerShadowLearningRow(runId, i, "BRIDGE_ONE", 60 + group * 4 + i * 6,
                    20 + i * 3, 0.18 + i * 0.01, "INDETERMINATE", 1));
            for (var i = 0; i < 3; i++)
                data.Add(new PlayerShadowLearningRow(runId, i + 5, "BRIDGE_TWO", i == 2 ? 1400 : 0,
                    36 + group * 2, i == 2 ? 0.05 : 0.65, "INDETERMINATE", 0));
            for (var i = 0; i < 2; i++)
                data.Add(new PlayerShadowLearningRow(runId, i + 8, "BRIDGE_ONE", 0, 0, 0,
                    "EMPTY_PATCH_VETO", 0));
            for (var i = 0; i < 2; i++)
                data.Add(new PlayerShadowLearningRow(runId, i + 10, "BRIDGE_ONE", 0, 0, 0,
                    "EMPTY_PATCH_VETO", null));
            return data;
        }).ToArray();
}
