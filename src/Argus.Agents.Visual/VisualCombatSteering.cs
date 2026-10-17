namespace Argus.Agents.Visual;

/// <summary>
/// Potential-field fallback steering based ONLY on visible, uncertain candidate blobs.
/// Direction indices follow Up, Left, Down, Right. Null means no credible nearby risk.
/// </summary>
public static class VisualCombatSteering
{
    public static int? SelectEvasionDirection(CombatVisualObservation observation, int radius)
    {
        var player = observation.Player;
        if (player is null) return null;

        var awayX = 0f;
        var awayY = 0f;
        var relevant = 0;
        foreach (var candidate in observation.ThreatCandidates)
        {
            var dx = player.X - candidate.X;
            var dy = player.Y - candidate.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < 1 || distanceSquared > radius * radius) continue;

            // Closer candidates have more influence; this is not a claim of enemy classification.
            var weight = candidate.Confidence / (distanceSquared + 400);
            awayX += dx * weight;
            awayY += dy * weight;
            relevant++;
        }

        if (relevant == 0 || MathF.Abs(awayX) + MathF.Abs(awayY) < 0.00001f) return null;

        return MathF.Abs(awayX) >= MathF.Abs(awayY)
            ? awayX >= 0 ? 3 : 1
            : awayY >= 0 ? 2 : 0;
    }
}
