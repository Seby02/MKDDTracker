using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDbContext : DbContext
{
    public DbSet<MkddCourse> Courses => Set<MkddCourse>();

    public DbSet<MkddPlayer> Players => Set<MkddPlayer>();

    public DbSet<MkddSnapshot> Snapshots => Set<MkddSnapshot>();

    public DbSet<MkddPerformanceEntity> Performances => Set<MkddPerformanceEntity>();

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=mkddtracker.db");
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

        modelBuilder.Entity<MkddSnapshot>()
    .HasIndex(x => new
    {
        x.CourseId,
        x.RankingDate
    })
    .IsUnique();

        modelBuilder.Entity<MkddPerformanceEntity>()
            .HasIndex(x => new
            {
                x.SnapshotId,
                x.PlayerId
            })
            .IsUnique();
    }
}