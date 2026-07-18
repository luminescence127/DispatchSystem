using Microsoft.AspNetCore.Mvc.Testing;
using System;
namespace DispatchSystem.Api.Tests
{
    public class AboutEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AboutEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AboutEndpoint_ReturnsAppName()
        {
            var client = _factory.CreateClient();

            var res = await client.GetAsync("/about");

            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync();

            Assert.Contains("DispatchSystem", body);//body 裡包含 DispatchSystem 這個字串
        }
    }
}
