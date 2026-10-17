namespace Argus.Core;

/// <summary>Explainable uncertainty for each visible-screen decision; not a claim about hidden game state.</summary>
public sealed record AgentEvidence(string Phase, string Reason, float Confidence, IReadOnlyList<string> VisibleLabels, float VisualChange = 0,
    CombatScreenEvidence? Combat = null);

public sealed record CombatScreenEvidence(float PlayerX, float PlayerY, int ThreatCandidates, float? NearestCandidatePixels,
    string SteeringSource)
{
    public int? BackgroundShiftX { get; init; }
    public int? BackgroundShiftY { get; init; }
    public float? BackgroundShiftConfidence { get; init; }
    public int MotionMatchedCandidates { get; init; }
    public int MotionMovingCandidates { get; init; }
    public int MotionStationaryCandidates { get; init; }
    // Pixel centers from the actual visual detector, NOT verified enemy locations.
    // Null in earlier traces means these detections were not recorded, not zero candidates.
    public IReadOnlyList<VisualCandidatePoint>? ThreatLocations { get; init; }
}

public sealed record VisualCandidatePoint(float X, float Y);

public interface IAgentEvidenceProvider
{
    AgentEvidence? LastEvidence { get; }
}
