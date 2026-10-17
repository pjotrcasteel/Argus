using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// A speculative report derived strictly from earlier VISUALLY DETECTED player pixels.
/// LastKnownPosition is not a current-frame detection and must never be passed to game steering.
/// </summary>
public sealed record PlayerOcclusionShadowFrame(string Status, VisualBlob? ObservedPlayer,
    VisualCandidatePoint? LastKnownPosition, int ConsecutiveMissingFrames, float HypothesisConfidence)
{
    public bool IsBridgeHypothesis => Status is "BRIDGE_ONE" or "BRIDGE_TWO";
}

/// <summary>
/// Offline-only, zero-velocity, maximum-two-frame player presence hypothesis.
/// Two consecutive compatible pixel detections are needed to arm. Missing frames NEVER
/// enter the real player detector as a fabricated previousPlayer, and no actions are issued.
/// </summary>
public sealed class PlayerOcclusionShadowTracker
{
    private const float MaximumObservedStepPixels = 55f;
    private const float MaximumReappearanceDistancePixels = 55f;
    private const int MaximumMissingFrames = 2;
    private VisualBlob? _lastObserved;
    private int _confirmedStreak;
    private int _missing;

    public void Reset()
    {
        _lastObserved = null;
        _confirmedStreak = 0;
        _missing = 0;
    }

    public PlayerOcclusionShadowFrame Observe(VisualBlob? observedPlayer, bool exploring)
    {
        if (!exploring)
        {
            Reset();
            return new PlayerOcclusionShadowFrame("NOT_COMBAT_PHASE", null, null, 0, 0);
        }

        if (observedPlayer is null)
        {
            if (_lastObserved is null || _confirmedStreak < 2)
            {
                Reset();
                return new PlayerOcclusionShadowFrame("NO_CONFIRMED_PRIOR", null, null, 0, 0);
            }

            _missing++;
            if (_missing > MaximumMissingFrames)
            {
                Reset();
                return new PlayerOcclusionShadowFrame("BRIDGE_EXPIRED", null, null, 0, 0);
            }

            return new PlayerOcclusionShadowFrame(_missing == 1 ? "BRIDGE_ONE" : "BRIDGE_TWO",
                null, new VisualCandidatePoint(_lastObserved.X, _lastObserved.Y), _missing,
                _missing == 1 ? 0.35f : 0.15f);
        }

        if (_lastObserved is null)
        {
            _lastObserved = observedPlayer;
            _confirmedStreak = 1;
            _missing = 0;
            return new PlayerOcclusionShadowFrame("OBSERVED_UNARMED", observedPlayer, null, 0, 0);
        }

        var near = Distance(observedPlayer, _lastObserved) <=
            (_missing > 0 ? MaximumReappearanceDistancePixels : MaximumObservedStepPixels);
        var compatible = ComparableSize(observedPlayer, _lastObserved);
        var previousMissing = _missing;
        _lastObserved = observedPlayer;
        _missing = 0;
        if (!near || !compatible)
        {
            _confirmedStreak = 1;
            return new PlayerOcclusionShadowFrame("OBSERVED_DISCONTINUITY", observedPlayer, null, 0, 0);
        }

        // A reappearance remains an unverified association. Require an additional
        // uninterrupted screenshot before permitting ANY further speculative bridge.
        _confirmedStreak = previousMissing > 0 ? 1 : Math.Min(100, _confirmedStreak + 1);
        var status = previousMissing > 0 ? "REAPPEARED_NEAR_PRIOR"
            : _confirmedStreak >= 2 ? "OBSERVED_ARMED" : "OBSERVED_UNARMED";
        return new PlayerOcclusionShadowFrame(status, observedPlayer, null, 0, 0);
    }

    private static float Distance(VisualBlob a, VisualBlob b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static bool ComparableSize(VisualBlob a, VisualBlob b) =>
        a.Width <= b.Width * 2 && b.Width <= a.Width * 2 &&
        a.Height <= b.Height * 2 && b.Height <= a.Height * 2;
}
