using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace MyPostgresApi.Tests
{
    public class ProgramTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly AppDbContext _dbContext;

        public ProgramTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        // ✅ This runs before each test
        public async Task InitializeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");
        }

        // ✅ This runs after each test
        public async Task DisposeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");
            _scope.Dispose();
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
                Name = "Unique Test User",
                Email = "uniqueuser@example.com",
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
            var testUser = new User
            {
                Name = "Login Tester",
                Email = "logintest@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("TestPassword123")
            };

            _dbContext.Users.Add(testUser);
            await _dbContext.SaveChangesAsync();

            var loginRequest = new
            {
                testUser.Email,
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<object>();
            Assert.NotNull(loginResponse);
        }
    }
}
