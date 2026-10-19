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

    /// <summary>
    /// Strict, game-independent backup for an already-confirmed choice screen
    /// whose tiny category captions were lost by OCR. Only a conspicuous
    /// title-cased, multiword line within the body of the screenshot qualifies.
    /// A heading or explanatory sentence must not become a click target.
    /// </summary>
    public static VisibleText? FindFirstTitleOnly(
        IReadOnlyList<VisibleText> lines, int minimumConfidence, int width, int height)
    {
        if (width < 200 || height < 350) return null;

        return lines
            .Where(t => t.Confidence >= minimumConfidence && t.Y >= height * 0.17f &&
                t.Y < height * 0.85f && t.X >= width * 0.10f && t.X < width * 0.90f)
            .Where(t => t.Height >= 12 && t.Height <= 42 && t.Width >= 35 && t.Width < width * 0.75f)
            .Where(t => IsShortTitle(t.Text))
            .OrderBy(t => t.Y)
            .FirstOrDefault();
    }

    private static bool IsShortTitle(string text)
    {
        if (text.Length is < 5 or > 48 || text.Any(c => !char.IsLetter(c) && c != ' ' && c != '-'))
            return false;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 4) return false;
        return words.All(word => word.Length >= 2 && char.IsUpper(word[0]) &&
            word.Skip(1).All(c => c == '-' || char.IsLower(c)));
    }
}
