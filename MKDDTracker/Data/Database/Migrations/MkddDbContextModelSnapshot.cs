using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace MKDDTracker.Scraper.Data.Database.Migrations;

[DbContext(typeof(MkddDbContext))]
partial class MkddDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) =>
        BuildModelCore(modelBuilder);

    internal static void BuildSnapshotModel(ModelBuilder modelBuilder) =>
        BuildModelCore(modelBuilder);

    private static void BuildModelCore(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation(
            "ProductVersion",
            MkddDatabaseMigrationMetadata.ProductVersion);

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddCourse",
            b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("INTEGER");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("TEXT");

                b.HasKey("Id");
                b.HasIndex("Name").IsUnique();
                b.ToTable("Courses");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddPlayer",
            b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("INTEGER");

                b.Property<string>("Country").HasColumnType("TEXT");
                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("TEXT");
                b.Property<int>("SitePlayerId").HasColumnType("INTEGER");

                b.HasKey("Id");
                b.HasIndex("SitePlayerId").IsUnique();
                b.ToTable("Players");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddSnapshot",
            b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("INTEGER");

                b.Property<DateTime>("CapturedAt").HasColumnType("TEXT");
                b.Property<int>("CourseId").HasColumnType("INTEGER");
                b.Property<DateOnly>("RankingDate").HasColumnType("TEXT");

                b.HasKey("Id");
                b.HasIndex("CourseId", "RankingDate").IsUnique();
                b.ToTable("Snapshots");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddPerformanceEntity",
            b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("INTEGER");

                b.Property<int>("PlayerId").HasColumnType("INTEGER");
                b.Property<int>("Rank").HasColumnType("INTEGER");
                b.Property<DateOnly>("RecordDate").HasColumnType("TEXT");
                b.Property<int>("SnapshotId").HasColumnType("INTEGER");
                b.Property<string>("Standard").HasColumnType("TEXT");
                b.Property<TimeSpan>("Time").HasColumnType("TEXT");
                b.Property<string>("VideoUrl").HasColumnType("TEXT");

                b.HasKey("Id");
                b.HasIndex("PlayerId");
                b.HasIndex("SnapshotId", "PlayerId").IsUnique();
                b.ToTable("Performances");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddSnapshot",
            b =>
            {
                b.HasOne(
                        "MKDDTracker.Scraper.Data.Entities.MkddCourse",
                        "Course")
                    .WithMany("Snapshots")
                    .HasForeignKey("CourseId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Course");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddPerformanceEntity",
            b =>
            {
                b.HasOne(
                        "MKDDTracker.Scraper.Data.Entities.MkddPlayer",
                        "Player")
                    .WithMany("Performances")
                    .HasForeignKey("PlayerId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne(
                        "MKDDTracker.Scraper.Data.Entities.MkddSnapshot",
                        "Snapshot")
                    .WithMany("Performances")
                    .HasForeignKey("SnapshotId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Player");
                b.Navigation("Snapshot");
            });

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddCourse",
            b => b.Navigation("Snapshots"));

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddPlayer",
            b => b.Navigation("Performances"));

        modelBuilder.Entity(
            "MKDDTracker.Scraper.Data.Entities.MkddSnapshot",
            b => b.Navigation("Performances"));
#pragma warning restore 612, 618
    }
}
