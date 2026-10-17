namespace Argus.Core;

/// <summary>Explainable uncertainty for each visible-screen decision; not a claim about hidden game state.</summary>
public sealed record AgentEvidence(string Phase, string Reason, float Confidence, IReadOnlyList<string> VisibleLabels, float VisualChange = 0,
    CombatScreenEvidence? Combat = null);

public sealed record CombatScreenEvidence(float PlayerX, float PlayerY, int ThreatCandidates, float? NearestCandidatePixels,
    string SteeringSource);

public interface IAgentEvidenceProvider
{
    AgentEvidence? LastEvidence { get; }
}
