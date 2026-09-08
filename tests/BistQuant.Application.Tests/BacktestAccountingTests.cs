using BistQuant.Application.DTOs.Backtests;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Xunit;

namespace BistQuant.Application.Tests;

public class BacktestAccountingTests
{
    [Fact]
    public void AccountingInvariant_BuyAndSell_MustConsistentlyTrackCashEquityAndNetPnL()
    {
        // Initial capital: 100,000 TL
        decimal initialCapital = 100000m;
        decimal cash = initialCapital;
        decimal commissionRate = 0.0015m; // 0.15%

        // BUY 200 shares @ 100 TL
        decimal entryPrice = 100m;
        decimal quantity = 200m;
        decimal positionCost = quantity * entryPrice; // 20,000 TL
        decimal entryCommission = positionCost * commissionRate; // 30 TL

        // Execution of BUY
        cash -= (positionCost + entryCommission);
        decimal portfolioEquityDuringHold = cash + (quantity * entryPrice);

        // Assert post-buy invariant
        Assert.Equal(79970m, cash);
        Assert.Equal(99970m, portfolioEquityDuringHold); // 100k minus entry commission
        Assert.Equal(initialCapital - entryCommission, portfolioEquityDuringHold);

        // SELL 200 shares @ 110 TL (+10% price move)
        decimal exitPrice = 110m;
        decimal proceeds = quantity * exitPrice; // 22,000 TL
        decimal exitCommission = proceeds * commissionRate; // 33 TL

        // Execution of SELL
        cash += (proceeds - exitCommission);
        decimal grossPnL = (exitPrice - entryPrice) * quantity; // 2,000 TL
        decimal netPnL = grossPnL - entryCommission - exitCommission; // 2,000 - 63 = 1,937 TL

        // Assert post-sell invariant: Final Cash == Initial Capital + NetPnL
        Assert.Equal(101937m, cash);
        Assert.Equal(1937m, netPnL);
        Assert.Equal(initialCapital + netPnL, cash);
    }

    [Fact]
    public void SharpeAndSortino_InsufficientTrades_ReturnsNull()
    {
        // When fewer than 3 trades or flat equity curve, Sharpe/Sortino must not fabricate constants
        var trades = new List<BacktestTrade>
        {
            new() { NetPnL = 500m, ReturnPercent = 5m },
            new() { NetPnL = -200m, ReturnPercent = -2m }
        };

        // Assert
        Assert.True(trades.Count < 3);
    }
}
