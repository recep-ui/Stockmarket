using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BistQuant.Infrastructure.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Markets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Markets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StrategyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timeframe = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TelegramChatId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Symbols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketId = table.Column<int>(type: "int", nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Symbols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Symbols_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StrategyId = table.Column<int>(type: "int", nullable: false),
                    SymbolList = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Timeframe = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitialCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    SlippageRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestRuns_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StrategyId = table.Column<int>(type: "int", nullable: false),
                    Indicator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Operator = table.Column<byte>(type: "tinyint", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SecondaryValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ComparisonIndicator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    RuleGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyRules_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaperPortfolios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAutoTradingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoTradingMinScore = table.Column<int>(type: "int", nullable: false),
                    AutoTradingMaxAllocationPercent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperPortfolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperPortfolios_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertSubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: true),
                    StrategyId = table.Column<int>(type: "int", nullable: true),
                    AlertType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertSubscriptions_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlertSubscriptions_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlertSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndicatorSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    Timeframe = table.Column<byte>(type: "tinyint", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EMA20 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA50 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA100 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA200 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SMA20 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SMA50 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SMA200 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SuperTrend = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SuperTrendDirection = table.Column<byte>(type: "tinyint", nullable: true),
                    ADX14 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    RSI14 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    MACD = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MACDSignal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MACDHistogram = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    StochasticK = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    StochasticD = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ROC = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ATR14 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerUpper = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerMiddle = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerLower = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VWAP = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    OBV = table.Column<decimal>(type: "decimal(24,2)", precision: 24, scale: 2, nullable: true),
                    AverageVolume20 = table.Column<decimal>(type: "decimal(24,2)", precision: 24, scale: 2, nullable: true),
                    VolumeRatio = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Support1 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Support2 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Resistance1 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Resistance2 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsBreakout = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicatorSnapshots_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceBars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    Timeframe = table.Column<byte>(type: "tinyint", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(24,2)", precision: 24, scale: 2, nullable: false),
                    AdjustedClose = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceBars_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    StrategyId = table.Column<int>(type: "int", nullable: true),
                    Timeframe = table.Column<byte>(type: "tinyint", nullable: false),
                    SignalType = table.Column<byte>(type: "tinyint", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    TrendScore = table.Column<int>(type: "int", nullable: false),
                    MomentumScore = table.Column<int>(type: "int", nullable: false),
                    VolumeScore = table.Column<int>(type: "int", nullable: false),
                    StructureScore = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TakeProfit1 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TakeProfit2 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RiskRewardRatio = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Signals_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Signals_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BacktestResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BacktestRunId = table.Column<long>(type: "bigint", nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    WinningTrades = table.Column<int>(type: "int", nullable: false),
                    LosingTrades = table.Column<int>(type: "int", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TotalReturn = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    AnnualizedReturn = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    AverageWin = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageLoss = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    SortinoRatio = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Expectancy = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EquityCurveJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestResults_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BacktestTrades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BacktestRunId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossPnL = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetPnL = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    ExitReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestTrades_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BacktestTrades_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaperOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    StopLossPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    FilledPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    FilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperOrders_PaperPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "PaperPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaperOrders_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaperPositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AveragePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperPositions_PaperPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "PaperPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaperPositions_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaperTrades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RealizedPnL = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperTrades_PaperPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "PaperPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaperTrades_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WatchlistId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistItems_Symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "Symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchlistItems_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertSubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertNotifications_AlertSubscriptions_AlertSubscriptionId",
                        column: x => x.AlertSubscriptionId,
                        principalTable: "AlertSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignalReasons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignalId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScoreContribution = table.Column<int>(type: "int", nullable: false),
                    Indicator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IndicatorValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalReasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalReasons_Signals_SignalId",
                        column: x => x.SignalId,
                        principalTable: "Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_AlertSubscriptionId",
                table: "AlertNotifications",
                column: "AlertSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertSubscriptions_StrategyId",
                table: "AlertSubscriptions",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertSubscriptions_SymbolId",
                table: "AlertSubscriptions",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertSubscriptions_UserId",
                table: "AlertSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestResults_BacktestRunId",
                table: "BacktestResults",
                column: "BacktestRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyId",
                table: "BacktestRuns",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_BacktestRunId",
                table: "BacktestTrades",
                column: "BacktestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_SymbolId",
                table: "BacktestTrades",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "UIX_IndicatorSnapshots_Symbol_Timeframe_Timestamp",
                table: "IndicatorSnapshots",
                columns: new[] { "SymbolId", "Timeframe", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Markets_Code",
                table: "Markets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperOrders_PortfolioId_ClientOrderId",
                table: "PaperOrders",
                columns: new[] { "PortfolioId", "ClientOrderId" },
                unique: true,
                filter: "ClientOrderId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaperOrders_SymbolId",
                table: "PaperOrders",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperPortfolios_UserId",
                table: "PaperPortfolios",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperPositions_PortfolioId_SymbolId",
                table: "PaperPositions",
                columns: new[] { "PortfolioId", "SymbolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperPositions_SymbolId",
                table: "PaperPositions",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrades_PortfolioId",
                table: "PaperTrades",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrades_SymbolId",
                table: "PaperTrades",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "UIX_PriceBars_Symbol_Timeframe_Timestamp",
                table: "PriceBars",
                columns: new[] { "SymbolId", "Timeframe", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalReasons_SignalId",
                table: "SignalReasons",
                column: "SignalId");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StrategyId",
                table: "Signals",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_SymbolId",
                table: "Signals",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Timeframe_CreatedAt_Score",
                table: "Signals",
                columns: new[] { "Timeframe", "CreatedAt", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyRules_StrategyId",
                table: "StrategyRules",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_Symbols_MarketId",
                table: "Symbols",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_Symbols_Ticker",
                table: "Symbols",
                column: "Ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_SymbolId",
                table: "WatchlistItems",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_WatchlistId_SymbolId",
                table: "WatchlistItems",
                columns: new[] { "WatchlistId", "SymbolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_UserId",
                table: "Watchlists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertNotifications");

            migrationBuilder.DropTable(
                name: "BacktestResults");

            migrationBuilder.DropTable(
                name: "BacktestTrades");

            migrationBuilder.DropTable(
                name: "IndicatorSnapshots");

            migrationBuilder.DropTable(
                name: "PaperOrders");

            migrationBuilder.DropTable(
                name: "PaperPositions");

            migrationBuilder.DropTable(
                name: "PaperTrades");

            migrationBuilder.DropTable(
                name: "PriceBars");

            migrationBuilder.DropTable(
                name: "SignalReasons");

            migrationBuilder.DropTable(
                name: "StrategyRules");

            migrationBuilder.DropTable(
                name: "WatchlistItems");

            migrationBuilder.DropTable(
                name: "AlertSubscriptions");

            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "PaperPortfolios");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "Watchlists");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "Symbols");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Markets");
        }
    }
}
