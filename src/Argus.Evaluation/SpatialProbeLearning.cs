using System.Text;

namespace Argus.Evaluation;

/// <summary>One frozen M2.15 screenshot location. Features are extracted from the current PNG only;
/// run/step/circle role are identifiers, never model inputs. Human truth is never a feature.</summary>
public sealed record SpatialProbeRow(string RunId, string SampleId, int Step, string Role,
    double[] ImageFeatures, int? HumanLabel);

public sealed record SpatialProbeRunCoverage(string RunId, int Reviewed, int Present, int Absent, int Unknown,
    int CenterPresent, int ControlPresent);

public sealed record SpatialProbeFold(string HeldOutRunId, string[] FitRuns, int TruePositive, int FalsePositive,
    int FalseNegative, int TrueNegative, int CenterBaselineTruePositive, int CenterBaselineFalsePositive,
    int CenterBaselineFalseNegative, int CenterBaselineTrueNegative);

public sealed record SpatialProbeModel(string[] FeatureNames, string[] TrainingRuns, double[] Means,
    double[] StandardDeviations, double[] Weights, double Bias)
{
    public double Probability(SpatialProbeRow row)
    {
        var z = Bias;
        for (var i = 0; i < Weights.Length; i++)
            z += Weights[i] * ((row.ImageFeatures[i] - Means[i]) / StandardDeviations[i]);
        return z >= 0 ? 1 / (1 + Math.Exp(-z)) : Math.Exp(z) / (1 + Math.Exp(z));
    }
}

public sealed record SpatialProbeLearningReport(string Status, string[] Blockers, int Labeled,
    IReadOnlyList<SpatialProbeRunCoverage> Runs, IReadOnlyList<SpatialProbeFold> LeaveOneRunOut,
    SpatialProbeModel? PrivateResearchModel);

/// <summary>
/// Offline, deterministically trained pixel-feature logistic baseline. Folds exclude an entire
/// development run including its labels from training, scaling, weighting and threshold choice.
/// Neither this class nor its output is ever consulted by ARGUS gameplay.
/// </summary>
public static class SpatialProbeLearning
{
    public static readonly string[] FeatureNames =
        ["patch_mean_luminance", "patch_luminance_stddev", "patch_ring_contrast",
         "patch_gradient_density", "patch_mean_saturation"];
    private const int Epochs = 1500;
    private const double LearningRate = 0.12;
    private const double L2 = 0.04;

    public static SpatialProbeLearningReport Evaluate(IReadOnlyList<SpatialProbeRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > 10_000) throw new InvalidDataException("Excessive spatial research rows.");
        foreach (var row in rows)
        {
            if (row.RunId is not ("dev-f" or "dev-g" or "dev-h") || row.Step < 0 ||
                row.SampleId.Length is < 5 or > 90 ||
                row.Role is not ("center-anchor" or "spatial-control") ||
                row.ImageFeatures.Length != FeatureNames.Length ||
                row.ImageFeatures.Any(x => !double.IsFinite(x) || x < 0 || x > 1) ||
                row.HumanLabel is not (null or 0 or 1))
                throw new InvalidDataException("Invalid or non-development spatial pixel evidence.");
        }

        if (rows.Select(r => r.SampleId).Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidDataException("Duplicate spatial sample.");
        var groups = rows.GroupBy(r => r.RunId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (RunId: g.Key, Rows: g.OrderBy(x => x.Step)
                .ThenBy(x => x.SampleId, StringComparer.Ordinal).ToArray())).ToArray();
        var blockers = new List<string>();
        if (groups.Length != 3)
            blockers.Add($"Need all three independent development runs, found {groups.Length}.");
        var coverage = new List<SpatialProbeRunCoverage>();
        foreach (var group in groups)
        {
            var labels = group.Rows.Where(x => x.HumanLabel.HasValue).ToArray();
            var positives = labels.Count(x => x.HumanLabel == 1);
            var absent = labels.Count(x => x.HumanLabel == 0);
            var centerPos = labels.Count(x => x.HumanLabel == 1 && x.Role == "center-anchor");
            var controlPos = positives - centerPos;
            coverage.Add(new SpatialProbeRunCoverage(group.RunId, labels.Length, positives, absent,
                group.Rows.Length - labels.Length, centerPos, controlPos));
            if (group.Rows.Length != 24 || labels.Length < 10 || positives < 3 || absent < 3)
                blockers.Add($"{group.RunId}: require 24 original probes, 10 reviewed, at least 3 present and 3 absent; " +
                    $"found {group.Rows.Length}/{labels.Length}/{positives}/{absent}.");
        }
        if (blockers.Count > 0)
            return new SpatialProbeLearningReport("NOT_READY", blockers.ToArray(), coverage.Sum(x => x.Reviewed),
                coverage, [], null);

        var folds = new List<SpatialProbeFold>();
        foreach (var holdout in groups)
        {
            var other = groups.Where(g => g.RunId != holdout.RunId).ToArray();
            var model = Fit(other.SelectMany(g => g.Rows).Where(r => r.HumanLabel.HasValue).ToArray(),
                other.Select(g => g.RunId).ToArray());
            var counters = new int[8];
            foreach (var row in holdout.Rows.Where(r => r.HumanLabel.HasValue))
            {
                Accumulate(counters, 0, row.HumanLabel!.Value, model.Probability(row) >= 0.5);
                Accumulate(counters, 4, row.HumanLabel.Value, row.Role == "center-anchor");
            }
            folds.Add(new SpatialProbeFold(holdout.RunId, model.TrainingRuns,
                counters[0], counters[1], counters[2], counters[3],
                counters[4], counters[5], counters[6], counters[7]));
        }
        var final = Fit(groups.SelectMany(g => g.Rows).Where(r => r.HumanLabel.HasValue).ToArray(),
            groups.Select(g => g.RunId).ToArray());
        return new SpatialProbeLearningReport("EXPLORATORY_TRAINED", [], coverage.Sum(x => x.Reviewed),
            coverage, folds, final);
    }

