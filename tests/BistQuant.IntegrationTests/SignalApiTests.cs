using System.Net;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Signals;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class SignalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SignalApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Signals_ShouldReturnScoreReasonsAndRiskLevels()
    {
        // Act
        var response = await _client.GetAsync("/api/analysis/THYAO/signals");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SignalDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("THYAO", result.Data.Symbol);
        Assert.True(result.Data.Score >= 0 && result.Data.Score <= 100);
        Assert.False(string.IsNullOrEmpty(result.Data.Signal));
        Assert.NotNull(result.Data.Risk);
        Assert.True(result.Data.Risk.StopLoss < result.Data.Price);
        Assert.True(result.Data.Risk.TakeProfit1 > result.Data.Price);
        Assert.NotEmpty(result.Data.Reasons);
    }
}
