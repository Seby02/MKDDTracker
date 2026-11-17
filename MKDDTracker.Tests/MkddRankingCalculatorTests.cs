using MKDDTracker.Scraper.Services.Ranking;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddRankingCalculatorTests
{
    [Fact]
    public void CalculateRanks_WithDistinctTimes_ReturnsSequentialRanks()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 74_500),
            Performance("Claire", 75_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithTwoPlayersTied_ReturnsCompetitionRanks()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 74_500),
            Performance("Claire", 74_500),
            Performance("David", 75_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 2, 2, 4 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithThreePlayersTied_SkipsThreeOccupiedPositions()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 74_500),
            Performance("Claire", 74_500),
            Performance("David", 74_500),
            Performance("Eve", 75_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 2, 2, 2, 5 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithTieForFirst_ReturnsOneOneThree()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 74_000),
            Performance("Claire", 75_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 1, 3 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithTieForLast_GivesBothPlayersTheSameRank()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 75_000),
            Performance("Claire", 75_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 2, 2 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithSeveralTieGroups_AppliesOneRuleEverywhere()
    {
        var performances = new[]
        {
            Performance("Alice", 74_000),
            Performance("Bob", 74_000),
            Performance("Claire", 74_500),
            Performance("David", 75_000),
            Performance("Eve", 75_000),
            Performance("Franck", 76_000)
        };

        var result = Calculate(performances);

        Assert.Equal(new[] { 1, 1, 3, 4, 4, 6 }, result.Select(x => x.Rank));
    }

    [Fact]
    public void CalculateRanks_WithEmptyInput_ReturnsEmptyResult()
    {
        var result = Calculate([]);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateRanks_WithOnePlayer_ReturnsFirstPlace()
    {
        var result = Calculate([Performance("Alice", 74_000)]);

        Assert.Equal(1, Assert.Single(result).Rank);
    }

    [Fact]
    public void CalculateRanks_DoesNotUsePlayerIdAsTieBreaker()
    {
        var performances = new[]
        {
            new TestPerformance(999, "High id", TimeSpan.FromMilliseconds(74_500)),
            new TestPerformance(1, "Low id", TimeSpan.FromMilliseconds(74_500))
        };

        var result = Calculate(performances);

        Assert.All(result, performance => Assert.Equal(1, performance.Rank));
    }

    [Fact]
    public void CalculateRanks_IsIndependentOfInsertionOrder()
    {
        var firstOrder = new[]
        {
            Performance("Alice", 75_000),
            Performance("Bob", 74_500),
            Performance("Claire", 74_500),
            Performance("David", 74_000)
        };
        var secondOrder = firstOrder.Reverse().ToArray();

        var firstResult = Calculate(firstOrder).ToDictionary(x => x.Item.Name, x => x.Rank);
        var secondResult = Calculate(secondOrder).ToDictionary(x => x.Item.Name, x => x.Rank);

        Assert.Equal(
            firstResult.OrderBy(x => x.Key),
            secondResult.OrderBy(x => x.Key));
    }

    [Fact]
    public void CalculateRank_CountsOnlyStrictlyFasterCompetitors()
    {
        var competitorTimes = new[]
        {
            TimeSpan.FromMilliseconds(74_000),
            TimeSpan.FromMilliseconds(74_500),
            TimeSpan.FromMilliseconds(74_500),
            TimeSpan.FromMilliseconds(75_000)
        };

        var rank = MkddRankingCalculator.CalculateRank(
            competitorTimes,
            TimeSpan.FromMilliseconds(74_500));

        Assert.Equal(2, rank);
    }

    private static IReadOnlyList<MkddRankedItem<TestPerformance>> Calculate(
        IEnumerable<TestPerformance> performances) =>
        MkddRankingCalculator.CalculateRanks(performances, x => x.Time);

    private static TestPerformance Performance(string name, int milliseconds) =>
        new(0, name, TimeSpan.FromMilliseconds(milliseconds));

    private sealed record TestPerformance(int PlayerId, string Name, TimeSpan Time);
}
