namespace Argus.Core;

/// <summary>Pixel-only palette matching; values describe colors, not a game's objects or internal state.</summary>
public sealed class VisualColorRule
{
    public int MinRed { get; init; }
    public int MinGreen { get; init; }
    public int MinBlue { get; init; }
    public int MinBlueMinusRed { get; init; } = -255;
    public int MinBlueMinusGreen { get; init; } = -255;
    public int MinRedMinusGreen { get; init; } = -255;
    public int MinRedMinusBlue { get; init; } = -255;

    public bool Matches(byte red, byte green, byte blue) =>
        red >= MinRed && green >= MinGreen && blue >= MinBlue &&
        blue - red >= MinBlueMinusRed && blue - green >= MinBlueMinusGreen &&
        red - green >= MinRedMinusGreen && red - blue >= MinRedMinusBlue;
}

/// <summary>A deliberately uncertain, portable vision configuration for color-segmented actors.</summary>
public sealed class CombatVisualSettings
{
    public VisualColorRule? PlayerPalette { get; init; }
    public VisualColorRule[] ThreatPalettes { get; init; } = [];
    public int MinimumPlayerPixels { get; init; } = 70;
    public int MinimumThreatPixels { get; init; } = 70;
    public int ThreatAvoidanceRadius { get; init; } = 120;
    public int MaxPlayerDistanceFromScreenCenter { get; init; } = 0;

    public void Validate()
    {
        if (PlayerPalette is null) throw new ArgumentException("A combat player palette is required.");
        if (MinimumPlayerPixels is < 10 or > 1000 || MinimumThreatPixels is < 10 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(MinimumPlayerPixels));
        if (ThreatAvoidanceRadius is < 25 or > 500) throw new ArgumentOutOfRangeException(nameof(ThreatAvoidanceRadius));
        if (MaxPlayerDistanceFromScreenCenter is < 0 or > 1500)
            throw new ArgumentOutOfRangeException(nameof(MaxPlayerDistanceFromScreenCenter));
    }
}
