using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.DTOs.Watchlists;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BistQuant.IntegrationTests;

public class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _anonymousClient;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _anonymousClient = factory.CreateClient();
    }

    private async Task<(HttpClient client, long userId, string token)> CreateAuthenticatedClientAsync(string role = "User")
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"user_{Guid.NewGuid():N}@test.com";
        var regRequest = new RegisterRequest(uniqueEmail, "Pass12345!", "Test User");

        var regResp = await client.PostAsJsonAsync("/api/auth/register", regRequest);
        var regData = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        var token = regData!.Data!.Token;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, regData.Data.UserId, token);
    }

    [Theory]
    [InlineData("/api/paper-portfolios")]
    [InlineData("/api/alerts")]
    [InlineData("/api/watchlists")]
    [InlineData("/api/auth/me")]
    public async Task AnonymousRequests_ToProtectedEndpoints_MustReturn401Unauthorized(string endpoint)
    {
        var response = await _anonymousClient.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithStandardUserToken_MustReturn403Forbidden()
    {
        var (userClient, _, _) = await CreateAuthenticatedClientAsync("User");
        var content = new MultipartFormDataContent();
        var response = await userClient.PostAsync("/api/admin/import/market-data", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Watchlist_CrossUserAccess_MustReturn404NotFound()
    {
        var (userAClient, _, _) = await CreateAuthenticatedClientAsync();
        var (userBClient, _, _) = await CreateAuthenticatedClientAsync();

        // User A creates a watchlist
        var createResp = await userAClient.PostAsJsonAsync("/api/watchlists", new CreateWatchlistRequest("User A Watchlist"));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<WatchlistDto>>();
        var watchlistId = created!.Data!.Id;

        // User B attempts to access User A's watchlist
        var accessResp = await userBClient.GetAsync($"/api/watchlists/{watchlistId}");
        Assert.Equal(HttpStatusCode.NotFound, accessResp.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUser_CanAccessOwnWatchlistAndPortfolio()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync();

        var portResp = await client.GetAsync("/api/paper-portfolios");
        Assert.Equal(HttpStatusCode.OK, portResp.StatusCode);

        var watchResp = await client.GetAsync("/api/watchlists");
        Assert.Equal(HttpStatusCode.OK, watchResp.StatusCode);
    }
}
