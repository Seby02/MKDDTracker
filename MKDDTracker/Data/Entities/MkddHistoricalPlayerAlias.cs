namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddHistoricalPlayerAlias
{
    public int Id { get; set; }

    public required string SourceSystem { get; set; }

    public required string ExternalName { get; set; }

    //
    // Version normalisée utilisée pour l'unicité
    // et les recherches insensibles à la casse.
    //

    public required string NormalizedExternalName { get; set; }

    //
    // Joueur moderne correspondant, s'il a été réconcilié.
    //
    // Aucun SitePlayerId ne sera inventé.
    //

    public int? PlayerId { get; set; }

    public MkddPlayer? Player { get; set; }

    public string? Country { get; set; }

    public int Confidence { get; set; }

    public string ResolutionStatus { get; set; } =
        "Unresolved";

    public ICollection<MkddHistoricalPerformance>
        HistoricalPerformances
    { get; set; } = [];
}