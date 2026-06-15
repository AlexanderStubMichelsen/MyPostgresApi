using System.Net;
using Xunit;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
    public class CorsTest : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public CorsTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Preflight_FromMachinemalOrigin_ReturnsCorsHeaders()
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/users");
            request.Headers.Add("Origin", "https://machinemal.eu");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type");

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
            Assert.Equal("https://machinemal.eu", Assert.Single(origins));
        }
    }
}
