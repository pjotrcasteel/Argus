using System.Globalization;
using System.Text;

namespace Argus.Evaluation;

public sealed record AppearanceRunReadiness(string RunId, int ScoredFrames, int MatchedCandidateCenters,
    int UnmatchedCandidateCenters, int MissedHumanCenters, bool EnoughReferences,
    IReadOnlyList<string> Blockers);

public sealed record AppearanceStudyReadiness(string Status, int DevelopmentRuns, int ScoredFrames,
    int MatchedCandidateCenters, int UnmatchedCandidateCenters, int MissedHumanCenters,
    IReadOnlyList<AppearanceRunReadiness> Runs, IReadOnlyList<string> Blockers);

/// <summary>
/// Examines only development reference coverage and candidate agreement, not a trained classifier.
/// Treat neighboring screenshots within a run as correlated evidence, not independent sessions.
/// An UNKNOWN or an absent candidate cannot silently become a negative training label.
/// </summary>
public static class AppearanceReadiness
{
    public const int MinimumIndependentDevelopmentRuns = 3;
    public const int MinimumScoredFramesPerRun = 5;
    public const int MinimumMatchedCandidatesPerRun = 10;
    public const int MinimumUnmatchedCandidatesPerRun = 10;

    public static AppearanceStudyReadiness Assess(IReadOnlyList<AppearanceAuditReport> development)
    {
        ArgumentNullException.ThrowIfNull(development);
        if (development.Count == 0 || development.Any(run => string.IsNullOrWhiteSpace(run.RunId)) ||
            development.Select(run => run.RunId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != development.Count)
            throw new InvalidDataException("At least one uniquely identified development run is required.");

        var runs = development.OrderBy(run => run.RunId, StringComparer.Ordinal).Select(run =>
        {
            if (run.Frames is null || run.Candidates is null ||
                run.LabeledFrames < 0 || run.ScoredFrames < 0 || run.ScoredFrames > run.LabeledFrames ||
                run.MissedHumanCenters < 0 || run.Frames.Count != run.LabeledFrames ||
                run.Frames.Count(f => f.Status == "SCORED") != run.ScoredFrames ||
                run.Frames.Any(f => f.MatchedCandidates < 0 || f.UnmatchedCandidates < 0 || f.MissedHumanCenters < 0) ||
                run.Candidates.Count != run.Frames.Sum(f => f.MatchedCandidates + f.UnmatchedCandidates) ||
                run.Candidates.Count(c => c.MatchedHuman) != run.Frames.Sum(f => f.MatchedCandidates) ||
                run.Candidates.Count(c => !c.MatchedHuman) != run.Frames.Sum(f => f.UnmatchedCandidates) ||
                run.MissedHumanCenters != run.Frames.Sum(f => f.MissedHumanCenters))
                throw new InvalidDataException("Inconsistent appearance audit evidence: " + run.RunId);

            var positives = run.Candidates.Count(c => c.MatchedHuman);
            var negatives = run.Candidates.Count - positives;
            var blockers = new List<string>();
            if (run.ScoredFrames < MinimumScoredFramesPerRun)
                blockers.Add($"Only {run.ScoredFrames} scored frames (< {MinimumScoredFramesPerRun}).");
            if (positives < MinimumMatchedCandidatesPerRun)
                blockers.Add($"Only {positives} human-matched candidate centers (< {MinimumMatchedCandidatesPerRun}).");
            if (negatives < MinimumUnmatchedCandidatesPerRun)
                blockers.Add($"Only {negatives} unmatched candidate centers (< {MinimumUnmatchedCandidatesPerRun}).");

            return new AppearanceRunReadiness(run.RunId, run.ScoredFrames, positives, negatives,
                run.MissedHumanCenters, blockers.Count == 0, blockers);
        }).ToArray();

        var studyBlockers = new List<string>();
        if (runs.Length < MinimumIndependentDevelopmentRuns)
            studyBlockers.Add($"Only {runs.Length} distinct development runs; require >= {MinimumIndependentDevelopmentRuns}.");
        foreach (var run in runs.Where(run => !run.EnoughReferences))
            studyBlockers.AddRange(run.Blockers.Select(blocker => $"{run.RunId}: {blocker}"));

        return new AppearanceStudyReadiness(studyBlockers.Count == 0 ? "SUFFICIENT_FOR_OFFLINE_EXPLORATION" : "NOT_READY",
            runs.Length, runs.Sum(run => run.ScoredFrames), runs.Sum(run => run.MatchedCandidateCenters),
            runs.Sum(run => run.UnmatchedCandidateCenters), runs.Sum(run => run.MissedHumanCenters),
            runs, studyBlockers);
    }

    public static string RenderMarkdown(AppearanceStudyReadiness report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ARGUS — Appearance reference readiness (development only)").AppendLine()
            .AppendLine($"**{report.Status}**. {report.DevelopmentRuns} development runs. " +
                "This is a data-sufficiency gate, **not** a learned classifier, held-out performance result, or " +
                "authorization to change gameplay.")
            .AppendLine()
            .AppendLine("| Run | Scored frames | Matched human centers | Unmatched candidate centers | Missed human centers | Enough examples |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var run in report.Runs)
            sb.AppendLine($"| {run.RunId.Replace("|", "/")} | {run.ScoredFrames} | {run.MatchedCandidateCenters} | " +
                $"{run.UnmatchedCandidateCenters} | {run.MissedHumanCenters} | {(run.EnoughReferences ? "Yes" : "No")} |");

        sb.AppendLine().AppendLine($"Total: {report.MatchedCandidateCenters.ToString(CultureInfo.InvariantCulture)} " +
            $"matched vs {report.UnmatchedCandidateCenters.ToString(CultureInfo.InvariantCulture)} " +
            "unmatched candidate centers. These are proximity-to-human-label outcomes, **not** confirmed " +
            "enemy/non-enemy identities. Missing candidates are not negative examples.");

        if (report.Blockers.Count != 0)
        {
            sb.AppendLine().AppendLine("## Blockers");
            foreach (var blocker in report.Blockers) sb.AppendLine("- " + blocker);
        }

        sb.AppendLine().AppendLine("Run-level separation is mandatory for any future experiment: " +
            "never split adjacent screenshots into train/test at random. Hold-out run-01 is forbidden for tuning; " +
            "collect fresh, untouched future evaluation data separately. Even a 'sufficient' result " +
            "permits offline investigation only, never live steering, autonomous training, or a gameplay claim.");
        return sb.ToString();
    }
}
