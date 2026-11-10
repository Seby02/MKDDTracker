using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MKDDTracker.Scraper.Data.Database.Migrations;

[DbContext(typeof(MkddDbContext))]
[Migration(MkddDatabaseMigrationMetadata.InitialCreateMigrationId)]
partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        MkddDbContextModelSnapshot.BuildSnapshotModel(modelBuilder);
#pragma warning restore 612, 618
    }
}
