namespace Argus.Agents.Visual;

/// <summary>Motion relative to the visible scene, not proof that any detected object is an enemy.</summary>
public sealed record ThreatMotionEvidence(VisualSceneShift? Background, int Matched, int Moving, int Stationary);

/// <summary>Matches candidates across frames after compensating for background translation.</summary>
public static class ThreatMotionAnalyzer
{
    public static ThreatMotionEvidence Analyze(CombatVisualObservation previous, CombatVisualObservation current, VisualSceneShift? scene)
    {
        if (scene is null || scene.Confidence < 0.3f || previous.Player is null || current.Player is null)
            return new ThreatMotionEvidence(scene, 0, 0, 0);

        var used = new HashSet<int>();
        var matched = 0;
        var moving = 0;
        var stationary = 0;
        foreach (var observed in current.ThreatCandidates)
        {
            var closest = -1;
            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < previous.ThreatCandidates.Count; i++)
            {
                if (used.Contains(i)) continue;
                var prior = previous.ThreatCandidates[i];
                var dx = observed.X - prior.X - scene.X;
                var dy = observed.Y - prior.Y - scene.Y;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }
            if (closest < 0 || closestDistance > 35) continue;
            used.Add(closest);
            matched++;
            if (closestDistance >= 5) moving++;
            else stationary++;
        }
        return new ThreatMotionEvidence(scene, matched, moving, stationary);
    }
}
