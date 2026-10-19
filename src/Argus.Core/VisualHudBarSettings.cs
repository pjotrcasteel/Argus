namespace Argus.Core;

public sealed class VisualHudBarSettings
{
    public int LeftPercent { get; init; } = 2;
    public int RightPercent { get; init; } = 75;
    public int TopPercent { get; init; } = 3;
    public int BottomPercent { get; init; } = 9;
    public int MinimumConsecutivePixels { get; init; } = 14;
    public VisualColorRule[] FillPalettes { get; init; } = [];

    public void Validate()
    {
        if (LeftPercent < 0 || LeftPercent >= 100 || RightPercent <= LeftPercent || RightPercent > 100 ||
            TopPercent < 0 || TopPercent >= 100 || BottomPercent <= TopPercent || BottomPercent > 100 ||
            MinimumConsecutivePixels < 4 || MinimumConsecutivePixels > 500 ||
            FillPalettes is not { Length: > 0 and <= 8 } || FillPalettes.Any(x => x is null))
            throw new ArgumentException("Invalid HUD bar witness configuration.");
    }
}
