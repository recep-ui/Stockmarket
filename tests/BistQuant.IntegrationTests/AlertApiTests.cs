using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Alerts;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class AlertApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AlertApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"alertuser_{Guid.NewGuid():N}@test.com";
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(uniqueEmail, "Pass12345!", "Alert User"));
        var regData = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regData!.Data!.Token);
        return client;
    }

    [Fact]
    public async Task Post_And_Get_AlertSubscription_ShouldSucceed()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Create alert subscription
        var createRequest = new CreateAlertRequest(
            Symbol: "THYAO",
            AlertType: "ScoreThreshold",
            Condition: ">=",
            Value: 80,
            Channel: NotificationChannel.InApp
        );

        var postResponse = await client.PostAsJsonAsync("/api/alerts", createRequest);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<ApiResponse<AlertSubscriptionDto>>();
        Assert.NotNull(created);
        Assert.True(created.Success);
        Assert.NotNull(created.Data);
        Assert.Equal("THYAO", created.Data.Symbol);
        Assert.Equal(80m, created.Data.Value);

        // 2. Query subscriptions
        var getResponse = await client.GetAsync("/api/alerts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var list = await getResponse.Content.ReadFromJsonAsync<ApiResponse<List<AlertSubscriptionDto>>>();
        Assert.NotNull(list);
        Assert.True(list.Success);
        Assert.Contains(list.Data!, a => a.Id == created.Data.Id);

        // 3. Delete subscription
        var deleteResponse = await client.DeleteAsync($"/api/alerts/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Get_Notifications_ShouldReturnList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/alerts/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<NotificationDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }
}
