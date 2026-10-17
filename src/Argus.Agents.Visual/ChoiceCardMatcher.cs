using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// Finds the first visible card title from OCR structure, not preconfigured card names or fixed coordinates.
/// Each card has a small categorical label and a larger title lower in the same visual region.
/// </summary>
public static class ChoiceCardMatcher
{
    public static VisibleText? FindFirst(IReadOnlyList<VisibleText> lines, IReadOnlyList<string> cardTypes, int minimumConfidence)
    {
        var anchors = lines
            .Where(t => t.Confidence >= minimumConfidence)
            .Where(t => cardTypes.Any(type => string.Equals(t.Text.Trim(), type, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Y).ToArray();

        foreach (var anchor in anchors)
        {
            var candidates = lines
                .Where(t => t.Confidence >= minimumConfidence)
                .Where(t => t.Y > anchor.Y + anchor.Height + 20 && t.Y < anchor.Y + 145)
                .Where(t => t.Height >= 14 && t.Height < 42)
                .Where(t => t.Text.Length is >= 3 and <= 60)
                .Where(t => t.Text.Count(char.IsLetter) >= t.Text.Length / 2)
                .OrderByDescending(t => t.Height)
                .ThenBy(t => t.Y)
                .ToArray();

            if (candidates.Length > 0) return candidates[0];
        }

        return null;
    }
}
