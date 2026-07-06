using System.Globalization;
using System.Text;

namespace OpenGameHUB.Services;

internal static class GameSearchHelper
{
    public static bool MatchesTitle(string title, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        if (string.IsNullOrWhiteSpace(title))
            return false;

        var normalizedTitle = NormalizeForSearch(title);
        var normalizedQuery = NormalizeForSearch(query);
        if (string.IsNullOrEmpty(normalizedQuery))
            return false;

        if (normalizedTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return true;

        var compactTitle = Compact(normalizedTitle);
        var compactQuery = Compact(normalizedQuery);
        if (!string.IsNullOrEmpty(compactQuery)
            && compactTitle.Contains(compactQuery, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var titleTokens = Tokenize(normalizedTitle);
        var queryTokens = Tokenize(normalizedQuery);
        return queryTokens.Length > 0
               && queryTokens.All(queryToken =>
                   titleTokens.Any(titleToken =>
                       titleToken.Contains(queryToken, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = RemoveDiacritics(MetadataSearchHelper.NormalizeTitle(value)).ToLowerInvariant();
        foreach (var ch in new[] { ':', '-', '–', '—', '|', '(', ')', '[', ']', '.', ',', '!', '?', '\'', '"', '™', '®' })
            normalized = normalized.Replace(ch, ' ');

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Compact(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray());

    private static string[] Tokenize(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
