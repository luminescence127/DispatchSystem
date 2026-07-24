using Microsoft.AspNetCore.Mvc.Testing;

namespace DispatchSystem.Api.Tests
{
    public class AboutEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task AboutEndpoint_ReturnsAppName()
        {
            var client = factory.CreateClient();

            var res = await client.GetAsync("/about");

            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync();

            Assert.Contains("DispatchSystem", body);//body 裡包含 DispatchSystem 這個字串
        }
    }
}
