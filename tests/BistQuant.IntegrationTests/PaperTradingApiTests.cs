using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class PaperTradingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PaperTradingApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"trader_{Guid.NewGuid():N}@test.com";
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(uniqueEmail, "Pass12345!", "Trader"));
        var regData = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regData!.Data!.Token);
        return client;
    }

    [Fact]
    public async Task Get_DefaultPortfolio_ShouldReturnVirtualAccount()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/paper-portfolios");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaperPortfolioDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100000m, result.Data.InitialBalance);
        Assert.True(result.Data.CashBalance > 0);
    }

    [Fact]
    public async Task ExecuteOrder_BuyAndSell_ShouldUpdatePositionsAndTrades()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Get portfolio
        var portResponse = await client.GetAsync("/api/paper-portfolios");
        var portResult = await portResponse.Content.ReadFromJsonAsync<ApiResponse<PaperPortfolioDto>>();
        Assert.NotNull(portResult?.Data);
        var portfolioId = portResult.Data.Id;

        // 2. Buy order: 10 shares of ASELS
        var buyOrder = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "ASELS",
            Side: OrderSide.Buy,
            Type: OrderType.Market,
            Quantity: 10
        );

        var buyResponse = await client.PostAsJsonAsync("/api/paper-portfolios/orders", buyOrder);
        Assert.Equal(HttpStatusCode.OK, buyResponse.StatusCode);

        var buyResult = await buyResponse.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>();
        Assert.NotNull(buyResult?.Data);
        Assert.Equal("ASELS", buyResult.Data.Symbol);
        Assert.Equal(10, buyResult.Data.Quantity);
        Assert.Equal(OrderSide.Buy, buyResult.Data.Side);

        // 3. Verify positions
        var posResponse = await client.GetAsync($"/api/paper-portfolios/{portfolioId}/positions");
        Assert.Equal(HttpStatusCode.OK, posResponse.StatusCode);

        var posResult = await posResponse.Content.ReadFromJsonAsync<ApiResponse<List<PaperPositionDto>>>();
        Assert.NotNull(posResult?.Data);
        Assert.Contains(posResult.Data, p => p.Symbol == "ASELS" && p.Quantity >= 10);

        // 4. Sell order: 5 shares of ASELS
        var sellOrder = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "ASELS",
            Side: OrderSide.Sell,
            Type: OrderType.Market,
            Quantity: 5
        );

        var sellResponse = await client.PostAsJsonAsync("/api/paper-portfolios/orders", sellOrder);
        Assert.Equal(HttpStatusCode.OK, sellResponse.StatusCode);

        var sellResult = await sellResponse.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>();
        Assert.NotNull(sellResult?.Data);
        Assert.Equal(OrderSide.Sell, sellResult.Data.Side);

        // 5. Verify trades
        var tradesResponse = await client.GetAsync($"/api/paper-portfolios/{portfolioId}/trades");
        Assert.Equal(HttpStatusCode.OK, tradesResponse.StatusCode);

        var tradesResult = await tradesResponse.Content.ReadFromJsonAsync<ApiResponse<List<PaperTradeDto>>>();
        Assert.NotNull(tradesResult?.Data);
        Assert.True(tradesResult.Data.Count >= 2);
    }

    [Fact]
    public async Task ExecuteOrder_IdempotencyExactOrderMapping_ReturnsCorrectTrade()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Get portfolio
        var portResponse = await client.GetAsync("/api/paper-portfolios");
        var portResult = await portResponse.Content.ReadFromJsonAsync<ApiResponse<PaperPortfolioDto>>();
        Assert.NotNull(portResult?.Data);
        var portfolioId = portResult.Data.Id;

        var clientOrderIdA = $"order_idemp_A_{Guid.NewGuid():N}";
        var clientOrderIdB = $"order_idemp_B_{Guid.NewGuid():N}";

        // 2. Execute Order A: 10 shares of THYAO
        var orderA = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "THYAO",
            Side: OrderSide.Buy,
            Type: OrderType.Market,
            Quantity: 10,
            ClientOrderId: clientOrderIdA
        );

        var respA = await client.PostAsJsonAsync("/api/paper-portfolios/orders", orderA);
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        var tradeA = (await respA.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>())!.Data!;
        Assert.Equal(10, tradeA.Quantity);

        // 3. Execute Order B later for same symbol: 25 shares of THYAO
        var orderB = new CreatePaperOrderRequest(
            PortfolioId: portfolioId,
            Symbol: "THYAO",
            Side: OrderSide.Buy,
            Type: OrderType.Market,
            Quantity: 25,
            ClientOrderId: clientOrderIdB
        );

        var respB = await client.PostAsJsonAsync("/api/paper-portfolios/orders", orderB);
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        var tradeB = (await respB.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>())!.Data!;
        Assert.Equal(25, tradeB.Quantity);
        Assert.NotEqual(tradeA.Id, tradeB.Id);

        // 4. Re-submit idempotent Order A -> MUST return Trade A, NOT more recent Trade B!
        var respAIdempotent = await client.PostAsJsonAsync("/api/paper-portfolios/orders", orderA);
        Assert.Equal(HttpStatusCode.OK, respAIdempotent.StatusCode);
        var tradeAReturned = (await respAIdempotent.Content.ReadFromJsonAsync<ApiResponse<PaperTradeDto>>())!.Data!;

        Assert.Equal(tradeA.Id, tradeAReturned.Id);
        Assert.Equal(10, tradeAReturned.Quantity);
        Assert.NotEqual(tradeB.Id, tradeAReturned.Id);
    }
}
