using System.Globalization;
using System.Text;
using Argus.Core;

namespace Argus.Analysis;

public sealed record RunMetadata(string? GameVersion = null, string? GameProfile = null, string? Viewport = null,
    string? PolicyVersion = null);

public sealed record CohortRun(string Id, RunEvidenceSummary Evidence, RunMetadata Metadata);

public sealed record ConfidenceInterval(double Lower, double Upper);

public sealed record CohortTotals(int Runs, int PassedVisualGate, int Frames, int PlayerLocatedFrames,
    int CameraEstimatedFrames, int UpgradeClicks, int TerminalStops, int MissingPlayerFrames,
    double WeightedPlayerCoverage, double CameraEstimateCoverageOfLocatedFrames, ConfidenceInterval VisualGateRate95,
    bool IsMatchedCohort);

public sealed record CohortReport(IReadOnlyList<CohortRun> Runs, CohortTotals Totals);

public static class CohortStatistics
{
    public static CohortReport Summarize(IEnumerable<CohortRun> input)
    {
        var runs = input.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        if (runs.Length == 0) throw new InvalidDataException("At least one independent run is required.");
        if (runs.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidDataException("Run directory names must be unique within a cohort.");

        var count = runs.Length;
        var passed = runs.Count(r => r.Evidence.MeetsVisualCombatGate);
        var frames = runs.Sum(r => r.Evidence.Frames);
        var located = runs.Sum(r => r.Evidence.PlayerLocatedFrames);
        var cameras = runs.Sum(r => r.Evidence.CameraEstimatedFrames);
        var upgrades = runs.Sum(r => r.Evidence.UpgradeClicks);
        var terminals = runs.Sum(r => r.Evidence.TerminalStops);
        var missing = runs.Sum(r => r.Evidence.MissingPlayerExplorationFrames);

        // An unknown version/profile/viewport/policy cannot justify comparison with another run.
        var matched = count >= 2 &&
            runs.All(r => !string.IsNullOrWhiteSpace(r.Metadata.GameVersion) &&
                !string.IsNullOrWhiteSpace(r.Metadata.GameProfile) &&
                !string.IsNullOrWhiteSpace(r.Metadata.Viewport) &&
                !string.IsNullOrWhiteSpace(r.Metadata.PolicyVersion)) &&
            runs.Select(r => (r.Metadata.GameVersion, r.Metadata.GameProfile, r.Metadata.Viewport, r.Metadata.PolicyVersion))
                .Distinct().Count() == 1;

        return new CohortReport(runs, new CohortTotals(count, passed, frames, located, cameras, upgrades, terminals,
            missing, frames == 0 ? 0 : (double)located / frames, located == 0 ? 0 : (double)cameras / located,
            Wilson95(passed, count), matched));
    }

    public static ConfidenceInterval Wilson95(int successes, int total)
    {
        if (total <= 0 || successes < 0 || successes > total)
            throw new ArgumentOutOfRangeException(nameof(total), "A positive valid sample count is required.");

        const double z = 1.959963984540054;
        var proportion = (double)successes / total;
        var z2 = z * z;
        var denominator = 1 + z2 / total;
        var center = (proportion + z2 / (2 * total)) / denominator;
        var half = z * Math.Sqrt(proportion * (1 - proportion) / total + z2 / (4.0 * total * total)) / denominator;
        return new ConfidenceInterval(Math.Max(0, center - half), Math.Min(1, center + half));
    }
}

public static class CohortMarkdown
{
    public static string Render(CohortReport report)
    {
        var totals = report.Totals;
        var builder = new StringBuilder();
        builder.AppendLine("# ARGUS — offline playtest cohort report")
            .AppendLine()
            .AppendLine("> **Interpretation:** These are black-box technical/perception observations, not game victories, skill scores or validated human UX.")
            .AppendLine()
            .AppendLine($"Independent runs: **{totals.Runs}**. Visual gate: **{totals.PassedVisualGate}/{totals.Runs}**.")
            .AppendLine($"Wilson 95% interval for visual-gate rate: **{Pct(totals.VisualGateRate95.Lower)}–{Pct(totals.VisualGateRate95.Upper)}**.")
            .AppendLine($"Player visually located in **{totals.PlayerLocatedFrames}/{totals.Frames}** sampled frames ({Pct(totals.WeightedPlayerCoverage)}).")
            .AppendLine($"Camera shift estimated in **{totals.CameraEstimatedFrames}/{totals.PlayerLocatedFrames}** player-located frames ({Pct(totals.CameraEstimateCoverageOfLocatedFrames)}).")
            .AppendLine($"Upgrade clicks: **{totals.UpgradeClicks}**. Terminal screen stops: **{totals.TerminalStops}**. Missing-player exploration frames: **{totals.MissingPlayerFrames}**.")
            .AppendLine($"Matched research cohort: **{(totals.IsMatchedCohort ? "YES" : "NO")}**.")
            .AppendLine()
            .AppendLine("| Run | Game version | Visual gate | Frames | Player seen | Camera estimates | Upgrades | Terminal stops |")
            .AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var run in report.Runs)
        {
            var e = run.Evidence;
            var version = string.IsNullOrWhiteSpace(run.Metadata.GameVersion) ? "unknown" : run.Metadata.GameVersion;
            builder.AppendLine($"| {Cell(run.Id)} | {Cell(version)} | {(e.MeetsVisualCombatGate ? "pass" : "not met")} | {e.Frames} | {e.PlayerLocatedFrames} | {e.CameraEstimatedFrames} | {e.UpgradeClicks} | {e.TerminalStops} |");
        }

        builder.AppendLine().AppendLine("**Cautions:** Passing the visual gate means ARGUS got basic browser/game evidence, not that it won, dodged successfully or balanced anything. ");
        if (!totals.IsMatchedCohort)
            builder.AppendLine("The run manifest metadata is missing or not identical across runs; this cohort is descriptive only and must not be treated as a controlled comparison.");
        builder.AppendLine("No labels for actual win/loss, enemies, hazards or game fairness are inferred from screenshots alone.");
        return builder.ToString();
    }

    private static string Pct(double rate) => (rate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
