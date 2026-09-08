using BistQuant.Application.Services.MarketData;

namespace BistQuant.Application.Common.Interfaces;

public interface ICorporateActionAdjustmentService
{
    Task ProcessCorporateActionsAsync(
        DateOnly sessionDate,
        IReadOnlyList<BistBulletinEquityRecord> records,
        CancellationToken cancellationToken = default);
}
