namespace Argus.Core;

public sealed record VisibleText(string Text, int X, int Y, int Width, int Height, float Confidence)
{
    public float CenterX => X + Width / 2f;
    public float CenterY => Y + Height / 2f;
}

public interface IFrameTextReader
{
    Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default);
}
