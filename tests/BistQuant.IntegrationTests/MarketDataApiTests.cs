using System.Net;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.MarketData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class MarketDataApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MarketDataApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Symbols_ShouldReturnSeededBistSymbols()
    {
        // Act
        var response = await _client.GetAsync("/api/symbols");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SymbolDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Data!);
        Assert.Contains(result.Data!, s => s.Ticker == "THYAO");
        Assert.Contains(result.Data!, s => s.Ticker == "ASELS");
    }

    [Fact]
    public async Task Get_Symbol_ByTicker_ShouldReturnDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/symbols/THYAO");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SymbolDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("THYAO", result.Data!.Ticker);
        Assert.Equal("Turk Hava Yollari", result.Data.Name);
    }

    [Fact]
    public async Task Get_HistoricalBars_ShouldReturnPriceSeries()
    {
        // Act
        var response = await _client.GetAsync("/api/market-data/THYAO/history?timeframe=Daily");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<PriceBarDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Data!);
        Assert.All(result.Data!, bar =>
        {
            Assert.True(bar.High >= bar.Low);
            Assert.True(bar.Open > 0);
            Assert.True(bar.Close > 0);
            Assert.True(bar.Volume >= 0);
        });
    }
}
