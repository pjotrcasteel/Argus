namespace Argus.Core;

/// <summary>Only the visible viewport, not the game's DOM or internal state.</summary>
public sealed record FrameObservation(DateTimeOffset TimestampUtc, int Width, int Height, byte[] PngBytes);
