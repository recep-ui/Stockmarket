using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class StrategyAndBacktestApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StrategyAndBacktestApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"stratuser_{Guid.NewGuid():N}@test.com";
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(uniqueEmail, "Pass12345!", "Strat User"));
        var regData = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regData!.Data!.Token);
        return client;
    }

    [Fact]
    public async Task Get_Strategies_ShouldReturnPredefinedStrategies()
    {
        // Act
        var response = await _client.GetAsync("/api/strategies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<StrategyDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Data!);
        Assert.Contains(result.Data!, s => s.Name.Contains("Trend Following"));
    }

    [Fact]
    public async Task Post_Backtests_ShouldRunSimulationAndReturnMetrics()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Arrange
        var request = new BacktestRunRequest(
            StrategyId: null,
            Symbol: "THYAO",
            Timeframe: Timeframe.Daily,
            InitialCapital: 100000m,
            CommissionRate: 0.0015m,
            SlippageRate: 0.0010m
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/backtests", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Request failed with {response.StatusCode}: {content}");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<BacktestRunDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(BacktestStatus.Completed, result.Data.Status);
        Assert.NotNull(result.Data.Result);
        Assert.True(result.Data.Result.TotalTrades >= 0);
        Assert.NotEmpty(result.Data.Result.EquityCurve);

        // Verify trades endpoint
        var tradesResponse = await _client.GetAsync($"/api/backtests/{result.Data.Id}/trades");
        Assert.Equal(HttpStatusCode.OK, tradesResponse.StatusCode);
        var tradesResult = await tradesResponse.Content.ReadFromJsonAsync<ApiResponse<List<BacktestTradeDto>>>();
        Assert.NotNull(tradesResult);
        Assert.True(tradesResult.Success);
    }
}
