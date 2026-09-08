using System;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.Sqlite
{
    [DbContext(typeof(BistQuantDbContext))]
    [Migration("20260908160000_BistBulletinFinalHardening")]
    public partial class BistBulletinFinalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsRevision",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SupersedesImportId",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "MarketDataImports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "MarketDataImports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastHttpStatus",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DownloadStatus",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UIX_MarketDataImports_Provider_SessionDate_Sha256",
                table: "MarketDataImports",
                columns: new[] { "Provider", "SessionDate", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImports_SupersedesImportId",
                table: "MarketDataImports",
                column: "SupersedesImportId");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastSeenInBulletinDate",
                table: "Symbols",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "Signals",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropIndex(
                name: "IX_Signals_Symbol_Strategy_Timeframe_SessionDate",
                table: "Signals");

            migrationBuilder.CreateIndex(
                name: "UIX_Signals_Symbol_SessionDate_Timeframe_NoStrategy",
                table: "Signals",
                columns: new[] { "SymbolId", "SourceSessionDate", "Timeframe" },
                unique: true,
                filter: "StrategyId IS NULL");

            migrationBuilder.CreateIndex(
                name: "UIX_Signals_Symbol_Strategy_SessionDate_Timeframe",
                table: "Signals",
                columns: new[] { "SymbolId", "StrategyId", "SourceSessionDate", "Timeframe" },
                unique: true,
                filter: "StrategyId IS NOT NULL");

            migrationBuilder.AddColumn<long>(
                name: "SourceSignalId",
                table: "PaperOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SignalSessionDate",
                table: "PaperOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TargetExecutionSessionDate",
                table: "PaperOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExecutedSessionDate",
                table: "PaperOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PaperOrders",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperOrders_Status_TargetExecutionSessionDate",
                table: "PaperOrders",
                columns: new[] { "Status", "TargetExecutionSessionDate" });

            migrationBuilder.CreateTable(
                name: "BulletinFetchAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ElapsedMs = table.Column<long>(type: "INTEGER", nullable: true),
                    ContentLength = table.Column<long>(type: "INTEGER", nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinFetchAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulletinFetchAttempts_SessionDate_AttemptedAtUtc",
                table: "BulletinFetchAttempts",
                columns: new[] { "SessionDate", "AttemptedAt" });

            migrationBuilder.CreateTable(
                name: "IndicatorContinuityWarnings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SymbolId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CorporateActionRaw = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PreviousCloseReported = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PreviousRawCloseInDb = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    WarningMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorContinuityWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicatorContinuityWarnings_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorContinuityWarnings_SymbolId_SessionDate",
                table: "IndicatorContinuityWarnings",
                columns: new[] { "SymbolId", "SessionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IndicatorContinuityWarnings");
            migrationBuilder.DropTable(name: "BulletinFetchAttempts");
            migrationBuilder.DropColumn(name: "SourceSignalId", table: "PaperOrders");
            migrationBuilder.DropColumn(name: "SignalSessionDate", table: "PaperOrders");
            migrationBuilder.DropColumn(name: "TargetExecutionSessionDate", table: "PaperOrders");
            migrationBuilder.DropColumn(name: "ExecutedSessionDate", table: "PaperOrders");
            migrationBuilder.DropColumn(name: "CancellationReason", table: "PaperOrders");
            migrationBuilder.DropIndex(name: "IX_PaperOrders_Status_TargetExecutionSessionDate", table: "PaperOrders");
            migrationBuilder.DropIndex(name: "UIX_Signals_Symbol_SessionDate_Timeframe_NoStrategy", table: "Signals");
            migrationBuilder.DropIndex(name: "UIX_Signals_Symbol_Strategy_SessionDate_Timeframe", table: "Signals");
            migrationBuilder.DropColumn(name: "IsSuperseded", table: "Signals");
            migrationBuilder.DropColumn(name: "LastSeenInBulletinDate", table: "Symbols");
            migrationBuilder.DropIndex(name: "IX_MarketDataImports_SupersedesImportId", table: "MarketDataImports");
            migrationBuilder.DropIndex(name: "UIX_MarketDataImports_Provider_SessionDate_Sha256", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "RevisionNumber", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "IsRevision", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "SupersedesImportId", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "IsCurrent", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "LastAttemptAt", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "NextAttemptAt", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "AttemptCount", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "LastHttpStatus", table: "MarketDataImports");
            migrationBuilder.DropColumn(name: "DownloadStatus", table: "MarketDataImports");
        }
    }
}
