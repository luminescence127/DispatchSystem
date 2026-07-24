using Microsoft.AspNetCore.Mvc.Testing;

namespace DispatchSystem.Api.Tests
{
    public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task HealthEndpoint_ReturnsHealthy()
        {
            var client = factory.CreateClient();

            var res = await client.GetAsync("/health");

            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync();

            Assert.Equal("Healthy", body);
        }
    }
}

