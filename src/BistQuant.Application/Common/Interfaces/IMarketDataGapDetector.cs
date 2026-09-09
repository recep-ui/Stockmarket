using BistQuant.Application.DTOs.MarketData;

namespace BistQuant.Application.Common.Interfaces;

public interface IMarketDataGapDetector
{
    Task<SymbolGapsDto> DetectGapsForSymbolAsync(string ticker, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<UniverseCoverageSummaryDto> GetUniverseCoverageAsync(DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default);
}
