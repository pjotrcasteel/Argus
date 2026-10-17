namespace Argus.Core;

/// <summary>Returns null to finish. Implementations may only use observations available to a real player.</summary>
public interface IVisualAgent
{
    ValueTask<GameAction?> DecideAsync(FrameObservation observation, CancellationToken cancellationToken = default);
}
