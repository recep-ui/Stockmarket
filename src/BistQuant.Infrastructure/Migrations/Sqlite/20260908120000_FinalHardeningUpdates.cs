using System;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.Sqlite
{
    [DbContext(typeof(BistQuantDbContext))]
    [Migration("20260908120000_FinalHardeningUpdates")]
    public partial class FinalHardeningUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedCandleClose",
                table: "WorkerHeartbeats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataTimestamp",
                table: "WorkerHeartbeats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaperOrderId",
                table: "PaperTrades",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrades_PaperOrderId",
                table: "PaperTrades",
                column: "PaperOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaperTrades_PaperOrderId",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "PaperOrderId",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "DataTimestamp",
                table: "WorkerHeartbeats");

            migrationBuilder.DropColumn(
                name: "ExpectedCandleClose",
                table: "WorkerHeartbeats");
        }
    }
}
