using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MKDDTracker.Scraper.Data.Database.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Courses",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Courses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Players",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SitePlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Country = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Players", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Snapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CourseId = table.Column<int>(type: "INTEGER", nullable: false),
                CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                RankingDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Snapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_Snapshots_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Performances",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                Rank = table.Column<int>(type: "INTEGER", nullable: false),
                Time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                Standard = table.Column<string>(type: "TEXT", nullable: true),
                RecordDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                VideoUrl = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Performances", x => x.Id);
                table.ForeignKey(
                    name: "FK_Performances_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Performances_Snapshots_SnapshotId",
                    column: x => x.SnapshotId,
                    principalTable: "Snapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Courses_Name",
            table: "Courses",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Performances_PlayerId",
            table: "Performances",
            column: "PlayerId");

        migrationBuilder.CreateIndex(
            name: "IX_Performances_SnapshotId_PlayerId",
            table: "Performances",
            columns: new[] { "SnapshotId", "PlayerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Players_SitePlayerId",
            table: "Players",
            column: "SitePlayerId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Snapshots_CourseId_RankingDate",
            table: "Snapshots",
            columns: new[] { "CourseId", "RankingDate" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Performances");
        migrationBuilder.DropTable(name: "Players");
        migrationBuilder.DropTable(name: "Snapshots");
        migrationBuilder.DropTable(name: "Courses");
    }
}
