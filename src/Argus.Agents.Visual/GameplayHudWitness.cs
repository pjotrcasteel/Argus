using System.Text.RegularExpressions;
using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// Conservative, screenshot-OCR-only confirmation of a conventional numeric
/// health HUD. The user profile must explicitly declare "HP" as gameplay cue.
/// Lower OCR confidence is allowed only for a complete numeric fraction near
/// the top of the viewport; no menu click or combat state is inferred from "HP" alone.
/// </summary>
public static partial class GameplayHudWitness
{
    [GeneratedRegex(@"^\s*HP\s+(?<current>\d{1,4})\s*/\s*(?<maximum>\d{1,4})(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HealthFraction();

    public static bool IsVisible(IReadOnlyList<VisibleText> labels, int width, int height)
    {
        if (width < 100 || height < 200) return false;
        foreach (var label in labels)
        {
            if (label.Confidence < 10 || label.Y < 0 || label.Y > height * 0.18 ||
                label.X < 0 || label.X > width * 0.75 || label.Width > width * 0.75)
                continue;
            var match = HealthFraction().Match(label.Text);
            if (!match.Success ||
                !int.TryParse(match.Groups["current"].Value, out var current) ||
                !int.TryParse(match.Groups["maximum"].Value, out var maximum))
                continue;
            if (maximum > 0 && current >= 0 && current <= maximum) return true;
        }

        return false;
    }
}
