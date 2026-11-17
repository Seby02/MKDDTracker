using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services.Ranking;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddCoursePageParserTests
{
    private readonly MkddCoursePageParser _parser = new();

    [Fact]
    public void Parse_WithNormalRow_ExtractsAllFields()
    {
        var result = _parser.Parse(Html(Row(
            rank: 2,
            playerName: "Alice",
            playerId: 42,
            country: "France",
            time: "1'14\"144",
            standard: "God",
            recordDate: "2026-09-15")));

        var performance = Assert.Single(result);
        Assert.Equal(2, performance.Rank);
        Assert.Equal(42, performance.SitePlayerId);
        Assert.Equal("Alice", performance.PlayerName);
        Assert.Equal("France", performance.Country);
        Assert.Equal(TimeSpan.FromMilliseconds(74_144), performance.Time);
        Assert.Equal("God", performance.Standard);
        Assert.Equal(new DateOnly(2026, 9, 15), performance.RecordDate);
    }

    [Fact]
    public void Parse_WithVideo_ReturnsVideoUrl()
    {
        var result = _parser.Parse(Html(Row(videoUrl: "https://example.test/run")));

        Assert.Equal("https://example.test/run", Assert.Single(result).VideoUrl);
    }

    [Fact]
    public void Parse_WithoutVideo_ReturnsNullVideoUrl()
    {
        var result = _parser.Parse(Html(Row()));

        Assert.Null(Assert.Single(result).VideoUrl);
    }

    [Fact]
    public void Parse_WithSeveralPlayers_ReturnsEveryPerformance()
    {
        var html = Html(
            Row(rank: 1, playerName: "Alice", playerId: 1),
            Row(rank: 2, playerName: "Bob", playerId: 2, time: "1'15\"000"));

        var result = _parser.Parse(html);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "Alice", "Bob" }, result.Select(x => x.PlayerName));
    }

    [Fact]
    public void Parse_WithEmptyPerformanceTable_ReturnsEmptyResult()
    {
        var result = _parser.Parse(Html());

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithIncompleteRow_IgnoresTheRow()
    {
        var html = Html(
            "<tr><td>1</td><td>Incomplete</td></tr>",
            Row(playerName: "Complete"));

        var result = _parser.Parse(html);

        Assert.Equal("Complete", Assert.Single(result).PlayerName);
    }

    [Fact]
    public void Parse_WithInvalidTime_ThrowsFormatException()
    {
        var exception = Assert.Throws<FormatException>(
            () => _parser.Parse(Html(Row(time: "74.144"))));

        Assert.Contains("Format de temps inconnu", exception.Message);
    }

    [Fact]
    public void Parse_WithInvalidDate_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(
            () => _parser.Parse(Html(Row(recordDate: "15/09/2026"))));
    }

    [Fact]
    public void Parse_WithoutPlayerLink_ThrowsInvalidOperationException()
    {
        var row = """
            <tr>
              <td>1</td><td>Alice</td><td>No link</td><td>France</td>
              <td>1'14"144</td><td>God</td><td>2026-09-15</td>
            </tr>
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => _parser.Parse(Html(row)));

        Assert.Contains("lien du joueur", exception.Message);
    }

    [Fact]
    public void Parse_WithoutPerformanceTable_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _parser.Parse("<html><body><table></table></body></html>"));
    }

    [Fact]
    public void Parse_WithUtf8BomAndWhitespace_CleansTextFields()
    {
        var html = Html(Row(playerName: " \uFEFFAlice ", country: " France "));

        var performance = Assert.Single(_parser.Parse(html));

        Assert.Equal("Alice", performance.PlayerName);
        Assert.Equal("France", performance.Country);
    }

    [Fact]
    public void Parse_LuigiCircuitSample_ReturnsOneHundredPerformancesAndNinetyFiveRanks()
    {
        var samplePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "luigi-circuit.html");
        var html = File.ReadAllText(samplePath);

        var result = _parser.Parse(html);

        Assert.Equal(100, result.Count);
        Assert.Equal(95, result.Select(x => x.Rank).Distinct().Count());
    }

    [Fact]
    public void Parse_LuigiCircuitSample_RanksMatchTheCentralCalculator()
    {
        var samplePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "luigi-circuit.html");
        var html = File.ReadAllText(samplePath);
        var result = _parser.Parse(html);

        var calculated = MkddRankingCalculator
            .CalculateRanks(result, x => x.Time);

        Assert.Equal(
            result.OrderBy(x => x.Time).Select(x => x.Rank),
            calculated.Select(x => x.Rank));
    }

    private static string Html(params string[] rows) =>
        $"<html><body><table class=\"c\"><tbody>{string.Concat(rows)}</tbody></table></body></html>";

    private static string Row(
        int rank = 1,
        string playerName = "Alice",
        int playerId = 42,
        string country = "France",
        string time = "1'14\"144",
        string standard = "God",
        string recordDate = "2026-09-15",
        string? videoUrl = null)
    {
        var video = videoUrl is null
            ? string.Empty
            : $"<span class=\"videolink\"><a href=\"{videoUrl}\">Video</a></span>";

        return $"""
            <tr>
              <td>{rank}</td>
              <td>{playerName}</td>
              <td><a href="profile.php?pid={playerId}">Profile</a></td>
              <td>{country}</td>
              <td>{time}{video}</td>
              <td>{standard}</td>
              <td>{recordDate}</td>
            </tr>
            """;
    }
}
