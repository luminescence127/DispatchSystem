using Microsoft.AspNetCore.Mvc.Testing;

namespace DispatchSystem.Api.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/health");

        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal("Healthy", body);
    }
}
