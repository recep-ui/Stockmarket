using System;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.SqlServer
{
    [DbContext(typeof(BistQuantDbContext))]
    [Migration("20260908120001_FinalHardeningUpdates")]
    public partial class FinalHardeningUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedCandleClose",
                table: "WorkerHeartbeats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataTimestamp",
                table: "WorkerHeartbeats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaperOrderId",
                table: "PaperTrades",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrades_PaperOrderId",
                table: "PaperTrades",
                column: "PaperOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaperTrades_PaperOrders_PaperOrderId",
                table: "PaperTrades",
                column: "PaperOrderId",
                principalTable: "PaperOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaperTrades_PaperOrders_PaperOrderId",
                table: "PaperTrades");

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
