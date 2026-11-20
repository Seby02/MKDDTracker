using System.Text;
using System.Text.RegularExpressions;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public static class HistoricalPlayerNameNormalizer
{
    public static string NormalizePlayerName(string? value) =>
        NormalizeText(value)
            .Replace("’", "'")
            .Replace("‘", "'");

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value
            .Trim()
            .Normalize(NormalizationForm.FormKC);

        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.ToLowerInvariant();
    }
}
