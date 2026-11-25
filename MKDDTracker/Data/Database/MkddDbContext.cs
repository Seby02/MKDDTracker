using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDbContext : DbContext
{
    public DbSet<MkddCourse> Courses => Set<MkddCourse>();

    public DbSet<MkddPlayer> Players => Set<MkddPlayer>();

    public DbSet<MkddHistoricalPlayerAlias> HistoricalPlayerAliases => Set<MkddHistoricalPlayerAlias>();

    public DbSet<MkddHistoricalPerformance> HistoricalPerformances => Set<MkddHistoricalPerformance>();

    public DbSet<MkddHistoricalPerformanceSource> HistoricalPerformanceSources => Set<MkddHistoricalPerformanceSource>();

    public MkddDbContext(DbContextOptions<MkddDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MkddCourse>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<MkddPlayer>()
            .HasIndex(x => x.SitePlayerId)
            .IsUnique();        

        modelBuilder.Entity<MkddHistoricalPlayerAlias>()
    .HasIndex(x => new
    {
        x.SourceSystem,
        x.NormalizedExternalName
    })
    .IsUnique();

        modelBuilder.Entity<MkddHistoricalPlayerAlias>()
            .HasOne(x => x.Player)
            .WithMany(x => x.HistoricalAliases)
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MkddHistoricalPerformance>()
            .HasIndex(x => x.ExternalKey)
            .IsUnique();

        modelBuilder.Entity<MkddHistoricalPerformance>()
            .HasIndex(x => new
            {
                x.HistoricalPlayerAliasId,
                x.CourseId,
                x.TimeType,
                x.Frequency,
                x.FirstSeenAt
            });

        modelBuilder.Entity<MkddHistoricalPerformance>()
            .HasOne(x => x.HistoricalPlayerAlias)
            .WithMany(x => x.HistoricalPerformances)
            .HasForeignKey(x => x.HistoricalPlayerAliasId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MkddHistoricalPerformance>()
            .HasOne(x => x.Course)
            .WithMany(x => x.HistoricalPerformances)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MkddHistoricalPerformanceSource>()
            .HasIndex(x => new
            {
                x.HistoricalPerformanceId,
                x.MessageId
            })
            .IsUnique();

        modelBuilder.Entity<MkddHistoricalPerformanceSource>()
            .HasOne(x => x.HistoricalPerformance)
            .WithMany(x => x.Sources)
            .HasForeignKey(x => x.HistoricalPerformanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}