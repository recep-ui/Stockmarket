using System.Net;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Providers.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace BistQuant.Application.Tests;

public class TelegramNotificationTests
{
    private static IConfiguration CreateConfig(string? botToken, string? defaultChatId, bool simulationMode)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Telegram:BotToken"] = botToken,
            ["Telegram:DefaultChatId"] = defaultChatId,
            ["Telegram:SimulationMode"] = simulationMode.ToString().ToLowerInvariant()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_ReturnsConfirmedSuccess()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfig("valid_token_123", "987654321", simulationMode: false);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance, httpClient);

        var msg = new NotificationMessage("Test Title", "Test Body", "987654321");
        var result = await provider.SendAsync(msg);

        Assert.True(result.Success);
        Assert.False(result.IsSimulated);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenBotTokenMissing_AndSimulationDisabled_ReturnsFailedResult()
    {
        var config = CreateConfig("", "987654321", simulationMode: false);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance);

        var msg = new NotificationMessage("Test Title", "Test Body", "987654321");
        var result = await provider.SendAsync(msg);

        Assert.False(result.Success);
        Assert.Contains("BotToken is missing", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenChatIdMissing_AndSimulationDisabled_ReturnsFailedResult()
    {
        var config = CreateConfig("valid_token", "", simulationMode: false);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance);

        var msg = new NotificationMessage("Test Title", "Test Body", null);
        var result = await provider.SendAsync(msg);

        Assert.False(result.Success);
        Assert.Contains("ChatId is missing", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenHttp4xx_ReturnsFailedResult()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfig("token", "12345", simulationMode: false);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance, httpClient);

        var msg = new NotificationMessage("Test Title", "Test Body", "12345");
        var result = await provider.SendAsync(msg);

        Assert.False(result.Success);
        Assert.Contains("400", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenHttp5xx_ReturnsFailedResult()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfig("token", "12345", simulationMode: false);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance, httpClient);

        var msg = new NotificationMessage("Test Title", "Test Body", "12345");
        var result = await provider.SendAsync(msg);

        Assert.False(result.Success);
        Assert.Contains("500", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenSimulationModeEnabled_ReturnsSimulatedSuccess()
    {
        var config = CreateConfig("", "", simulationMode: true);
        var provider = new TelegramNotificationProvider(config, NullLogger<TelegramNotificationProvider>.Instance);

        var msg = new NotificationMessage("Test Title", "Test Body", null);
        var result = await provider.SendAsync(msg);

        Assert.True(result.Success);
        Assert.True(result.IsSimulated);
    }

    [Fact]
    public async Task AlertEngine_DoesNotUpdate_LastTriggeredAt_WhenNotificationFails()
    {
        // Mock provider returning failed result
        var mockProvider = new Mock<INotificationProvider>();
        mockProvider
            .Setup(p => p.SendAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDeliveryResult(false, "Telegram", null, "Dispatch failed"));

        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<Infrastructure.Persistence.BistQuantDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new Infrastructure.Persistence.BistQuantDbContext(options);
        dbContext.Database.EnsureCreated();

        var market = new Market { Id = 1, Code = "BIST", Name = "Borsa Istanbul", Country = "Turkey", Currency = "TRY", Timezone = "Europe/Istanbul" };
        var user = new User { Email = "test@user.com", DisplayName = "Test User", TelegramChatId = "999" };
        var symbol = new Symbol { Ticker = "TEST", Name = "Test Symbol", MarketId = 1, IsActive = true };
        dbContext.Markets.Add(market);
        dbContext.Users.Add(user);
        dbContext.Symbols.Add(symbol);
        await dbContext.SaveChangesAsync();

        var sub = new AlertSubscription
        {
            UserId = user.Id,
            SymbolId = symbol.Id,
            AlertType = "ScoreThreshold",
            Condition = ">=",
            Value = 50,
            Channel = NotificationChannel.Telegram,
            IsActive = true,
            LastTriggeredAt = null
        };
        dbContext.AlertSubscriptions.Add(sub);
        await dbContext.SaveChangesAsync();

        var alertEngine = new AlertEngine(dbContext, new[] { mockProvider.Object }, NullLogger<AlertEngine>.Instance);

        var signal = new Signal
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.Buy,
            Score = 80,
            Price = 100m,
            StopLoss = 95m,
            TakeProfit1 = 110m,
            TakeProfit2 = 120m,
            RiskRewardRatio = 2.0m,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        await alertEngine.ProcessAlertsForSignalAsync(signal);

        // Assert LastTriggeredAt remains null because dispatch failed!
        var refreshedSub = await dbContext.AlertSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(refreshedSub);
        Assert.Null(refreshedSub.LastTriggeredAt);
    }
}
