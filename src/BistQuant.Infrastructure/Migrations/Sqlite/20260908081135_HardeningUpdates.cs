using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class HardeningUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Strategies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Strategies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkerHeartbeats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkerInstance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScanType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timeframe = table.Column<byte>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    SymbolCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerHeartbeats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_UserId",
                table: "Strategies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerHeartbeats_Timeframe_CompletedAt",
                table: "WorkerHeartbeats",
                columns: new[] { "Timeframe", "CompletedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Strategies_Users_UserId",
                table: "Strategies",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Strategies_Users_UserId",
                table: "Strategies");

            migrationBuilder.DropTable(
                name: "WorkerHeartbeats");

            migrationBuilder.DropIndex(
                name: "IX_Strategies_UserId",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Strategies");
        }
    }
}
