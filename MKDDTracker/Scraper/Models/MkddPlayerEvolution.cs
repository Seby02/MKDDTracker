namespace MKDDTracker.Scraper.Models;

public sealed record MkddPlayerEvolution(
    string PlayerName,
    string CourseName,
    IReadOnlyList<MkddPlayerEvolutionPoint> Points);