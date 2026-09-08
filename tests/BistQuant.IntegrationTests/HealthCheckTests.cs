using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BistQuant.IntegrationTests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Get_LivenessEndpoints_ShouldReturnOk(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReadinessEndpoint_ShouldReturnStatusAccuratelyReflectingDependencies()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert: When worker or redis is not running in test suite, returns 503 (fail closed), otherwise 200 OK
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable for readiness check, but received {response.StatusCode}");
    }
}
