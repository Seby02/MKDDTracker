using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MKDDTracker.Data.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalYahooImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalPlayerAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalName = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedExternalName = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolutionStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPlayerAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPlayerAliases_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalPerformances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalKey = table.Column<string>(type: "TEXT", nullable: false),
                    HistoricalPlayerAliasId = table.Column<int>(type: "INTEGER", nullable: false),
                    CourseId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeType = table.Column<string>(type: "TEXT", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: true),
                    Time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DateSource = table.Column<string>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Improvement = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    ImportStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ImportReasonsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationWarningsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPerformances_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPerformances_HistoricalPlayerAliases_HistoricalPlayerAliasId",
                        column: x => x.HistoricalPlayerAliasId,
                        principalTable: "HistoricalPlayerAliases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalPerformanceSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HistoricalPerformanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SourceFile = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTime = table.Column<string>(type: "TEXT", nullable: true),
                    SourceLine = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerConfidence = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeTypeConfidence = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPerformanceSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPerformanceSources_HistoricalPerformances_HistoricalPerformanceId",
                        column: x => x.HistoricalPerformanceId,
                        principalTable: "HistoricalPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPerformances_CourseId",
                table: "HistoricalPerformances",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPerformances_ExternalKey",
                table: "HistoricalPerformances",
                column: "ExternalKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPerformances_HistoricalPlayerAliasId_CourseId_TimeType_Frequency_FirstSeenAt",
                table: "HistoricalPerformances",
                columns: new[] { "HistoricalPlayerAliasId", "CourseId", "TimeType", "Frequency", "FirstSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPerformanceSources_HistoricalPerformanceId_MessageId",
                table: "HistoricalPerformanceSources",
                columns: new[] { "HistoricalPerformanceId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlayerAliases_PlayerId",
                table: "HistoricalPlayerAliases",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlayerAliases_SourceSystem_NormalizedExternalName",
                table: "HistoricalPlayerAliases",
                columns: new[] { "SourceSystem", "NormalizedExternalName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalPerformanceSources");

            migrationBuilder.DropTable(
                name: "HistoricalPerformances");

            migrationBuilder.DropTable(
                name: "HistoricalPlayerAliases");
        }
    }
}
