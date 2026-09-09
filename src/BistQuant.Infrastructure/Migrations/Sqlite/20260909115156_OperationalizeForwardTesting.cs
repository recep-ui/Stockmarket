using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class OperationalizeForwardTesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketDataImports_Provider_SessionDate",
                table: "MarketDataImports");

            migrationBuilder.AddColumn<bool>(
                name: "SourceBulletinRevisedAfterExecution",
                table: "PaperTrades",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SourceSignalId",
                table: "PaperTrades",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ForwardTestStartDate",
                table: "PaperPortfolios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForwardTest",
                table: "PaperPortfolios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SourceBulletinRevisedAfterExecution",
                table: "PaperOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "DataOrigin",
                table: "MarketDataImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "BackfillJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CurrentDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    SessionsTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionsSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionsFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    BarsInserted = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackfillJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForwardTestDailyReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PortfolioId = table.Column<long>(type: "INTEGER", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BulletinRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    SymbolsAnalyzed = table.Column<int>(type: "INTEGER", nullable: false),
                    SignalsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    BuySignals = table.Column<int>(type: "INTEGER", nullable: false),
                    SellSignals = table.Column<int>(type: "INTEGER", nullable: false),
                    OrdersQueued = table.Column<int>(type: "INTEGER", nullable: false),
                    OrdersFilled = table.Column<int>(type: "INTEGER", nullable: false),
                    OrdersExpired = table.Column<int>(type: "INTEGER", nullable: false),
                    RealizedPnL = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnrealizedPnL = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PortfolioEquity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DrawdownPercent = table.Column<decimal>(type: "TEXT", precision: 10, scale: 4, nullable: false),
                    Errors = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardTestDailyReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForwardTestDailyReports_PaperPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "PaperPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrades_SourceSignalId",
                table: "PaperTrades",
                column: "SourceSignalId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperOrders_Portfolio_Status_TargetExecutionSessionDate",
                table: "PaperOrders",
                columns: new[] { "PortfolioId", "Status", "TargetExecutionSessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImports_Provider_SessionDate_IsCurrent",
                table: "MarketDataImports",
                columns: new[] { "Provider", "SessionDate", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "UIX_MarketDataImports_Provider_SessionDate_CurrentSuccess",
                table: "MarketDataImports",
                columns: new[] { "Provider", "SessionDate" },
                unique: true,
                filter: "[IsCurrent] = 1 AND [Status] = 4");

            migrationBuilder.CreateIndex(
                name: "IX_BackfillJobs_Status_CurrentDate",
                table: "BackfillJobs",
                columns: new[] { "Status", "CurrentDate" });

            migrationBuilder.CreateIndex(
                name: "UIX_ForwardTestDailyReports_Portfolio_SessionDate",
                table: "ForwardTestDailyReports",
                columns: new[] { "PortfolioId", "SessionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackfillJobs");

            migrationBuilder.DropTable(
                name: "ForwardTestDailyReports");

            migrationBuilder.DropIndex(
                name: "IX_PaperTrades_SourceSignalId",
                table: "PaperTrades");

            migrationBuilder.DropIndex(
                name: "IX_PaperOrders_Portfolio_Status_TargetExecutionSessionDate",
                table: "PaperOrders");

            migrationBuilder.DropIndex(
                name: "IX_MarketDataImports_Provider_SessionDate_IsCurrent",
                table: "MarketDataImports");

            migrationBuilder.DropIndex(
                name: "UIX_MarketDataImports_Provider_SessionDate_CurrentSuccess",
                table: "MarketDataImports");

            migrationBuilder.DropColumn(
                name: "SourceBulletinRevisedAfterExecution",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "SourceSignalId",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "ForwardTestStartDate",
                table: "PaperPortfolios");

            migrationBuilder.DropColumn(
                name: "IsForwardTest",
                table: "PaperPortfolios");

            migrationBuilder.DropColumn(
                name: "SourceBulletinRevisedAfterExecution",
                table: "PaperOrders");

            migrationBuilder.DropColumn(
                name: "DataOrigin",
                table: "MarketDataImports");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImports_Provider_SessionDate",
                table: "MarketDataImports",
                columns: new[] { "Provider", "SessionDate" });
        }
    }
}
