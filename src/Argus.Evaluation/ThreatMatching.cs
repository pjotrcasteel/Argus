using Argus.Core;

namespace Argus.Evaluation;

/// <summary>Deterministic, one-to-one nearest-neighbor screen-pixel matching.</summary>
public sealed record ThreatMatchResult(int TruePositives, IReadOnlyList<VisualCandidatePoint> FalsePositives,
    IReadOnlyList<VisualCandidatePoint> FalseNegatives)
{
    // Indices into the caller's prediction list; enables a per-candidate audit without
    // reimplementing or altering the shared one-to-one matcher.
    public IReadOnlyList<int> MatchedPredictionIndices { get; init; } = [];
}

public static class ThreatMatching
{
    public static ThreatMatchResult Match(IReadOnlyList<VisualCandidatePoint> truth, IReadOnlyList<VisualCandidatePoint> predictions,
        float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        var pairs = new List<(int Actual, int Predicted, float Distance)>();
        for (var i = 0; i < truth.Count; i++)
            for (var j = 0; j < predictions.Count; j++)
            {
                var dx = truth[i].X - predictions[j].X;
                var dy = truth[i].Y - predictions[j].Y;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= radius) pairs.Add((i, j, distance));
            }

        var actualUsed = new HashSet<int>();
        var predictedUsed = new HashSet<int>();
        foreach (var (actual, predicted, _) in pairs.OrderBy(p => p.Distance).ThenBy(p => p.Actual).ThenBy(p => p.Predicted))
        {
            if (actualUsed.Contains(actual) || predictedUsed.Contains(predicted)) continue;
            actualUsed.Add(actual);
            predictedUsed.Add(predicted);
        }

        return new ThreatMatchResult(actualUsed.Count,
            predictions.Where((_, i) => !predictedUsed.Contains(i)).ToArray(),
            truth.Where((_, i) => !actualUsed.Contains(i)).ToArray())
        {
            MatchedPredictionIndices = predictedUsed.Order().ToArray()
        };
    }
}
