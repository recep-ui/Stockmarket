using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.PaperTrading;

public record PaperPortfolioDto(
    long Id,
    long UserId,
    string Name,
    decimal InitialBalance,
    decimal CashBalance,
    decimal PortfolioValue,
    decimal TotalPnL,
    decimal TotalPnLPercent,
    bool IsAutoTradingEnabled,
    int AutoTradingMinScore,
    decimal AutoTradingMaxAllocationPercent
);

public record PaperPositionDto(
    long Id,
    int SymbolId,
    string Symbol,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    decimal TotalCost,
    decimal CurrentValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPercent
);

public record CreatePaperOrderRequest(
    long PortfolioId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice = null,
    string? ClientOrderId = null
);

public record PaperTradeDto(
    long Id,
    int SymbolId,
    string Symbol,
    OrderSide Side,
    decimal Quantity,
    decimal Price,
    decimal TotalValue,
    decimal RealizedPnL,
    decimal Commission,
    DateTime ExecutedAt,
    long? SourceSignalId = null
);
