namespace Argus.Core;

public interface IVisualGameEnvironment : IAsyncDisposable
{
    Task OpenAsync(Uri address, CancellationToken cancellationToken = default);
    Task<FrameObservation> ObserveAsync(CancellationToken cancellationToken = default);
    Task ExecuteAsync(GameAction action, CancellationToken cancellationToken = default);
}
