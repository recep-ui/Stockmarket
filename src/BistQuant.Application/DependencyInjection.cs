using BistQuant.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BistQuant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITechnicalAnalysisService, TechnicalAnalysisService>();
        services.AddScoped<IScoringEngine, ScoringEngine>();
        services.AddScoped<ISignalEngine, SignalEngine>();
        services.AddScoped<IMarketScannerService, MarketScannerService>();
        services.AddScoped<IStrategyEngine, StrategyEngine>();
        services.AddScoped<IStrategyEvaluationPipeline, StrategyEvaluationPipeline>();
        services.AddScoped<IBacktestEngine, BacktestEngine>();
        services.AddScoped<IAlertEngine, AlertEngine>();
        services.AddScoped<IPaperTradingService, PaperTradingService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
