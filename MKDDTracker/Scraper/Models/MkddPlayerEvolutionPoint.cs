namespace MKDDTracker.Scraper.Models;

public sealed record MkddPlayerEvolutionPoint(
    DateOnly RankingDate,
    int Rank,
    TimeSpan Time,
    int? RankChange,
    TimeSpan? TimeChange);