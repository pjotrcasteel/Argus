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
                .Where(t => Normalize(t.Text).Contains(sought, StringComparison.Ordinal))
                .OrderByDescending(t => t.Confidence)
                .ToArray();

            if (matches.Length > 0) return matches[0];
        }

        return null;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
