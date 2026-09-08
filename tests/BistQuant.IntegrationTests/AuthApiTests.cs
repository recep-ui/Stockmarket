using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_ShouldReturnValidJwtToken()
    {
        var uniqueEmail = $"trader_{Guid.NewGuid():N}@bistquant.com";
        var registerRequest = new RegisterRequest(
            Email: uniqueEmail,
            Password: "SecurePassword123!",
            DisplayName: "Quant Algo Trader"
        );

        // 1. Register
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var regResult = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(regResult?.Data);
        Assert.NotEmpty(regResult.Data.Token);
        Assert.Equal(uniqueEmail, regResult.Data.Email);

        // 2. Login
        var loginRequest = new LoginRequest(
            Email: uniqueEmail,
            Password: "SecurePassword123!"
        );

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(loginResult?.Data);
        Assert.NotEmpty(loginResult.Data.Token);

        // 3. Query Me with Bearer token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Data.Token);
        var meResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meResult = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileDto>>();
        Assert.NotNull(meResult?.Data);
        Assert.Equal(uniqueEmail, meResult.Data.Email);
        Assert.Equal("Quant Algo Trader", meResult.Data.DisplayName);
    }
}
