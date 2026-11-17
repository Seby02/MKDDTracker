using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class Pre2003DateCorrectionService
{
    private const string YahooGroupsSource = "YahooGroups";
    private const string CorrectionDateSource = "SyntheticPre2003Correction";
    private const int ExpectedPerformanceCount = 44;
    private static readonly DateTimeOffset Cutoff = new(2003, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<long> CorrectableMessageIds = [24161, 24538, 23296];

    private readonly MkddDbContext _db;

    public Pre2003DateCorrectionService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<Pre2003DateCorrectionPlan> AnalyzeAsync(
        CancellationToken cancellationToken = default)
    {
        List<MkddHistoricalPerformance> performances = await LoadPerformancesAsync(
            tracking: false,
            cancellationToken);
        return CreatePlan(performances);
    }

    public async Task<Pre2003DateCorrectionPlan> CorrectAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            List<MkddHistoricalPerformance> performances = await LoadPerformancesAsync(
                tracking: true,
                cancellationToken);
            Pre2003DateCorrectionPlan plan = CreatePlan(performances);
            if (plan.PerformanceCount != ExpectedPerformanceCount || plan.Messages.Count != CorrectableMessageIds.Count)
            {
                throw new InvalidOperationException(
                    $"La correction attend exactement {ExpectedPerformanceCount} performances réparties sur " +
                    $"{CorrectableMessageIds.Count} messages ; {plan.PerformanceCount} performances et " +
                    $"{plan.Messages.Count} messages ont été trouvés.");
            }

            foreach (MkddHistoricalPerformance performance in performances)
            {
                long messageId = GetMessageId(performance);
                DateTimeOffset correctedDate = GetCorrectedDate(messageId);
                performance.FirstSeenAt = correctedDate;
                performance.DateSource = CorrectionDateSource;
                foreach (MkddHistoricalPerformanceSource source in performance.Sources)
                {
                    source.PublishedAt = correctedDate;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return plan;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static DateTimeOffset GetCorrectedDate(long messageId)
    {
        long dayOffset = ((messageId % 730) * 397 + 17) % 730;
        if (dayOffset < 0)
            dayOffset += 730;

        return new DateTimeOffset(2006, 1, 1, 12, 0, 0, TimeSpan.Zero)
            .AddDays(dayOffset);
    }

    private async Task<List<MkddHistoricalPerformance>> LoadPerformancesAsync(
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<MkddHistoricalPerformance> query = _db.HistoricalPerformances
            .Include(performance => performance.Sources);
        if (!tracking)
            query = query.AsNoTracking();

        List<MkddHistoricalPerformance> performances = await query
            .Where(performance => performance.SourceSystem == YahooGroupsSource)
            .ToListAsync(cancellationToken);

        return performances
            .Where(performance =>
                performance.FirstSeenAt.ToUniversalTime() < Cutoff &&
                performance.Sources.Any(source => CorrectableMessageIds.Contains(source.MessageId)))
            .ToList();
    }

    private static Pre2003DateCorrectionPlan CreatePlan(
        IReadOnlyCollection<MkddHistoricalPerformance> performances)
    {
        var messagePlans = new List<Pre2003DateCorrectionMessagePlan>();
        foreach (IGrouping<long, MkddHistoricalPerformance> group in performances
                     .GroupBy(GetMessageId)
                     .OrderBy(group => group.Key))
        {
            messagePlans.Add(new Pre2003DateCorrectionMessagePlan(
                group.Key,
                group.Select(performance => performance.FirstSeenAt)
                    .Distinct()
                    .OrderBy(date => date.ToUniversalTime())
                    .ToList(),
                GetCorrectedDate(group.Key),
                group.Count(),
                group.Sum(performance => performance.Sources.Count)));
        }

        return new Pre2003DateCorrectionPlan(messagePlans);
    }

    private static long GetMessageId(MkddHistoricalPerformance performance)
    {
        long[] messageIds = performance.Sources
            .Where(source => CorrectableMessageIds.Contains(source.MessageId))
            .Select(source => source.MessageId)
            .Distinct()
            .ToArray();

        if (messageIds.Length != 1)
        {
            throw new InvalidOperationException(
                $"La performance {performance.Id} doit être associée à un seul des messages ciblés.");
        }

        return messageIds[0];
    }
}

public sealed record Pre2003DateCorrectionMessagePlan(
    long MessageId,
    IReadOnlyList<DateTimeOffset> PreviousFirstSeenAt,
    DateTimeOffset CorrectedFirstSeenAt,
    int PerformanceCount,
    int SourceCount);

public sealed class Pre2003DateCorrectionPlan(
    IReadOnlyList<Pre2003DateCorrectionMessagePlan> messages)
{
    public IReadOnlyList<Pre2003DateCorrectionMessagePlan> Messages { get; } = messages;

    public int PerformanceCount => Messages.Sum(message => message.PerformanceCount);

    public int SourceCount => Messages.Sum(message => message.SourceCount);
}
