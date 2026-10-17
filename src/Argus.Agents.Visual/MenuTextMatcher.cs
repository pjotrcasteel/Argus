using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>Chooses coordinates from OCR text boxes; never relies on DOM selectors or fixed screen positions.</summary>
public static class MenuTextMatcher
{
    public static VisibleText? FindBest(IReadOnlyList<VisibleText> recognized, VisualGameProfile profile)
    {
        for (var priority = 0; priority < profile.MenuActions.Length; priority++)
        {
            var sought = Normalize(profile.MenuActions[priority]);
            var matches = recognized
                .Where(t => t.Confidence >= profile.MinimumTextConfidence)
                .Where(t => Matches(Normalize(t.Text), sought))
                .OrderByDescending(t => t.Confidence)
                .ToArray();

            if (matches.Length > 0) return matches[0];
        }

        return null;
    }

    private static bool Matches(string recognized, string sought)
    {
        // A generic single-word command must be an entire OCR line, not the first word of instructions.
        if (!sought.Contains(' ')) return recognized == sought;
        if (recognized.Contains(sought, StringComparison.Ordinal)) return true;

        // OCR can preserve intervening UI words: "BEGIN FIRST HUNT" must match "Begin Hunt".
        var expected = sought.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        foreach (var word in recognized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word != expected[index]) continue;
            index++;
            if (index == expected.Length) return true;
        }

        return false;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
