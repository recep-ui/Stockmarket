using System;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.SqlServer
{
    [DbContext(typeof(BistQuantDbContext))]
    [Migration("20260908150001_BistDailyBulletinIntegration")]
    public partial class BistDailyBulletinIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SourceSessionDate",
                table: "Signals",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketDataImports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowsRead = table.Column<int>(type: "int", nullable: false),
                    RowsAccepted = table.Column<int>(type: "int", nullable: false),
                    RowsRejected = table.Column<int>(type: "int", nullable: false),
                    PriceBarsInserted = table.Column<int>(type: "int", nullable: false),
                    PriceBarsUpdated = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyInstrumentMarketStats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousLastPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ClosingSessionPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ChangePercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Vwap = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TotalTradedValue = table.Column<decimal>(type: "decimal(24,2)", precision: 24, scale: 2, nullable: true),
                    TotalTradedVolume = table.Column<decimal>(type: "decimal(24,2)", precision: 24, scale: 2, nullable: true),
                    TotalNumberOfContracts = table.Column<long>(type: "bigint", nullable: true),
                    Suspended = table.Column<bool>(type: "bit", nullable: false),
                    CorporateActionRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MarketSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TradingMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceImportId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyInstrumentMarketStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyInstrumentMarketStats_MarketDataImports_SourceImportId",
                        column: x => x.SourceImportId,
                        principalTable: "MarketDataImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DailyInstrumentMarketStats_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Symbol_Strategy_Timeframe_SessionDate",
                table: "Signals",
                columns: new[] { "SymbolId", "StrategyId", "Timeframe", "SourceSessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyInstrumentMarketStats_SourceImportId",
                table: "DailyInstrumentMarketStats",
                column: "SourceImportId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyInstrumentMarketStats_SymbolId_SessionDate",
                table: "DailyInstrumentMarketStats",
                columns: new[] { "SymbolId", "SessionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImports_Provider_SessionDate",
                table: "MarketDataImports",
                columns: new[] { "Provider", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImports_SessionDate_Sha256",
                table: "MarketDataImports",
                columns: new[] { "SessionDate", "Sha256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyInstrumentMarketStats");

            migrationBuilder.DropTable(
                name: "MarketDataImports");

            migrationBuilder.DropIndex(
                name: "IX_Signals_Symbol_Strategy_Timeframe_SessionDate",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "SourceSessionDate",
                table: "Signals");
        }
    }
}
