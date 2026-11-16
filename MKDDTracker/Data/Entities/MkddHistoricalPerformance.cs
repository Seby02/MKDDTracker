namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddHistoricalPerformance
{
    public long Id { get; set; }

    //
    // Clé déterministe créée par l'importeur.
    // Elle garantit que rejouer l'import ne crée
    // pas de doublons.
    //

    public required string ExternalKey { get; set; }

    public int HistoricalPlayerAliasId { get; set; }

    public MkddHistoricalPlayerAlias HistoricalPlayerAlias { get; set; } = null!;

    public int CourseId { get; set; }

    public MkddCourse Course { get; set; } = null!;

    //
    // ThreeLap ou Flap.
    //

    public required string TimeType { get; set; }

    //
    // 50, 60 ou null.
    //

    public int? Frequency { get; set; }

    public TimeSpan Time { get; set; }

    //
    // Première publication connue dans Yahoo Groups.
    // Ce n'est pas nécessairement la date exacte
    // de réalisation du chrono.
    //

    public DateTimeOffset FirstSeenAt { get; set; }

    public required string DateSource { get; set; }

    public int SequenceNumber { get; set; }

    public TimeSpan? PreviousTime { get; set; }

    public TimeSpan? Improvement { get; set; }

    public int OccurrenceCount { get; set; }

    public required string SourceSystem { get; set; }

    public required string ImportStatus { get; set; }

    //
    // Stockage JSON simple des raisons de review.
    //

    public string? ImportReasonsJson { get; set; }

    public string? ValidationWarningsJson { get; set; }

    public ICollection<MkddHistoricalPerformanceSource> Sources { get; set; } = [];
}