namespace Argus.Core;

public enum GameActionKind
{
    Wait,
    MovePointer,
    Click,
    KeyDown,
    KeyUp,
    HoldKey
}

public sealed record GameAction(GameActionKind Kind, string? Key = null, float X = 0, float Y = 0, int DurationMs = 0);
