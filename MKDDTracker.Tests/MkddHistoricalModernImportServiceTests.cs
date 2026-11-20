using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Services.HistoricalImport;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalModernImportServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_RejectsMissingOrNullParsedPerformances()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);

        MkddHistoricalModernImportResult missing = await service.AnalyzeAsync(
            "Luigi Circuit",
            null);
        MkddPerformance?[] parsed = [null];
        MkddHistoricalModernImportResult nullEntry = await service.AnalyzeAsync(
            "Luigi Circuit",
            parsed);

        Assert.Contains("La liste des performances parsees est absente.", missing.Errors);
        Assert.Equal(1, nullEntry.RejectedCount);
        Assert.Contains("Une ou plusieurs performances parsees sont nulles.", nullEntry.Errors);
        Assert.Equal("Une performance parsee est nulle.", nullEntry.Outcomes.Single().Error);
    }

    [Fact]
    public async Task ImportAsync_CreatesFirstPrWithExplicitMetadataAndAlias()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance performance = CreatePerformance(
            sitePlayerId: 42,
            timeMilliseconds: 75_321,
            recordDate: new DateOnly(2024, 2, 3),
            timeType: "ThreeLap",
            frequency: 60);

        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [performance],
            observationAt: new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.Zero),
            fallbackTimeType: "Flap");

        Assert.True(result.ImportCompleted);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.FirstPrsDetected);
        Assert.Equal(1, result.EventsImported);

        await using var db = fixture.Factory.CreateDbContext();
        MkddHistoricalPerformance historical = await db.HistoricalPerformances
            .Include(item => item.HistoricalPlayerAlias)
            .SingleAsync();
        Assert.Equal("MKDDTracker", historical.SourceSystem);
        Assert.Equal("Approved", historical.ImportStatus);
        Assert.Equal("ThreeLap", historical.TimeType);
        Assert.Equal(60, historical.Frequency);
        Assert.Equal(TimeSpan.FromMilliseconds(75_321), historical.Time);
        Assert.Equal("RecordDate", historical.DateSource);
        Assert.Equal(new DateTimeOffset(2024, 2, 3, 12, 0, 0, TimeSpan.Zero), historical.FirstSeenAt);
        Assert.Null(historical.PreviousTime);
        Assert.Null(historical.Improvement);
        Assert.Equal(1, historical.SequenceNumber);
        Assert.Equal(1, historical.OccurrenceCount);
        Assert.Equal(64, historical.ExternalKey.Length);
        Assert.Equal("MKDDTracker", historical.HistoricalPlayerAlias.SourceSystem);
        Assert.Equal("Racer 42", historical.HistoricalPlayerAlias.ExternalName);
        Assert.Equal(100, historical.HistoricalPlayerAlias.Confidence);
        Assert.Equal("Matched", historical.HistoricalPlayerAlias.ResolutionStatus);
        Assert.Equal(42, (await db.Players.SingleAsync()).SitePlayerId);
    }

    [Fact]
    public async Task ImportAsync_PreservesExplicitFlapTimeType()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance performance = CreatePerformance(
            43,
            75_321,
            new DateOnly(2024, 2, 3),
            "Flap",
            null);

        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [performance],
            fallbackTimeType: "ThreeLap");

        Assert.True(result.ImportCompleted);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal("Flap", (await db.HistoricalPerformances.SingleAsync()).TimeType);
    }

    [Fact]
    public async Task ImportAsync_RejectsMissingTimeTypeWithoutWritingAnything()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance performance = CreatePerformance(
            42,
            75_000,
            new DateOnly(2024, 2, 3),
            timeType: null,
            frequency: null);

        MkddHistoricalModernImportResult preview = await service.AnalyzeAsync(
            "Luigi Circuit",
            [performance]);
        MkddHistoricalModernImportResult imported = await service.ImportAsync(
            "Luigi Circuit",
            [performance]);

        Assert.Equal("MissingTimeType", preview.Outcomes.Single().Error);
        Assert.Contains("MissingTimeType", preview.Errors);
        Assert.False(imported.ImportCompleted);
        Assert.Equal("MissingTimeType", imported.Outcomes.Single().Error);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Empty(await db.Courses.ToListAsync());
        Assert.Empty(await db.Players.ToListAsync());
        Assert.Empty(await db.HistoricalPlayerAliases.ToListAsync());
        Assert.Empty(await db.HistoricalPerformances.ToListAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsCoherentThreeLapSeriesAsConsistent()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance[] performances = CreateSeries(
            "ThreeLap",
            90_000,
            95_000,
            100_000,
            105_000,
            110_000);

        MkddHistoricalModernImportResult result = await service.AnalyzeAsync(
            "Luigi Circuit",
            performances);

        Assert.Equal(TimeTypeValidationStatus.Consistent, result.TimeTypeValidation);
        Assert.Null(result.TimeTypeValidationReason);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportAsync_RejectsManifestlyThreeLapTimesDeclaredAsFlap()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance[] performances = CreateSeries(
            "Flap",
            90_000,
            95_000,
            100_000,
            105_000,
            110_000);

        MkddHistoricalModernImportResult preview = await service.AnalyzeAsync(
            "Luigi Circuit",
            performances);
        MkddHistoricalModernImportResult imported = await service.ImportAsync(
            "Luigi Circuit",
            performances);

        Assert.Equal(TimeTypeValidationStatus.Suspicious, preview.TimeTypeValidation);
        string validationReason = preview.TimeTypeValidationReason ?? string.Empty;
        Assert.StartsWith("SuspiciousTimeType", validationReason);
        Assert.Contains(validationReason, preview.Errors);
        Assert.False(imported.ImportCompleted);
        Assert.Equal(TimeTypeValidationStatus.Suspicious, imported.TimeTypeValidation);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Empty(await db.Courses.ToListAsync());
        Assert.Empty(await db.Players.ToListAsync());
        Assert.Empty(await db.HistoricalPlayerAliases.ToListAsync());
        Assert.Empty(await db.HistoricalPerformances.ToListAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotRejectTimesNearPlausibilityThresholds()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance[] flapBoundary = CreateSeries(
            "Flap",
            59_000,
            60_000,
            61_000,
            62_000,
            59_000);
        MkddPerformance[] threeLapBoundary = CreateSeries(
            "ThreeLap",
            44_999,
            45_000,
            45_001,
            90_000,
            120_000);

        MkddHistoricalModernImportResult flap = await service.AnalyzeAsync(
            "Luigi Circuit",
            flapBoundary);
        MkddHistoricalModernImportResult threeLap = await service.AnalyzeAsync(
            "Peach Beach",
            threeLapBoundary);

        Assert.Equal(TimeTypeValidationStatus.Consistent, flap.TimeTypeValidation);
        Assert.Equal(TimeTypeValidationStatus.Consistent, threeLap.TimeTypeValidation);
        Assert.DoesNotContain("SuspiciousTimeType", string.Join(" ", flap.Errors));
        Assert.DoesNotContain("SuspiciousTimeType", string.Join(" ", threeLap.Errors));
    }

    [Fact]
    public async Task AnalyzeAndImportAsync_DetectStrictImprovementAndPreservePreviousTimeAndSequence()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedHistoryAsync(
            fixture,
            sourceSystem: "MKDDTracker",
            timeMilliseconds: 80_000,
            timeType: "ThreeLap",
            frequency: 60,
            sequenceNumber: 1);
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance improved = CreatePerformance(
            42,
            79_000,
            new DateOnly(2024, 2, 4),
            "ThreeLap",
            60);

        MkddHistoricalModernImportResult preview = await service.AnalyzeAsync(
            "Luigi Circuit",
            [improved]);
        Assert.Equal(1, preview.ImprovementsDetected);
        Assert.Equal(TimeSpan.FromSeconds(80), preview.Outcomes.Single().PreviousTime);
        Assert.Equal(TimeSpan.FromSeconds(1), preview.Outcomes.Single().Improvement);
        Assert.Equal(2, preview.Outcomes.Single().SequenceNumber);

        MkddHistoricalModernImportResult imported = await service.ImportAsync(
            "Luigi Circuit",
            [improved]);
        Assert.Equal(1, imported.EventsImported);

        await using var db = fixture.Factory.CreateDbContext();
        MkddHistoricalPerformance performance = await db.HistoricalPerformances
            .OrderBy(item => item.SequenceNumber)
            .LastAsync();
        Assert.Equal(TimeSpan.FromSeconds(80), performance.PreviousTime);
        Assert.Equal(TimeSpan.FromSeconds(1), performance.Improvement);
        Assert.Equal(2, performance.SequenceNumber);
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoresIdenticalAndSlowerTimes()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedHistoryAsync(
            fixture,
            sourceSystem: "MKDDTracker",
            timeMilliseconds: 80_000,
            timeType: "ThreeLap",
            frequency: 60,
            sequenceNumber: 3);
        var service = new MkddHistoricalModernImportService(fixture.Factory);

        MkddHistoricalModernImportResult identical = await service.AnalyzeAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 80_000, new DateOnly(2024, 3, 2), "ThreeLap", 60)]);
        MkddHistoricalModernImportResult slower = await service.AnalyzeAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 81_000, new DateOnly(2024, 3, 3), "ThreeLap", 60)]);

        Assert.Equal(1, identical.IdenticalTimesIgnored);
        Assert.Equal(1, slower.SlowerTimesIgnored);
        Assert.Equal(ModernPrDetectionStatus.Unchanged, identical.Outcomes.Single().Status);
        Assert.Equal(ModernPrDetectionStatus.Slower, slower.Outcomes.Single().Status);
        Assert.Equal(TimeSpan.FromSeconds(80), identical.Outcomes.Single().PreviousTime);
        Assert.Equal(TimeSpan.FromSeconds(80), slower.Outcomes.Single().PreviousTime);
        Assert.Equal(0, identical.EventsToCreate);
        Assert.Equal(0, slower.EventsToCreate);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesIndependentSeriesForCourseTypeAndFrequency()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedHistoryAsync(
            fixture,
            sourceSystem: "MKDDTracker",
            timeMilliseconds: 80_000,
            timeType: "ThreeLap",
            frequency: 60,
            sequenceNumber: 1);
        var service = new MkddHistoricalModernImportService(fixture.Factory);

        MkddHistoricalModernImportResult otherCourse = await service.AnalyzeAsync(
            "Peach Beach",
            [CreatePerformance(42, 90_000, new DateOnly(2024, 3, 2), "ThreeLap", 60)]);
        MkddHistoricalModernImportResult otherType = await service.AnalyzeAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 90_000, new DateOnly(2024, 3, 2), "Flap", 60)]);
        MkddHistoricalModernImportResult otherFrequency = await service.AnalyzeAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 90_000, new DateOnly(2024, 3, 2), "ThreeLap", 50)]);

        Assert.Equal(1, otherCourse.FirstPrsDetected);
        Assert.Equal(1, otherType.FirstPrsDetected);
        Assert.Equal(1, otherFrequency.FirstPrsDetected);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await db.HistoricalPerformances.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_ReusesExistingModernAlias()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedPlayerAndAliasAsync(fixture, "MKDDTracker", "Racer 42", 42);
        var service = new MkddHistoricalModernImportService(fixture.Factory);

        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 75_000, new DateOnly(2024, 4, 1), "ThreeLap", null)]);

        Assert.Equal(1, result.ExistingAliases);
        Assert.Equal(0, result.AliasesToCreate);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await db.HistoricalPlayerAliases.CountAsync());
        MkddHistoricalPerformance performance = await db.HistoricalPerformances.SingleAsync();
        Assert.Equal(1, performance.HistoricalPlayerAliasId);
    }

    [Fact]
    public async Task ImportAsync_IsIdempotentForRepeatedInput()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddPerformance performance = CreatePerformance(
            42,
            75_000,
            new DateOnly(2024, 4, 1),
            "ThreeLap",
            null);

        MkddHistoricalModernImportResult first = await service.ImportAsync(
            "Luigi Circuit",
            [performance]);
        MkddHistoricalModernImportResult second = await service.ImportAsync(
            "Luigi Circuit",
            [performance]);

        Assert.Equal(1, first.EventsToCreate);
        Assert.Equal(1, first.EventsImported);
        Assert.Equal(0, second.EventsToCreate);
        Assert.Equal(1, second.ExistingEvents);
        Assert.Equal(1, second.ExistingAliases);
        Assert.Equal(0, second.EventsImported);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await db.HistoricalPerformances.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_DoesNotInventFrequencyOrRecordDate()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new MkddHistoricalModernImportService(fixture.Factory);
        DateTimeOffset observedAt = new(2024, 5, 6, 9, 0, 0, TimeSpan.FromHours(-4));
        MkddPerformance performance = CreatePerformance(
            42,
            75_000,
            default,
            timeType: "ThreeLap",
            frequency: null);

        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [performance],
            observedAt);

        Assert.Equal(1, result.EventsImported);
        await using var db = fixture.Factory.CreateDbContext();
        MkddHistoricalPerformance historical = await db.HistoricalPerformances.SingleAsync();
        Assert.Equal("ThreeLap", historical.TimeType);
        Assert.Null(historical.Frequency);
        Assert.Equal("ObservationDate", historical.DateSource);
        Assert.Equal(observedAt.ToUniversalTime(), historical.FirstSeenAt);
    }

    [Fact]
    public async Task ImportAsync_PreservesPreexistingYahooHistory()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedHistoryAsync(
            fixture,
            sourceSystem: "YahooGroups",
            timeMilliseconds: 80_000,
            timeType: "ThreeLap",
            frequency: 60,
            sequenceNumber: 1,
            externalKey: "yahoo-original");
        var service = new MkddHistoricalModernImportService(fixture.Factory);

        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 79_000, new DateOnly(2024, 5, 7), "ThreeLap", 60)]);

        Assert.Equal(1, result.ImprovementsDetected);
        await using var db = fixture.Factory.CreateDbContext();
        List<MkddHistoricalPerformance> performances = await db.HistoricalPerformances.ToListAsync();
        Assert.Equal(2, performances.Count);
        MkddHistoricalPerformance yahooPerformance = performances.Single(item => item.ExternalKey == "yahoo-original");
        MkddHistoricalPerformance modernPerformance = performances.Single(item => item.SourceSystem == "MKDDTracker");
        Assert.Equal("YahooGroups", yahooPerformance.SourceSystem);
        Assert.Equal(TimeSpan.FromSeconds(80), modernPerformance.PreviousTime);
        Assert.Equal(TimeSpan.FromSeconds(1), modernPerformance.Improvement);
    }

    [Fact]
    public async Task ImportAsync_RollsBackCoursePlayerAliasAndEventOnFailure()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailModernHistoryInsert BEFORE INSERT ON HistoricalPerformances " +
                "BEGIN SELECT RAISE(ABORT, 'forced failure'); END;");
        }

        var service = new MkddHistoricalModernImportService(fixture.Factory);
        MkddHistoricalModernImportResult result = await service.ImportAsync(
            "Luigi Circuit",
            [CreatePerformance(42, 75_000, new DateOnly(2024, 6, 1), "ThreeLap", 60)]);

        Assert.False(result.ImportCompleted);
        Assert.NotEmpty(result.Errors);
        await using var verify = fixture.Factory.CreateDbContext();
        Assert.Equal(0, await verify.Courses.CountAsync());
        Assert.Equal(0, await verify.Players.CountAsync());
        Assert.Equal(0, await verify.HistoricalPlayerAliases.CountAsync());
        Assert.Equal(0, await verify.HistoricalPerformances.CountAsync());
    }

    private static MkddPerformance CreatePerformance(
        int sitePlayerId,
        int timeMilliseconds,
        DateOnly recordDate,
        string? timeType,
        int? frequency) =>
        new(
            1,
            sitePlayerId,
            $"Racer {sitePlayerId}",
            "FR",
            TimeSpan.FromMilliseconds(timeMilliseconds),
            "God",
            recordDate,
            null,
            timeType,
            frequency);

    private static MkddPerformance[] CreateSeries(string timeType, params int[] timeMilliseconds) =>
        timeMilliseconds
            .Select((time, index) => CreatePerformance(
                100 + index,
                time,
                new DateOnly(2024, 2, 3).AddDays(index),
                timeType,
                frequency: null))
            .ToArray();

    private static async Task SeedHistoryAsync(
        TestDatabase fixture,
        string sourceSystem,
        int timeMilliseconds,
        string timeType,
        int? frequency,
        int sequenceNumber,
        string? externalKey = null)
    {
        await using var db = fixture.Factory.CreateDbContext();
        var player = new MkddPlayer
        {
            SitePlayerId = 42,
            Name = "Racer 42",
            Country = "FR"
        };
        var course = new MkddCourse { Name = "Luigi Circuit" };
        var alias = new MkddHistoricalPlayerAlias
        {
            SourceSystem = sourceSystem,
            ExternalName = "Racer 42",
            NormalizedExternalName = "racer 42",
            Player = player,
            Confidence = 100,
            ResolutionStatus = "Matched"
        };
        db.HistoricalPerformances.Add(new MkddHistoricalPerformance
        {
            ExternalKey = externalKey ?? $"seed-{sourceSystem}",
            HistoricalPlayerAlias = alias,
            Course = course,
            TimeType = timeType,
            Frequency = frequency,
            Time = TimeSpan.FromMilliseconds(timeMilliseconds),
            FirstSeenAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            DateSource = "RecordDate",
            SequenceNumber = sequenceNumber,
            OccurrenceCount = 1,
            SourceSystem = sourceSystem,
            ImportStatus = "Approved"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPlayerAndAliasAsync(
        TestDatabase fixture,
        string sourceSystem,
        string externalName,
        int sitePlayerId)
    {
        await using var db = fixture.Factory.CreateDbContext();
        var player = new MkddPlayer
        {
            SitePlayerId = sitePlayerId,
            Name = externalName,
            Country = "FR"
        };
        db.Players.Add(player);
        db.HistoricalPlayerAliases.Add(new MkddHistoricalPlayerAlias
        {
            SourceSystem = sourceSystem,
            ExternalName = externalName,
            NormalizedExternalName = "racer 42",
            Player = player,
            Confidence = 100,
            ResolutionStatus = "Matched"
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _directory;

        private TestDatabase(string directory, TestDbContextFactory factory)
        {
            _directory = directory;
            Factory = factory;
        }

        public TestDbContextFactory Factory { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            string directory = DatabaseTestSupport.CreateTemporaryDirectory();
            var options = new DbContextOptionsBuilder<MkddDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "modern-history.db")};Pooling=False")
                .Options;
            var database = new TestDatabase(directory, new TestDbContextFactory(options));
            await using var db = database.Factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            return database;
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<MkddDbContext> options) : IDbContextFactory<MkddDbContext>
    {
        public MkddDbContext CreateDbContext() => new(options);

        public Task<MkddDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
