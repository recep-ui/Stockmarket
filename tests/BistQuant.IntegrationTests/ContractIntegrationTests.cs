using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Alerts;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.DTOs.Indicators;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Application.DTOs.Scanner;
using BistQuant.Application.DTOs.Signals;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Application.DTOs.Watchlists;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BistQuant.IntegrationTests;

public class ContractIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ContractIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient Client, AuthResponseDto Auth)> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"contract_user_{Guid.NewGuid():N}@bistquant.com";
        var regRequest = new RegisterRequest(uniqueEmail, "Pass12345!", "Contract QA Tester");
        
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
        
        var regResult = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(regResult?.Data);
        Assert.NotEmpty(regResult.Data.Token);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regResult.Data.Token);
        return (client, regResult.Data);
    }

    [Fact]
    public async Task Execute_Complete_Contract_Workflow_AllTwelveOperations()
    {
        // 1. LOGIN & AUTH
        var (authClient, auth) = await CreateAuthenticatedClientAsync();
        Assert.False(string.IsNullOrEmpty(auth.Token), "JWT token must not be empty");
        Assert.True(auth.UserId > 0, "UserId must be positive");
        Assert.Equal("Contract QA Tester", auth.DisplayName);

        // Verify Login endpoint specifically
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(auth.Email, "Pass12345!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginResult = await loginResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(loginResult?.Data);
        Assert.Equal(auth.Email, loginResult.Data.Email);

        // 2. DASHBOARD / SCANNER OVERVIEW
        var overviewResp = await authClient.GetAsync("/api/scanner/overview");
        Assert.Equal(HttpStatusCode.OK, overviewResp.StatusCode);
        var overview = await overviewResp.Content.ReadFromJsonAsync<ApiResponse<MarketOverviewDto>>();
        Assert.NotNull(overview?.Data);
        Assert.True(overview.Data.TotalSymbols >= 0);
        Assert.NotNull(overview.Data.TopSignals);

        // 3. SCANNER RESULTS (PAGED)
        var scanResp = await authClient.GetAsync("/api/scanner?timeframe=Daily&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, scanResp.StatusCode);
        var scanResult = await scanResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ScannerItemDto>>>();
        Assert.NotNull(scanResult?.Data);
        Assert.NotNull(scanResult.Data.Items);

        // 4. STOCK DETAIL (SYMBOLS & TECHNICAL ANALYSIS)
        var symbolResp = await authClient.GetAsync("/api/symbols/THYAO");
        Assert.Equal(HttpStatusCode.OK, symbolResp.StatusCode);
        var symbolResult = await symbolResp.Content.ReadFromJsonAsync<ApiResponse<SymbolDto>>();
        Assert.NotNull(symbolResult?.Data);
        Assert.Equal("THYAO", symbolResult.Data.Ticker);

        var techResp = await authClient.GetAsync("/api/analysis/THYAO/technical?timeframe=Daily");
        Assert.Equal(HttpStatusCode.OK, techResp.StatusCode);
        var techResult = await techResp.Content.ReadFromJsonAsync<ApiResponse<IndicatorSnapshotDto>>();
        Assert.NotNull(techResult?.Data);
        Assert.Equal("THYAO", techResult.Data.Symbol);
        Assert.True(techResult.Data.Timeframe == Timeframe.Daily);

        var signalResp = await authClient.GetAsync("/api/analysis/THYAO/signals?timeframe=Daily");
        Assert.Equal(HttpStatusCode.OK, signalResp.StatusCode);
        var signalResult = await signalResp.Content.ReadFromJsonAsync<ApiResponse<SignalDto>>();
        Assert.NotNull(signalResult?.Data);
        Assert.Equal("THYAO", signalResult.Data.Symbol);

        // 5. CREATE STRATEGY (Contract: Name, Description, StrategyType, Timeframe, Rules)
        var createStratReq = new CreateStrategyRequest(
            Name: $"Trend Strategy {Guid.NewGuid():N}",
            Description: "Integration test strategy",
            StrategyType: "Technical",
            Timeframe: Timeframe.Daily,
            Rules: new List<StrategyRuleDto>()
        );
        var stratResp = await authClient.PostAsJsonAsync("/api/strategies", createStratReq);
        Assert.Equal(HttpStatusCode.Created, stratResp.StatusCode);
        var stratResult = await stratResp.Content.ReadFromJsonAsync<ApiResponse<StrategyDto>>();
        Assert.NotNull(stratResult?.Data);
        Assert.Equal(createStratReq.Name, stratResult.Data.Name);
        Assert.Equal("Technical", stratResult.Data.StrategyType);
        Assert.Equal(Timeframe.Daily, stratResult.Data.Timeframe);
        var createdStrategyId = stratResult.Data.Id;

        // 6. RUN BACKTEST (Contract: Timeframe.Daily = 250, TotalReturn, MaxDrawdown, NetPnL, ReturnPercent, EquityCurve.Date)
        var backtestReq = new BacktestRunRequest(
            StrategyId: createdStrategyId,
            Symbol: "THYAO",
            Timeframe: Timeframe.Daily,
            InitialCapital: 100000m,
            CommissionRate: 0.0015m,
            SlippageRate: 0.0010m
        );
        var btResp = await authClient.PostAsJsonAsync("/api/backtests", backtestReq);
        Assert.Equal(HttpStatusCode.OK, btResp.StatusCode);
        var btResult = await btResp.Content.ReadFromJsonAsync<ApiResponse<BacktestRunDto>>();
        Assert.NotNull(btResult?.Data);
        Assert.Equal(BacktestStatus.Completed, btResult.Data.Status);
        Assert.NotNull(btResult.Data.Result);
        Assert.NotNull(btResult.Data.Result.EquityCurve);
        if (btResult.Data.Result.EquityCurve.Count > 0)
        {
            var firstPt = btResult.Data.Result.EquityCurve[0];
            Assert.False(string.IsNullOrEmpty(firstPt.Date), "EquityPoint must contain Date property");
            Assert.True(firstPt.Equity > 0, "Equity must be positive");
        }

        // Check Backtest Trades endpoint
        var tradesResp = await authClient.GetAsync($"/api/backtests/{btResult.Data.Id}/trades");
        Assert.Equal(HttpStatusCode.OK, tradesResp.StatusCode);
        var tradesResult = await tradesResp.Content.ReadFromJsonAsync<ApiResponse<List<BacktestTradeDto>>>();
        Assert.NotNull(tradesResult?.Data);

        // 7. CREATE / GET PAPER PORTFOLIO (Contract: PortfolioValue, CashBalance)
        var portResp = await authClient.GetAsync("/api/paper-portfolios");
        Assert.Equal(HttpStatusCode.OK, portResp.StatusCode);
        var portResult = await portResp.Content.ReadFromJsonAsync<ApiResponse<PaperPortfolioDto>>();
        Assert.NotNull(portResult?.Data);
        Assert.True(portResult.Data.PortfolioValue > 0, "PortfolioValue must be populated");
        Assert.True(portResult.Data.CashBalance > 0, "CashBalance must be populated");
        var portfolioId = portResult.Data.Id;

        // 8. PAPER BUY (Contract: PortfolioId, Symbol, Side, Type, Quantity)
        var buyOrder = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "THYAO",
            Side: OrderSide.Buy,
            Type: OrderType.Market,
            Quantity: 10,
            ClientOrderId: $"buy_{Guid.NewGuid():N}"
        );
        var buyResp = await authClient.PostAsJsonAsync("/api/paper-portfolios/orders", buyOrder);
        Assert.Equal(HttpStatusCode.OK, buyResp.StatusCode);
        var buyResult = await buyResp.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>();
        Assert.NotNull(buyResult?.Data);
        Assert.Equal("THYAO", buyResult.Data.Symbol);
        Assert.Equal(OrderSide.Buy, buyResult.Data.Side);
        Assert.True(buyResult.Data.Price > 0);
        Assert.True(buyResult.Data.Commission > 0);

        // 9. PAPER SELL (Contract: Partial sell and RealizedPnL)
        var sellOrder = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "THYAO",
            Side: OrderSide.Sell,
            Type: OrderType.Market,
            Quantity: 5,
            ClientOrderId: $"sell_{Guid.NewGuid():N}"
        );
        var sellResp = await authClient.PostAsJsonAsync("/api/paper-portfolios/orders", sellOrder);
        Assert.Equal(HttpStatusCode.OK, sellResp.StatusCode);
        var sellResult = await sellResp.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>();
        Assert.NotNull(sellResult?.Data);
        Assert.Equal("THYAO", sellResult.Data.Symbol);
        Assert.Equal(OrderSide.Sell, sellResult.Data.Side);

        // 10. CREATE ALERT (Contract: Symbol ticker string, AlertType, Condition, Value, Channel)
        var alertReq = new CreateAlertRequest(
            Symbol: "THYAO",
            AlertType: "ScoreThreshold",
            Condition: ">=",
            Value: 80m,
            Channel: NotificationChannel.InApp
        );
        var alertResp = await authClient.PostAsJsonAsync("/api/alerts", alertReq);
        Assert.Equal(HttpStatusCode.OK, alertResp.StatusCode);
        var alertResult = await alertResp.Content.ReadFromJsonAsync<ApiResponse<AlertSubscriptionDto>>();
        Assert.NotNull(alertResult?.Data);
        Assert.Equal("THYAO", alertResult.Data.Symbol);
        Assert.Equal(NotificationChannel.InApp, alertResult.Data.Channel);
        var alertId = alertResult.Data.Id;

        // 11. CREATE WATCHLIST (Contract: Name, Description)
        var createWlReq = new CreateWatchlistRequest("Ana İzleme Listesi", "Gözde BIST 30 hisseleri");
        var wlResp = await authClient.PostAsJsonAsync("/api/watchlists", createWlReq);
        Assert.Equal(HttpStatusCode.Created, wlResp.StatusCode);
        var wlResult = await wlResp.Content.ReadFromJsonAsync<ApiResponse<WatchlistDto>>();
        Assert.NotNull(wlResult?.Data);
        Assert.Equal(createWlReq.Name, wlResult.Data.Name);
        var watchlistId = wlResult.Data.Id;

        // 12. ADD WATCHLIST SYMBOL (Contract: AddWatchlistItemRequest.Symbol ticker)
        var addItemReq = new AddWatchlistItemRequest("THYAO");
        var addItemResp = await authClient.PostAsJsonAsync($"/api/watchlists/{watchlistId}/items", addItemReq);
        Assert.Equal(HttpStatusCode.OK, addItemResp.StatusCode);
        var addItemResult = await addItemResp.Content.ReadFromJsonAsync<ApiResponse<WatchlistDto>>();
        Assert.NotNull(addItemResult?.Data);
        Assert.Contains(addItemResult.Data.Items, item => item.Ticker == "THYAO");
        var addedItem = addItemResult.Data.Items.First(item => item.Ticker == "THYAO");
        Assert.True(addedItem.CurrentPrice > 0, "CurrentPrice must be present in WatchlistItemDto");
    }
}
