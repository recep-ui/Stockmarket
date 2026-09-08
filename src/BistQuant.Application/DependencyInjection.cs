using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Application.Services.MarketData;
using Microsoft.Extensions.DependencyInjection;

namespace BistQuant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IHolidayCalendar, ConfigurableHolidayCalendar>();
        services.AddSingleton<IMarketSessionCalendar, BistMarketSessionCalendar>();
        services.AddSingleton<IMarketScanScheduler, MarketScanScheduler>();
        services.AddSingleton<BistDailyBulletinParser>();
        services.AddScoped<ICorporateActionAdjustmentService, CorporateActionAdjustmentService>();
        services.AddScoped<ISignalClassifier, SignalClassifier>();
        services.AddScoped<IMarketDataFreshnessPolicy, MarketDataFreshnessPolicy>();
        services.AddScoped<ITechnicalAnalysisService, TechnicalAnalysisService>();
        services.AddScoped<IScoringEngine, ScoringEngine>();
        services.AddScoped<IStrategyEvaluationPipeline, StrategyEvaluationPipeline>();
        services.AddScoped<IStrategyEngine, StrategyEngine>();
        services.AddScoped<ISignalEngine>(sp => new SignalEngine(
            sp.GetRequiredService<IApplicationDbContext>(),
            sp.GetRequiredService<IStrategyEvaluationPipeline>(),
            sp.GetRequiredService<ISignalClassifier>(),
            sp.GetRequiredService<ITechnicalAnalysisService>(),
            sp.GetRequiredService<IMarketDataFreshnessPolicy>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SignalEngine>>()));
        services.AddScoped<IMarketScannerService, MarketScannerService>();
        services.AddScoped<IBacktestEngine, BacktestEngine>();
        services.AddScoped<IAlertEngine, AlertEngine>();
        services.AddScoped<IPaperTradingService, PaperTradingService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
