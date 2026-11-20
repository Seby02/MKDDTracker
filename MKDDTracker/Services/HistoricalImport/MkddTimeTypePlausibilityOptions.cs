namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class MkddTimeTypePlausibilityOptions
{
    public int MinimumSampleSize { get; set; } = 5;

    public double SuspiciousRatio { get; set; } = 0.8;

    public int FlapSuspiciousThresholdSeconds { get; set; } = 60;

    public int ThreeLapSuspiciousThresholdSeconds { get; set; } = 45;

    public void Validate()
    {
        if (MinimumSampleSize < 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumSampleSize));
        if (!double.IsFinite(SuspiciousRatio) || SuspiciousRatio <= 0 || SuspiciousRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(SuspiciousRatio));
        if (FlapSuspiciousThresholdSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(FlapSuspiciousThresholdSeconds));
        if (ThreeLapSuspiciousThresholdSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(ThreeLapSuspiciousThresholdSeconds));
    }
}
