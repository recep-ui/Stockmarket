using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(BistQuantDbContext context, ILogger logger, bool isDevelopment = true)
    {
        logger.LogInformation("Applying pending database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");

        if (isDevelopment)
        {
            logger.LogInformation("Development/Demo mode detected: Seeding initial market data and demo user...");
            await SeedMarketsAndSymbolsAsync(context, logger);
            await SeedHistoricalDataAsync(context, logger);
            await SeedDefaultUserAsync(context, logger);
        }
        else
        {
            logger.LogInformation("Production mode: Skipping demo/random data generation.");
        }
    }

    private static async Task SeedDefaultUserAsync(BistQuantDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync()) return;

        var user = new User
        {
            Email = "demo@bistquant.com",
            PasswordHash = BistQuant.Application.Common.Security.PasswordHasher.HashPassword("Demo1234!"),
            DisplayName = "BIST Quant Demo",
            Role = "Admin",
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded default demo user (demo@bistquant.com).");
    }

    private static async Task SeedMarketsAndSymbolsAsync(BistQuantDbContext context, ILogger logger)
    {
        if (await context.Markets.AnyAsync())
        {
            return;
        }

        var bist = new Market
        {
            Code = "BIST",
            Name = "Borsa Istanbul",
            Country = "Turkey",
            Currency = "TRY",
            Timezone = "Europe/Istanbul"
        };

        context.Markets.Add(bist);
        await context.SaveChangesAsync();

        var symbols = new List<Symbol>
        {
            new() { MarketId = bist.Id, Ticker = "THYAO", Name = "Turk Hava Yollari", Sector = "Ulastirma", Industry = "Havacilik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "ASELS", Name = "Aselsan Elektronik", Sector = "Savunma", Industry = "Elektronik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "TUPRS", Name = "Tupras Turkiye Petrol", Sector = "Enerji", Industry = "Rafineri", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "TOASO", Name = "Tofas Turk Otomobil", Sector = "Otomotiv", Industry = "Imalat", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "SISE", Name = "Turkiye Sise ve Cam", Sector = "Sanayi", Industry = "Cam", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "EREGL", Name = "Eregli Demir Celik", Sector = "Metal", Industry = "Demir Celik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "KCHOL", Name = "Koc Holding", Sector = "Holding", Industry = "Holding", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "SAHOL", Name = "Sabanci Holding", Sector = "Holding", Industry = "Holding", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "BIMAS", Name = "BIM Birlesik Magazalar", Sector = "Perakende", Industry = "Gida Perakende", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "FROTO", Name = "Ford Otosan", Sector = "Otomotiv", Industry = "Imalat", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "AKBNK", Name = "Akbank", Sector = "Bankacilik", Industry = "Bankacilik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "GARAN", Name = "Garanti BBVA", Sector = "Bankacilik", Industry = "Bankacilik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "ISCTR", Name = "Turkiye Is Bankasi", Sector = "Bankacilik", Industry = "Bankacilik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "YKBNK", Name = "Yapi Kredi Bankasi", Sector = "Bankacilik", Industry = "Bankacilik", IsActive = true },
            new() { MarketId = bist.Id, Ticker = "PETKM", Name = "Petkim Petrokimya", Sector = "Kimya", Industry = "Petrokimya", IsActive = true }
        };

        context.Symbols.AddRange(symbols);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded BIST market and {Count} top symbols.", symbols.Count);
    }

    private static async Task SeedHistoricalDataAsync(BistQuantDbContext context, ILogger logger)
    {
        if (await context.PriceBars.AnyAsync())
        {
            return;
        }

        var symbols = await context.Symbols.ToListAsync();
        var random = new Random(42);
        var basePrices = new Dictionary<string, decimal>
        {
            ["THYAO"] = 320.0m,
            ["ASELS"] = 62.5m,
            ["TUPRS"] = 175.0m,
            ["TOASO"] = 245.0m,
            ["SISE"] = 48.0m,
            ["EREGL"] = 52.0m,
            ["KCHOL"] = 210.0m,
            ["SAHOL"] = 92.0m,
            ["BIMAS"] = 480.0m,
            ["FROTO"] = 1050.0m,
            ["AKBNK"] = 56.0m,
            ["GARAN"] = 110.0m,
            ["ISCTR"] = 14.5m,
            ["YKBNK"] = 31.0m,
            ["PETKM"] = 22.0m
        };

        var allBars = new List<PriceBar>();
        var now = DateTime.UtcNow;

        foreach (var sym in symbols)
        {
            var currentPrice = basePrices.TryGetValue(sym.Ticker, out var bp) ? bp : 100.0m;
            // Generate 120 daily bars (approx 6 months)
            for (int i = 120; i >= 0; i--)
            {
                var barDate = now.Date.AddDays(-i);
                // Skip weekends
                if (barDate.DayOfWeek == DayOfWeek.Saturday || barDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                var dailyDrift = (decimal)(random.NextDouble() * 0.04 - 0.018); // slight upward bias
                var open = Math.Round(currentPrice * (1 + (decimal)(random.NextDouble() * 0.01 - 0.005)), 2);
                var close = Math.Round(open * (1 + dailyDrift), 2);
                var high = Math.Round(Math.Max(open, close) * (1 + (decimal)(random.NextDouble() * 0.015)), 2);
                var low = Math.Round(Math.Min(open, close) * (1 - (decimal)(random.NextDouble() * 0.015)), 2);
                var volume = Math.Round((decimal)(random.Next(500000, 15000000)), 2);

                allBars.Add(new PriceBar
                {
                    SymbolId = sym.Id,
                    Timeframe = Timeframe.Daily,
                    Timestamp = barDate,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                });

                currentPrice = close;
            }
        }

        context.PriceBars.AddRange(allBars);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} historical OHLCV price bars across {SymbolCount} symbols.", allBars.Count, symbols.Count);
    }
}
