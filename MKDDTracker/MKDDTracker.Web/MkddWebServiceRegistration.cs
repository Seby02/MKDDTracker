using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Services.Database;
using MKDDTracker.Scraper.Services.Dashboard;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.HistoricalImport;
using MKDDTracker.Scraper.Services.SnapshotService;

namespace MKDDTracker.Web;

public static class MkddWebServiceRegistration
{
    public static IServiceCollection AddMkddWebServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string databasePath)
    {
        services.AddDbContextFactory<MkddDbContext>(
            options =>
                options.UseSqlite($"Data Source={databasePath}"),
            ServiceLifetime.Scoped);

        MkddTimeTypePlausibilityOptions options = new();
        IConfigurationSection section = configuration.GetSection("HistoricalImport:TimeTypePlausibility");
        if (int.TryParse(section["MinimumSampleSize"], out int minimumSampleSize))
            options.MinimumSampleSize = minimumSampleSize;
        if (double.TryParse(section["SuspiciousRatio"], System.Globalization.CultureInfo.InvariantCulture, out double suspiciousRatio))
            options.SuspiciousRatio = suspiciousRatio;
        if (int.TryParse(section["FlapSuspiciousThresholdSeconds"], out int flapSuspiciousThresholdSeconds))
            options.FlapSuspiciousThresholdSeconds = flapSuspiciousThresholdSeconds;
        if (int.TryParse(section["ThreeLapSuspiciousThresholdSeconds"], out int threeLapSuspiciousThresholdSeconds))
            options.ThreeLapSuspiciousThresholdSeconds = threeLapSuspiciousThresholdSeconds;
        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton<MkddDatabaseBackupService>();
        services.AddSingleton<MkddDatabaseSchemaInspector>();
        services.AddScoped<MkddDatabaseInitializer>();
        services.AddScoped<MkddHistoricalDatabaseCheckService>();
        services.AddScoped<MkddHistoricalDashboardService>();
        services.AddScoped<MkddHistoricalImportService>();
        services.AddScoped<MkddHistoricalRankingService>(
    serviceProvider =>
        new MkddHistoricalRankingService(
            serviceProvider.GetRequiredService<
                IDbContextFactory<MkddDbContext>>()));
        services.AddScoped<MkddHistoricalRankingComparisonService>();
        services.AddScoped<MkddHistoricalRankingSelectionService>();
        services.AddScoped<MkddHistoricalPlayerTimelineService>(
    serviceProvider =>
        new MkddHistoricalPlayerTimelineService(
            serviceProvider.GetRequiredService<
                IDbContextFactory<MkddDbContext>>(),
            serviceProvider.GetRequiredService<
                MkddHistoricalRankingService>()));
        services.AddScoped<MkddHistoricalPlayerProfileService>();
        services.AddScoped<MkddSnapshotComparisonService>();
        services.AddScoped<MkddSnapshotImportService>(
    serviceProvider =>
        new MkddSnapshotImportService(
            serviceProvider.GetRequiredService<
                IDbContextFactory<MkddDbContext>>()));
        services.AddScoped<MkddHistoricalModernImportService>();

        return services;
    }
}
