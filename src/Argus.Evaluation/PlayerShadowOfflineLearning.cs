using System.Text;
using System.Text;

namespace Argus.Evaluation;

public sealed record PlayerShadowModel(string Algorithm, string[] FeatureNames, string[] TrainingRunIds,
    double[] Means, double[] StandardDeviations, double[] Weights, double Bias, double DecisionThreshold)
{
    public double ProbabilityVisible(PlayerShadowLearningRow row)
    {
        var features = row.Features();
        var value = Bias;
        for (var i = 0; i < features.Length; i++)
            value += Weights[i] * ((features[i] - Means[i]) / StandardDeviations[i]);
        return value >= 0 ? 1d / (1d + Math.Exp(-value)) : Math.Exp(value) / (1d + Math.Exp(value));
    }
}

public sealed record PlayerShadowFoldResult(string HeldOutRunId, string[] TrainingRunIds,
    int Visible, int Absent, int ModelTruePositive, int ModelFalsePositive, int ModelFalseNegative, int ModelTrueNegative,
    int BaselineTruePositive, int BaselineFalsePositive, int BaselineFalseNegative, int BaselineTrueNegative);

public sealed record PlayerShadowRunCoverage(string RunId, int Reviewed, int Visible, int Absent, int HardNegatives,
    int Unknown);
public sealed record PlayerShadowLearningReport(string Status, string[] Blockers, int DevelopmentRuns,
    int TotalLabeled, IReadOnlyList<PlayerShadowRunCoverage> Coverage,
    IReadOnlyList<PlayerShadowFoldResult> LeaveOneRunOut, PlayerShadowModel? FinalResearchModel)
{
    public bool Trained => Status == "RESEARCH_TRAINED";
}

/// <summary>
/// Actual fixed-hyperparameter, L2-regularized and class-balanced batch logistic learner.
/// Every held-out score uses a distinct entire play-run. This remains research only.
/// </summary>
public static class PlayerShadowOfflineLearning
{
    public const int MinimumRuns = 3;
    public const int MinimumReviewedPerRun = 10;
    public const int MinimumPerClassPerRun = 3;
    public const int MinimumHardNegativesPerRun = 2;
    private const double Lambda = 0.04;
    private const double StepSize = 0.12;
    private const int Epochs = 1500;
    private static readonly string[] Names = ["log1p_player_palette_pixels", "rgb_stddev_div64",
        "strong_edges_times4", "second_missing_frame"];

    public static PlayerShadowLearningReport Evaluate(IReadOnlyList<PlayerShadowLearningRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > 1_000_000) throw new InvalidDataException("Too many research samples.");
        var blocks = new List<string>();
        var clean = new List<PlayerShadowLearningRow>();
        foreach (var row in rows)
        {
            if (row.RunId.Length < 5 || row.RunId.Length > 64 ||
                !row.RunId.StartsWith("dev-", StringComparison.Ordinal) ||
                row.RunId.Skip(4).Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
                row.Step < 0 || row.RawBridge is not ("BRIDGE_ONE" or "BRIDGE_TWO") ||
                row.Witness is not ("INDETERMINATE" or "EMPTY_PATCH_VETO") ||
                row.PlayerPalettePixels is < 0 or > 2401 ||
                !double.IsFinite(row.RgbStdDev) || row.RgbStdDev is < 0 or > 128 ||
                !double.IsFinite(row.StrongEdgeFraction) || row.StrongEdgeFraction is < 0 or > 1 ||
                row.Label is not (null or 0 or 1))
                throw new InvalidDataException("Untrusted/held-out player appearance features are not allowed.");
            clean.Add(row);
        }

