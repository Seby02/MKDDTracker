using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Parsing;

public sealed class MkddCoursePageParser
{
    public IReadOnlyList<MkddPerformance> Parse(string html)
{
    var document = new HtmlDocument();
    document.LoadHtml(html);

    var table = document.DocumentNode
        .SelectSingleNode(
            "//table[contains(concat(' ', normalize-space(@class), ' '), ' c ')]");

    if (table is null)
    {
        throw new InvalidOperationException(
            "Impossible de trouver le tableau des performances.");
    }

    var rows = table.SelectNodes(".//tr[td]");

    if (rows is null)
    {
        return [];
    }

    var performances = new List<MkddPerformance>();

    foreach (var row in rows)
    {
        var cells = row.SelectNodes("./td");

        if (cells is null || cells.Count < 7)
        {
            continue;
        }

        var performance = ParseRow(cells);

        performances.Add(performance);
    }

    return performances;
}

    private static MkddPerformance ParseRow(
        HtmlNodeCollection cells)
    {
        var rank = int.Parse(
            GetText(cells[0]),
            CultureInfo.InvariantCulture);

        var playerName = GetText(cells[1]);

        var playerId = ExtractPlayerId(cells[2]);

        var country = GetText(cells[3]);

        var time = ParseTime(
            GetText(cells[4]));

        var standard = GetText(cells[5]);

        var recordDate = DateOnly.ParseExact(
            GetText(cells[6]),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        var videoUrl = ExtractVideoUrl(cells[4]);

        return new MkddPerformance(
            rank,
            playerId,
            playerName,
            country,
            time,
            standard,
            recordDate,
            videoUrl);
    }

    private static string GetText(HtmlNode node)
    {
        var text = HtmlEntity.DeEntitize(node.InnerText);

        // Le site contient parfois des caractères/entités parasites après certaines valeurs.
        text = text
            .Replace("\uFEFF", "")
            .Trim();

        return text;
    }

    private static int ExtractPlayerId(HtmlNode cell)
    {
        var link = cell.SelectSingleNode(".//a[@href]");

        if (link is null)
        {
            throw new InvalidOperationException(
                "Impossible de trouver le lien du joueur.");
        }

        var href = link.GetAttributeValue("href", "");

        var match = Regex.Match(
            href,
            @"pid=(\d+)");

        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Impossible d'extraire le PlayerId depuis '{href}'.");
        }

        return int.Parse(
            match.Groups[1].Value,
            CultureInfo.InvariantCulture);
    }

    private static TimeSpan ParseTime(string value)
    {
        var match = Regex.Match(
            value,
            @"(?<minutes>\d+)'(?<seconds>\d{2})""(?<milliseconds>\d{3})");

        if (!match.Success)
        {
            throw new FormatException(
                $"Format de temps inconnu : '{value}'.");
        }

        var minutes = int.Parse(
            match.Groups["minutes"].Value,
            CultureInfo.InvariantCulture);

        var seconds = int.Parse(
            match.Groups["seconds"].Value,
            CultureInfo.InvariantCulture);

        var milliseconds = int.Parse(
            match.Groups["milliseconds"].Value,
            CultureInfo.InvariantCulture);

        return new TimeSpan(
            0,
            0,
            minutes,
            seconds,
            milliseconds);
    }

    private static string? ExtractVideoUrl(HtmlNode cell)
    {
        var link = cell.SelectSingleNode(
            ".//span[contains(@class, 'videolink')]//a[@href]");

        return link?.GetAttributeValue("href", null);
    }
}