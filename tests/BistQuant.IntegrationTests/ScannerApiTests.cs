using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.DTOs.Scanner;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class ScannerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ScannerApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"scanuser_{Guid.NewGuid():N}@test.com";
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(uniqueEmail, "Pass12345!", "Scan User"));
        var regData = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regData!.Data!.Token);
        return client;
    }

    [Fact]
    public async Task Get_Scanner_ShouldReturnRankedResults()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner?timeframe=Daily&pageSize=15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ScannerItemDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Items);
        Assert.All(result.Data.Items, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.Symbol));
            Assert.True(item.Price > 0);
            Assert.True(item.Score >= 0 && item.Score <= 100);
            Assert.False(string.IsNullOrEmpty(item.Signal));
        });
    }

    [Fact]
    public async Task Get_MarketOverview_ShouldReturnAggregateStatistics()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/overview");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MarketOverviewDto>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.TotalSymbols > 0);
        Assert.True(result.Data.AverageMarketScore > 0);
        Assert.NotEmpty(result.Data.TopSignals);
    }

    [Fact]
    public async Task Post_ScannerRun_ShouldCompleteScan()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsync("/api/scanner/run?timeframe=Daily", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }
}
