using BistQuant.Application.Common.Interfaces;
using BistQuant.Infrastructure.Caching;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BistQuant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";
        var sqlServerConn = configuration.GetConnectionString("DefaultConnection") 
            ?? (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase) 
                ? throw new InvalidOperationException("Connection string 'DefaultConnection' is required when DatabaseProvider is SqlServer.") 
                : string.Empty);
        var sqliteConn = configuration.GetConnectionString("SqliteConnection") 
            ?? "Data Source=bistquant.db;Cache=Shared;";

        services.AddDbContext<BistQuantDbContext>((sp, options) =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(sqliteConn, b => b.MigrationsAssembly(typeof(BistQuantDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlServer(sqlServerConn, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(BistQuantDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                });
            }

            options.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly, ProviderSpecificMigrationsAssembly>();
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<BistQuantDbContext>());

        // In-Memory Cache always available as foundational layer
        services.AddMemoryCache();

        // Redis Connection (Optional - falls back to MemoryCache)
        var redisConnString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnString))
        {
            try
            {
                var options = ConfigurationOptions.Parse(redisConnString);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 1000;
                var multiplexer = ConnectionMultiplexer.Connect(options);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }
            catch
            {
                // Fallback handled gracefully in CacheService
            }
        }

        services.AddSingleton<ICacheService, CacheService>();

        // Market Data Services
        services.AddHttpClient<IBistDailyBulletinMarketDataProvider, Providers.MarketData.BistDailyBulletinMarketDataProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/csv,application/zip,text/plain,*/*");
        });

        // Register active IMarketDataProvider as the official BIST Daily Bulletin provider
        services.AddScoped<Application.Interfaces.IMarketDataProvider>(sp => sp.GetRequiredService<IBistDailyBulletinMarketDataProvider>());
        services.AddScoped<Providers.MarketData.MockMarketDataProvider>();
        services.AddScoped<IBistBulletinBackfillService, Services.MarketData.BistBulletinBackfillService>();
        services.AddScoped<Providers.MarketData.ICsvMarketDataService, Providers.MarketData.CsvMarketDataService>();

        // Notification Providers
        services.AddScoped<Application.Interfaces.INotificationProvider, Providers.Notifications.TelegramNotificationProvider>();
        services.AddScoped<Application.Interfaces.INotificationProvider, Providers.Notifications.InAppNotificationProvider>();

        // Security & Auth
        services.AddScoped<Application.Interfaces.IJwtService, Services.JwtService>();

        return services;
    }
}
