using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace MyPostgresApi.Tests
{
    public class ProgramTest : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProgramTest(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

        [Fact]
        public async Task GetUsers_ReturnsSuccessStatusCode()
        {
            var response = await _client.GetAsync("/api/users");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task PostUser_CreatesUser()
        {
            var newUser = new
            {
                Name = "Test User",
                Email = "testuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            var createdUser = await response.Content.ReadFromJsonAsync<object>();
            Assert.NotNull(createdUser);
        }

        [Fact]
        public async Task LoginUser_ReturnsSuccess()
        {
            var loginRequest = new
            {
                Email = "testuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<object>();
            Assert.NotNull(loginResponse);
        }
    }
}