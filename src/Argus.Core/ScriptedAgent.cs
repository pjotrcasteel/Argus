namespace Argus.Core;

/// <summary>Deterministic adapter smoke-test agent; does not claim to understand screen contents or learn.</summary>
public sealed class ScriptedAgent(IEnumerable<GameAction> actions) : IVisualAgent
{
    private readonly IEnumerator<GameAction> _actions = actions.GetEnumerator();

    public ValueTask<GameAction?> DecideAsync(FrameObservation observation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_actions.MoveNext() ? _actions.Current : null);
    }
}
