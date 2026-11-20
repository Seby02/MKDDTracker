using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MKDDTracker.Scraper.Services.Database;
using MKDDTracker.Scraper.Services.Dashboard;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.HistoricalImport;
using MKDDTracker.Web;
using Xunit;
using MKDDTracker.Scraper.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace MKDDTracker.Tests;

public sealed class MkddWebServiceRegistrationTests
{
    [Fact]
    public void CriticalHistoricalServices_CanBeResolved()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddMkddWebServices(configuration, "Data Source=:memory:");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalModernImportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalDatabaseCheckService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalDashboardService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalRankingService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalRankingComparisonService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalRankingSelectionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalPlayerTimelineService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MkddHistoricalPlayerProfileService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDbContextFactory<MkddDbContext>>());
    }
}
