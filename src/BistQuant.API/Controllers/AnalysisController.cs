using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Indicators;
using BistQuant.Application.DTOs.Signals;
using BistQuant.Application.Services;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly ITechnicalAnalysisService _technicalService;
    private readonly ISignalEngine _signalEngine;

    public AnalysisController(
        ITechnicalAnalysisService technicalService,
        ISignalEngine signalEngine)
    {
        _technicalService = technicalService;
        _signalEngine = signalEngine;
    }

    [HttpGet("{symbol}/technical")]
    public async Task<ActionResult<ApiResponse<IndicatorSnapshotDto>>> GetTechnicalSnapshot(
        string symbol,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _technicalService.GetLatestSnapshotAsync(symbol, timeframe, cancellationToken);
        if (snapshot == null)
        {
            return NotFound(ApiResponse<IndicatorSnapshotDto>.Fail($"Technical snapshot for '{symbol}' could not be computed."));
        }

        var dto = new IndicatorSnapshotDto(
            symbol.ToUpperInvariant(),
            snapshot.Timeframe,
            snapshot.Timestamp,
            snapshot.EMA20,
            snapshot.EMA50,
            snapshot.EMA100,
            snapshot.EMA200,
            snapshot.SMA20,
            snapshot.SMA50,
            snapshot.SMA200,
            snapshot.RSI14,
            snapshot.MACD,
            snapshot.MACDSignal,
            snapshot.MACDHistogram,
            snapshot.ATR14,
            snapshot.SuperTrend,
            snapshot.SuperTrendDirection,
            snapshot.BollingerUpper,
            snapshot.BollingerMiddle,
            snapshot.BollingerLower,
            snapshot.AverageVolume20,
            snapshot.VolumeRatio,
            snapshot.OBV,
            snapshot.Support1,
            snapshot.Support2,
            snapshot.Resistance1,
            snapshot.Resistance2,
            snapshot.IsBreakout
        );

        return Ok(ApiResponse<IndicatorSnapshotDto>.Ok(dto));
    }

    [HttpGet("{symbol}/signals")]
    public async Task<ActionResult<ApiResponse<SignalDto>>> GetSignals(
        string symbol,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        var signal = await _signalEngine.GetLatestSignalAsync(symbol, timeframe, cancellationToken);
        if (signal == null)
        {
            return NotFound(ApiResponse<SignalDto>.Fail($"Signals for '{symbol}' could not be generated."));
        }

        var snapshot = await _technicalService.GetLatestSnapshotAsync(symbol, timeframe, cancellationToken);

        var dto = new SignalDto(
            symbol.ToUpperInvariant(),
            signal.Price,
            signal.Score,
            signal.SignalType.ToString().ToUpperInvariant(),
            new ScoresSummaryDto(signal.TrendScore, signal.MomentumScore, signal.VolumeScore, signal.StructureScore),
            new IndicatorsSummaryDto(snapshot?.RSI14, snapshot?.ADX14, snapshot?.VolumeRatio),
            new RiskSummaryDto(signal.StopLoss, signal.TakeProfit1, signal.TakeProfit2, signal.RiskRewardRatio),
            signal.Reasons.Select(r => new SignalReasonDto(r.Code, r.Title, r.Description, r.ScoreContribution, r.Indicator, r.IndicatorValue)).ToList(),
            signal.CreatedAt
        );

        return Ok(ApiResponse<SignalDto>.Ok(dto));
    }

    [HttpGet("{symbol}/reasons")]
    public async Task<ActionResult<ApiResponse<List<SignalReasonDto>>>> GetSignalReasons(
        string symbol,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        var signal = await _signalEngine.GetLatestSignalAsync(symbol, timeframe, cancellationToken);
        if (signal == null)
        {
            return NotFound(ApiResponse<List<SignalReasonDto>>.Fail($"No active signal found for '{symbol}'."));
        }

        var reasons = signal.Reasons
            .Select(r => new SignalReasonDto(r.Code, r.Title, r.Description, r.ScoreContribution, r.Indicator, r.IndicatorValue))
            .ToList();

        return Ok(ApiResponse<List<SignalReasonDto>>.Ok(reasons));
    }
}
