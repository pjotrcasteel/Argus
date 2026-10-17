namespace Argus.Core;

/// <summary>
/// Configuration of visible game labels and ordinary controls; no internal state, DOM, selectors or engine access.
/// MovementKeys must be ordered Up, Left, Down, Right.
/// </summary>
public sealed class VisualGameProfile
{
    public string Name { get; init; } = "Generic";
    public string[] MenuActions { get; init; } = ["Begin Hunt", "Continue", "Play", "Start", "Next", "Enter"];
    public string[] GameplayCues { get; init; } = ["HP", "Health", "Wave"];
    public string[] RouteCues { get; init; } = [];
    public string[] MovementKeys { get; init; } = ["w", "a", "s", "d"];
    public int MinimumTextConfidence { get; init; } = 45;
    public int MovementMilliseconds { get; init; } = 300;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || MenuActions.Length == 0 || MenuActions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A game profile requires a name and at least one non-empty menu label.");
        if (MovementKeys.Length != 4 || MovementKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("MovementKeys must contain four keys in Up, Left, Down, Right order.");
        if (MinimumTextConfidence is < 0 or > 100 || MovementMilliseconds is < 50 or > 1500)
            throw new ArgumentOutOfRangeException(nameof(MovementMilliseconds), "Profile thresholds are outside safety limits.");
    }
}