    private static SpatialProbeModel Fit(SpatialProbeRow[] rows, string[] runs)
    {
        var count = rows.Length;
        var present = rows.Count(x => x.HumanLabel == 1);
        var absent = count - present;
        if (present == 0 || absent == 0) throw new InvalidDataException("Training split lacks a class.");
        var means = new double[FeatureNames.Length];
        var deviations = new double[FeatureNames.Length];
        for (var j = 0; j < means.Length; j++)
        {
            var dimension = j;
            means[j] = rows.Average(x => x.ImageFeatures[dimension]);
            var variance = rows.Average(x => Math.Pow(x.ImageFeatures[dimension] - means[dimension], 2));
            deviations[j] = variance < 1e-12 ? 1 : Math.Sqrt(variance);
        }
        var weights = new double[means.Length];
        var bias = 0d;
        for (var epoch = 0; epoch < Epochs; epoch++)
        {
            var gradient = new double[weights.Length];
            var biasGradient = 0d;
            foreach (var row in rows)
            {
                var score = bias;
                for (var j = 0; j < weights.Length; j++)
                    score += weights[j] * (row.ImageFeatures[j] - means[j]) / deviations[j];
                var probability = score >= 0 ? 1 / (1 + Math.Exp(-score)) : Math.Exp(score) / (1 + Math.Exp(score));
                var balancedWeight = row.HumanLabel == 1 ? 0.5 / present : 0.5 / absent;
                var error = (probability - row.HumanLabel!.Value) * balancedWeight;
                biasGradient += error;
                for (var j = 0; j < gradient.Length; j++)
                    gradient[j] += error * (row.ImageFeatures[j] - means[j]) / deviations[j];
            }
            bias -= LearningRate * biasGradient;
            for (var j = 0; j < weights.Length; j++)
                weights[j] -= LearningRate * (gradient[j] + L2 * weights[j]);
        }
        if (!double.IsFinite(bias) || weights.Any(x => !double.IsFinite(x)))
            throw new InvalidDataException("Nonfinite exploratory model.");
        return new SpatialProbeModel([.. FeatureNames], runs, means, deviations, weights, bias);
    }

    private static void Accumulate(int[] stats, int offset, int truth, bool guess)
    {
        if (truth == 1 && guess) stats[offset]++;
        else if (truth == 0 && guess) stats[offset + 1]++;
        else if (truth == 1) stats[offset + 2]++;
        else stats[offset + 3]++;
    }

    public static string RenderMarkdown(SpatialProbeLearningReport report)
    {
        var b = new StringBuilder("# M2.16 — screenshot-location learning (OFFLINE ONLY)\n\n");
        b.AppendLine($"Status: **{report.Status}**; {report.Labeled} private human references.");
        b.AppendLine("Five image-only appearance features; no role, run, screen location, labels, agent predictions, or BRIDGE inputs in the model.");
        b.AppendLine("Fixed 1500-epoch class-balanced logistic / L2 0.04 / learning rate 0.12 / threshold 0.5; train-run-only scaling.");
        b.AppendLine("Whole-run development-only exclusion is NOT a fresh prospective held-out evaluation.");
        foreach (var problem in report.Blockers) b.AppendLine("- NOT_READY: " + problem);
        b.AppendLine("\n| Run | Reviewed | Present | Absent | Unknown | Center positives | Control positives |");
        b.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var run in report.Runs)
            b.AppendLine($"| {run.RunId} | {run.Reviewed} | {run.Present} | {run.Absent} | " +
                $"{run.Unknown} | {run.CenterPresent} | {run.ControlPresent} |");
        if (report.PrivateResearchModel is null) return b.AppendLine("\nNo weights fitted.").ToString();
        b.AppendLine("\n| Excluded run | Model TP/FP/FN/TN | Center-only baseline TP/FP/FN/TN |");
        b.AppendLine("| --- | --- | --- |");
        foreach (var f in report.LeaveOneRunOut)
            b.AppendLine($"| {f.HeldOutRunId} | {f.TruePositive}/{f.FalsePositive}/{f.FalseNegative}/{f.TrueNegative} | " +
                $"{f.CenterBaselineTruePositive}/{f.CenterBaselineFalsePositive}/" +
                $"{f.CenterBaselineFalseNegative}/{f.CenterBaselineTrueNegative} |");
        b.AppendLine("\n**NOT a general player detector:** this limited center-versus-grid study is position/phase biased. " +
            "Do not claim success from synthetic tests, do not tune on M2.4 run-01, do not deploy weights. " +
            "Require prospective new independent whole sessions and significantly broader locations.");
        return b.ToString();
    }
}
