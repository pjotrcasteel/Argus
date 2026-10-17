namespace Argus.Core;

/// <summary>
/// Evaluate each stored screenshot exactly once, including the bounded final screenshot.
/// At the step limit the proposed input is discarded, but its evidence still belongs to
/// the current image rather than the previous frame.
/// </summary>
public static class FrameDecisionCapture
{
    public static async ValueTask<CapturedFrameDecision> CaptureAsync(IVisualAgent agent, FrameObservation frame, bool atStepLimit,
        CancellationToken cancellationToken = default)
    {
        var proposedAction = await agent.DecideAsync(frame, cancellationToken);
        return new CapturedFrameDecision(atStepLimit ? null : proposedAction,
            (agent as IAgentEvidenceProvider)?.LastEvidence, atStepLimit);
    }
}

public sealed record CapturedFrameDecision(GameAction? Action, AgentEvidence? Evidence, bool InputSuppressedAtStepLimit);