        var groups = clean.GroupBy(x => x.RunId, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Id: g.Key, Rows: g.OrderBy(row => row.Step).ToArray())).ToArray();
        if (groups.Length < MinimumRuns)
            blocks.Add($"Need >= {MinimumRuns} distinct development runs; found {groups.Length}.");
        if (clean.Select(x => (x.RunId, x.Step)).Distinct().Count() != clean.Count)
            throw new InvalidDataException("Duplicate step within an independent development run.");

        var coverage = new List<PlayerShadowRunCoverage>();
        foreach (var group in groups)
        {
            var reviewed = group.Rows.Where(x => x.Label.HasValue).ToArray();
            var visible = reviewed.Count(x => x.Label == 1);
            var absent = reviewed.Count(x => x.Label == 0);
            var hard = reviewed.Count(x => x.Label == 0 && x.Witness == "INDETERMINATE");
            coverage.Add(new PlayerShadowRunCoverage(group.Id, reviewed.Length, visible, absent, hard,
                group.Rows.Length - reviewed.Length));
            if (reviewed.Length < MinimumReviewedPerRun || visible < MinimumPerClassPerRun ||
                absent < MinimumPerClassPerRun || hard < MinimumHardNegativesPerRun)
                blocks.Add($"{group.Id}: need >={MinimumReviewedPerRun} human-reviewed proposals, " +
                    $">={MinimumPerClassPerRun} VISIBLE and ABSENT each, " +
                    $">={MinimumHardNegativesPerRun} unresolved human-ABSENT hard negatives; " +
                    $"found {reviewed.Length}/{visible}/{absent}/{hard}.");
        }

        if (blocks.Count > 0)
            return new PlayerShadowLearningReport("NOT_READY", blocks.ToArray(), groups.Length,
                coverage.Sum(x => x.Reviewed), coverage, [], null);

        var folds = new List<PlayerShadowFoldResult>();
        foreach (var holdout in groups)
        {
            var others = groups.Where(g => g.Id != holdout.Id).ToArray();
            // Strictly no held-out rows (including labels/features) in fitting or normalization.
            var fit = Fit(others.SelectMany(g => g.Rows).Where(r => r.Label.HasValue).ToArray(),
                others.Select(g => g.Id).ToArray());
            var test = holdout.Rows.Where(x => x.Label.HasValue).ToArray();
            var truePositive = 0;
            var falsePositive = 0;
            var falseNegative = 0;
            var trueNegative = 0;
            var baselineTp = 0;
            var baselineFp = 0;
            var baselineFn = 0;
            var baselineTn = 0;
            foreach (var row in test)
            {
                Accumulate(row.Label!.Value, fit.ProbabilityVisible(row) >= fit.DecisionThreshold,
                    ref truePositive, ref falsePositive, ref falseNegative, ref trueNegative);
                // Existing M2.11 conservative rule is a baseline, NOT a reliable player identity:
                // unresolved stale guess => provisionally visible; empty-patch veto => absent.
                Accumulate(row.Label.Value, row.Witness != "EMPTY_PATCH_VETO",
                    ref baselineTp, ref baselineFp, ref baselineFn, ref baselineTn);
            }

            folds.Add(new PlayerShadowFoldResult(holdout.Id, fit.TrainingRunIds, test.Count(x => x.Label == 1),
                test.Count(x => x.Label == 0), truePositive, falsePositive, falseNegative, trueNegative,
                baselineTp, baselineFp, baselineFn, baselineTn));
        }

        // Research-only fit using all DEVELOPMENT runs. Never an active game policy.
        var final = Fit(clean.Where(x => x.Label.HasValue).ToArray(), groups.Select(g => g.Id).ToArray());
        return new PlayerShadowLearningReport("RESEARCH_TRAINED", [], groups.Length,
            coverage.Sum(x => x.Reviewed), coverage, folds, final);
    }

    private static PlayerShadowModel Fit(IReadOnlyList<PlayerShadowLearningRow> examples, string[] runs)
    {
        var count = examples.Count;
        var features = examples.Select(x => x.Features()).ToArray();
        var positive = examples.Count(x => x.Label == 1);
        var negative = count - positive;
        if (positive == 0 || negative == 0) throw new InvalidDataException("Training fold lacks a class.");

        var means = new double[Names.Length];
        var deviations = new double[Names.Length];
        for (var j = 0; j < Names.Length; j++)
        {
            means[j] = features.Average(x => x[j]);
            var variance = features.Average(x => Math.Pow(x[j] - means[j], 2));
            deviations[j] = variance < 1e-12 ? 1 : Math.Sqrt(variance);
        }

        var weights = new double[Names.Length];
        var intercept = 0d;
        for (var epoch = 0; epoch < Epochs; epoch++)
        {
            var gradient = new double[Names.Length];
            var biasGradient = 0d;
            for (var i = 0; i < count; i++)
            {
                var score = intercept;
                for (var j = 0; j < Names.Length; j++)
                    score += weights[j] * ((features[i][j] - means[j]) / deviations[j]);
                var probability = score >= 0 ? 1d / (1d + Math.Exp(-score)) : Math.Exp(score) / (1d + Math.Exp(score));
                var target = examples[i].Label!.Value;
                var balancedWeight = target == 1 ? 0.5d / positive : 0.5d / negative;
                var error = (probability - target) * balancedWeight;
                biasGradient += error;
                for (var j = 0; j < Names.Length; j++)
                    gradient[j] += error * ((features[i][j] - means[j]) / deviations[j]);
            }

            intercept -= StepSize * biasGradient;
            for (var j = 0; j < Names.Length; j++)
                weights[j] -= StepSize * (gradient[j] + Lambda * weights[j]);
        }

        if (!double.IsFinite(intercept) || weights.Any(x => !double.IsFinite(x)))
            throw new InvalidDataException("Nonfinite fitted model.");
        return new PlayerShadowModel("class-balanced-l2-logistic-v1", [.. Names], runs, means, deviations,
            weights, intercept, 0.5);
    }

    private static void Accumulate(int truth, bool predictedPositive,
        ref int tp, ref int fp, ref int fn, ref int tn)
    {
        if (truth == 1 && predictedPositive) tp++;
        else if (truth == 0 && predictedPositive) fp++;
        else if (truth == 1) fn++;
        else tn++;
    }

    public static string RenderMarkdown(PlayerShadowLearningReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# M2.13 — FIRST offline trainable ARGUS visual model").AppendLine()
            .AppendLine($"Status: **{report.Status}**. {report.DevelopmentRuns} development run groups; " +
                $"{report.TotalLabeled} reviewed proposals. **NEVER a live player identity or a gameplay controller.**")
            .AppendLine("Fixed: balanced L2 logistic regression, regularization 0.04, step 0.12, " +
                "1500 deterministic batch epochs, decision threshold 0.5, training-fold-only scaling.")
            .AppendLine("Independent unit: **entire run**. Adjacent screenshots are correlated.")
            .AppendLine();
        foreach (var blocker in report.Blockers) sb.AppendLine("- NOT_READY: " + blocker);
        sb.AppendLine()
            .AppendLine("| Dev run | Reviewed | Visible | Absent | Hard absent negatives | Unknown |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var run in report.Coverage)
            sb.AppendLine($"| {run.RunId} | {run.Reviewed} | {run.Visible} | {run.Absent} | " +
                $"{run.HardNegatives} | {run.Unknown} |");
        if (!report.Trained)
            return sb.AppendLine().AppendLine("NO model fitted: insufficient real reviewed run/class coverage.").ToString();

        sb.AppendLine().AppendLine("## Leave-one-*whole-run*-out evaluation")
            .AppendLine("| Held out run | Fit on development runs | Pos | Neg | Model TP/FP/FN/TN | Old heuristic TP/FP/FN/TN |")
            .AppendLine("| --- | --- | ---: | ---: | --- | --- |");
        foreach (var f in report.LeaveOneRunOut)
            sb.AppendLine($"| {f.HeldOutRunId} | {string.Join(", ", f.TrainingRunIds)} | " +
                $"{f.Visible} | {f.Absent} | {f.ModelTruePositive}/{f.ModelFalsePositive}/" +
                $"{f.ModelFalseNegative}/{f.ModelTrueNegative} | {f.BaselineTruePositive}/" +
                $"{f.BaselineFalsePositive}/{f.BaselineFalseNegative}/{f.BaselineTrueNegative} |");
        sb.AppendLine().AppendLine("The final model is an **OFFLINE research artifact** trained on all " +
            "development-run rows; only the fold scores are out-of-run. " +
            "This does not establish generalization to a new game or procedural world. " +
            "Do not promote weights to live steering, do not use held-out run-01 for tuning, " +
            "and collect new truly untouched full runs before any gameplay application.");
        return sb.ToString();
    }
}
