using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;

namespace BistQuant.Application.Common.Interfaces;

public interface IBistDailyBulletinMarketDataProvider : IMarketDataProvider
{
    Task<BulletinDownloadResult> DownloadBulletinForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<MarketDataImport> ImportBulletinForDateAsync(DateOnly date, bool force = false, CancellationToken cancellationToken = default);
    Task<MarketDataImport> ImportBulletinStreamAsync(DateOnly? dateHint, string fileName, Stream contentStream, CancellationToken cancellationToken = default);
    Task<MarketDataImport?> GetLatestImportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketDataImport>> GetRecentImportsAsync(int count = 30, CancellationToken cancellationToken = default);
}
