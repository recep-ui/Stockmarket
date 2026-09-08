using System.Net;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Indicators;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class AnalysisApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AnalysisApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_TechnicalSnapshot_ShouldReturnCalculatedIndicators()
    {
        // Act
        var response = await _client.GetAsync("/api/analysis/THYAO/technical");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IndicatorSnapshotDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("THYAO", result.Data.Symbol);
        Assert.NotNull(result.Data.EMA20);
        Assert.NotNull(result.Data.EMA50);
        Assert.NotNull(result.Data.RSI14);
        Assert.NotNull(result.Data.ATR14);
        Assert.NotNull(result.Data.BollingerUpper);
        Assert.NotNull(result.Data.BollingerLower);
    }
}
