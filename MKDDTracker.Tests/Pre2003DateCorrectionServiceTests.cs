using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.HistoricalImport;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class Pre2003DateCorrectionServiceTests
{
    private static readonly long[] MessageIds = [24161, 24538, 23296];
    private static readonly DateTimeOffset Cutoff = new(2003, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CorrectionTargetsOnlyTheExpectedPerformancesAndIsDeterministic()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "pre2003-correction.db");
        try
        {
            await using (var setup = DatabaseTestSupport.CreateContext(databasePath))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.HistoricalPerformances.AddRange(CreateFixture());
                await setup.SaveChangesAsync();
            }

            Pre2003DateCorrectionPlan plan;
            Dictionary<long, PerformanceState> before;
            Dictionary<long, SourceState> sourcesBefore;
            await using (var db = DatabaseTestSupport.CreateContext(databasePath))
            {
                before = db.HistoricalPerformances
                    .Select(PerformanceState.From)
                    .ToDictionary(state => state.Id);
                sourcesBefore = db.HistoricalPerformanceSources
                    .Select(SourceState.From)
                    .ToDictionary(state => state.Id);

                var service = new Pre2003DateCorrectionService(db);
                plan = await service.AnalyzeAsync();

                Assert.Equal(44, plan.PerformanceCount);
                Assert.Equal(3, plan.Messages.Count);
                Assert.Equal(44, plan.SourceCount);
                Assert.Equal(MessageIds.Order(), plan.Messages.Select(message => message.MessageId).Order());

                foreach (Pre2003DateCorrectionMessagePlan message in plan.Messages)
                {
                    Assert.InRange(message.CorrectedFirstSeenAt, Cutoff.AddYears(3), new DateTimeOffset(2007, 12, 31, 23, 59, 59, TimeSpan.Zero));
                    Assert.Equal(TimeSpan.Zero, message.CorrectedFirstSeenAt.Offset);
                    Assert.Equal(message.CorrectedFirstSeenAt, Pre2003DateCorrectionService.GetCorrectedDate(message.MessageId));
                    Assert.All(
                        Enumerable.Range(0, 5),
                        _ => Assert.Equal(
                            message.CorrectedFirstSeenAt,
                            Pre2003DateCorrectionService.GetCorrectedDate(message.MessageId)));
                    Assert.Equal(1, message.PreviousFirstSeenAt.Count);
                    Assert.True(message.PreviousFirstSeenAt[0] < Cutoff);
                }

                Assert.Equal(15, plan.Messages.Single(message => message.MessageId == 24161).PerformanceCount);
                Assert.Equal(15, plan.Messages.Single(message => message.MessageId == 24538).PerformanceCount);
                Assert.Equal(14, plan.Messages.Single(message => message.MessageId == 23296).PerformanceCount);

                await service.CorrectAsync();
            }

            await using (var verify = DatabaseTestSupport.CreateContext(databasePath))
            {
                var after = await verify.HistoricalPerformances
                    .Include(performance => performance.Sources)
                    .ToListAsync();
                Assert.Equal(before.Count, after.Count);

                foreach (MkddHistoricalPerformance performance in after)
                {
                    PerformanceState original = before[performance.Id];
                    MkddHistoricalPerformanceSource? matchingSource = performance.Sources
                        .SingleOrDefault(source => MessageIds.Contains(source.MessageId));
                    bool shouldBeCorrected =
                        matchingSource is not null &&
                        string.Equals(performance.SourceSystem, "YahooGroups", StringComparison.Ordinal) &&
                        original.FirstSeenAt.ToUniversalTime() < Cutoff;

                    if (shouldBeCorrected)
                    {
                        DateTimeOffset expectedDate = Pre2003DateCorrectionService.GetCorrectedDate(matchingSource!.MessageId);
                        Assert.Equal(expectedDate, performance.FirstSeenAt);
                        Assert.Equal("SyntheticPre2003Correction", performance.DateSource);
                        Assert.Equal(original with { FirstSeenAt = expectedDate, DateSource = "SyntheticPre2003Correction" }, PerformanceState.From(performance));
                        Assert.All(performance.Sources, source => Assert.Equal(expectedDate, source.PublishedAt));
                    }
                    else
                    {
                        Assert.Equal(original, PerformanceState.From(performance));
                        foreach (MkddHistoricalPerformanceSource source in performance.Sources)
                        {
                            Assert.Equal(sourcesBefore[source.Id], SourceState.From(source));
                        }
                    }
                }

                foreach (MkddHistoricalPerformanceSource source in after.SelectMany(performance => performance.Sources))
                {
                    if (MessageIds.Contains(source.MessageId) &&
                        after.Single(performance => performance.Id == source.HistoricalPerformanceId)
                            .DateSource == "SyntheticPre2003Correction")
                    {
                        Assert.Equal(
                            Pre2003DateCorrectionService.GetCorrectedDate(source.MessageId),
                            source.PublishedAt);
                    }
                    else if (sourcesBefore.TryGetValue(source.Id, out SourceState? original))
                    {
                        Assert.Equal(original, SourceState.From(source));
                    }
                }

                Pre2003DateCorrectionPlan secondDryRun = await new Pre2003DateCorrectionService(verify).AnalyzeAsync();
                Assert.Equal(0, secondDryRun.PerformanceCount);
                Assert.Equal(0, secondDryRun.Messages.Count);
                Assert.Equal(0, secondDryRun.SourceCount);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static List<MkddHistoricalPerformance> CreateFixture()
    {
        var alias = new MkddHistoricalPlayerAlias
        {
            SourceSystem = "YahooGroups",
            ExternalName = "Test Player",
            NormalizedExternalName = "test player"
        };
        var course = new MkddCourse { Name = "Luigi Circuit" };
        var performances = new List<MkddHistoricalPerformance>();
        int counter = 0;
        int[] counts = [15, 15, 14];
        for (int messageIndex = 0; messageIndex < MessageIds.Length; messageIndex++)
        {
            DateTimeOffset oldDate = new(2000 + messageIndex, 4, 10, 9, 30, 0, TimeSpan.Zero);
            for (int item = 0; item < counts[messageIndex]; item++)
            {
                performances.Add(CreatePerformance(
                    alias,
                    course,
                    counter++,
                    "YahooGroups",
                    oldDate,
                    MessageIds[messageIndex],
                    "Imported"));
            }
        }

        performances.Add(CreatePerformance(alias, course, counter++, "YahooGroups", Cutoff, 24161, "AtCutoff"));
        performances.Add(CreatePerformance(alias, course, counter++, "YahooGroups", Cutoff.AddDays(-1), 99999, "OtherMessage"));
        performances.Add(CreatePerformance(alias, course, counter, "OtherSource", Cutoff.AddDays(-1), 24161, "OtherSystem"));
        return performances;
    }

    private static MkddHistoricalPerformance CreatePerformance(
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        int index,
        string sourceSystem,
        DateTimeOffset firstSeenAt,
        long messageId,
        string status)
    {
        var performance = new MkddHistoricalPerformance
        {
            ExternalKey = $"fixture-{index}",
            HistoricalPlayerAlias = alias,
            Course = course,
            TimeType = "ThreeLap",
            Frequency = 60,
            Time = TimeSpan.FromMilliseconds(70_000 + index),
            FirstSeenAt = firstSeenAt,
            DateSource = "YahooPublishedAt",
            SourceSystem = sourceSystem,
            ImportStatus = status,
            SequenceNumber = index + 1,
            OccurrenceCount = 1
        };
        performance.Sources.Add(new MkddHistoricalPerformanceSource
        {
            MessageId = messageId,
            PublishedAt = firstSeenAt,
            SourceFile = $"file-{index}.txt",
            OriginalTime = "1:10.000",
            SourceLine = $"line-{index}",
            ImportStatus = status
        });
        return performance;
    }

    private sealed record PerformanceState(
        long Id,
        string ExternalKey,
        int HistoricalPlayerAliasId,
        int CourseId,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        string DateSource,
        string SourceSystem,
        string ImportStatus,
        int SequenceNumber,
        int OccurrenceCount)
    {
        public static PerformanceState From(MkddHistoricalPerformance performance) => new(
            performance.Id,
            performance.ExternalKey,
            performance.HistoricalPlayerAliasId,
            performance.CourseId,
            performance.TimeType,
            performance.Frequency,
            performance.Time,
            performance.FirstSeenAt,
            performance.DateSource,
            performance.SourceSystem,
            performance.ImportStatus,
            performance.SequenceNumber,
            performance.OccurrenceCount);
    }

    private sealed record SourceState(
        long Id,
        long HistoricalPerformanceId,
        long MessageId,
        DateTimeOffset? PublishedAt,
        string? SourceFile,
        string? OriginalTime,
        string? SourceLine,
        string ImportStatus)
    {
        public static SourceState From(MkddHistoricalPerformanceSource source) => new(
            source.Id,
            source.HistoricalPerformanceId,
            source.MessageId,
            source.PublishedAt,
            source.SourceFile,
            source.OriginalTime,
            source.SourceLine,
            source.ImportStatus);
    }
}
