namespace Argus.Agents.Visual;

/// <summary>Supplemental shadow candidates confirmed by consecutive visual observations only.</summary>
public sealed record ShadowTemporalObservation(IReadOnlyList<VisualBlob> Confirmed,
    int PendingCandidates, int StationaryCandidates)
{
    // Research-only proposals: repeated still sprites, not confirmed enemies.
    public IReadOnlyList<VisualBlob> PersistentStationaryCandidates { get; init; } = [];
}

/// <summary>
/// Per-run, causal association of *supplemental* candidates. Does not modify active combat vision,
/// claim enemy identities, or issue any player actions. Only the immediately previous valid
/// gameplay frame can contribute; a menu, missing player or missing frame resets all tracks.
/// </summary>
public sealed class ShadowTemporalCandidateTracker
{
    private const float MaxStepDistance = 55f;
    private const float MinimumTotalMovement = 3f;
    private const int MinimumStationaryObservations = 3;
    private readonly List<Track> _tracks = [];

    public void Reset() => _tracks.Clear();

    public ShadowTemporalObservation Observe(ShadowThreatObservation frame)
    {
        if (frame.Baseline.Player is null)
        {
            Reset();
            return new ShadowTemporalObservation([], 0, 0);
        }

        var updated = new List<Track>();
        var pairs = new List<(int Previous, int Current, float Distance)>();
        for (var i = 0; i < _tracks.Count; i++)
            for (var j = 0; j < frame.AddedCandidates.Count; j++)
            {
                var previous = _tracks[i].Blob;
                var current = frame.AddedCandidates[j];
                var dx = previous.X - current.X;
                var dy = previous.Y - current.Y;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= MaxStepDistance && ComparableSize(previous, current))
                    pairs.Add((i, j, distance));
            }

        var previousUsed = new HashSet<int>();
        var currentUsed = new HashSet<int>();
        var associated = new Dictionary<int, Track>();
        foreach (var pair in pairs.OrderBy(p => p.Distance).ThenBy(p => p.Previous).ThenBy(p => p.Current))
        {
            if (!previousUsed.Add(pair.Previous)) continue;
            if (!currentUsed.Add(pair.Current))
            {
                previousUsed.Remove(pair.Previous);
                continue;
            }

            var old = _tracks[pair.Previous];
            var current = frame.AddedCandidates[pair.Current];
            associated[pair.Current] = new Track(current, Math.Min(100, old.SeenConsecutive + 1),
                old.TotalMovement + pair.Distance, old.AppearanceStable && SimilarAppearance(old.Blob, current));
        }

        var confirmed = new List<VisualBlob>();
        var stableStationary = new List<VisualBlob>();
        var pending = 0;
        var stationary = 0;
        for (var j = 0; j < frame.AddedCandidates.Count; j++)
        {
            var track = associated.GetValueOrDefault(j) ?? new Track(frame.AddedCandidates[j], 1, 0, true);
            updated.Add(track);
            if (track.SeenConsecutive >= 2 && track.TotalMovement >= MinimumTotalMovement)
                confirmed.Add(track.Blob);
            else if (track.SeenConsecutive >= 2)
            {
                stationary++;
                if (track.SeenConsecutive >= MinimumStationaryObservations && track.AppearanceStable)
                    stableStationary.Add(track.Blob);
            }
            else
                pending++;
        }

        _tracks.Clear();
        _tracks.AddRange(updated);
        return new ShadowTemporalObservation(confirmed, pending, stationary)
        {
            PersistentStationaryCandidates = stableStationary
        };
    }

    private static bool ComparableSize(VisualBlob a, VisualBlob b) =>
        a.Width <= b.Width * 2 && b.Width <= a.Width * 2 &&
        a.Height <= b.Height * 2 && b.Height <= a.Height * 2;

    // Simple geometric/pixel-count stability; no color identity or game-specific sprite classes.
    // Matching backgrounds also satisfy it, hence persistent still objects remain research-only.
    private static bool SimilarAppearance(VisualBlob previous, VisualBlob current) =>
        Math.Abs(previous.Width - current.Width) <= Math.Max(2, previous.Width / 4) &&
        Math.Abs(previous.Height - current.Height) <= Math.Max(2, previous.Height / 4) &&
        current.MatchingPixels >= previous.MatchingPixels * 0.75f &&
        current.MatchingPixels <= previous.MatchingPixels * 1.25f;

    private sealed record Track(VisualBlob Blob, int SeenConsecutive, float TotalMovement, bool AppearanceStable);
}
