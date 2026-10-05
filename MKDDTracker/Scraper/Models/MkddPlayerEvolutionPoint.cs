namespace MKDDTracker.Scraper.Models;

public sealed record MkddPlayerEvolutionPoint(
    DateTime Date,
    int Rank,
    TimeSpan Time,
    int? RankChange,
    TimeSpan? TimeChange);