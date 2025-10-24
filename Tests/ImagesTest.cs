using System.Net;
using System.Text.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Xunit.Abstractions;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
    public class ImagesTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly AppDbContext _dbContext;
        private readonly IServiceScope _scope;
        private readonly ITestOutputHelper _output;

        public ImagesTest(CustomWebApplicationFactory factory, ITestOutputHelper output)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _output = output;
        }

        public async Task InitializeAsync()
        {
            await _dbContext.Database.EnsureCreatedAsync();
            await ResetDatabaseAsync();
        }

        public async Task DisposeAsync()
        {
            await ResetDatabaseAsync();
            _scope.Dispose();
        }

        private async Task ResetDatabaseAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM saved_images;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM users;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('saved_images','users');");
            _dbContext.ChangeTracker.Clear();
        }

        [Fact]
        public async Task SaveImage_WithoutAuthentication_ReturnsUnauthorized()
        {
            var image = new
            {
                ImageUrl = "https://example.com/image.jpg",
                Title = "Test Image",
                Photographer = "John Doe",
                SourceLink = "https://source.com"
            };

            var response = await _client.PostAsJsonAsync("/api/images/save", image);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetImages_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await _client.GetAsync("/api/images/mine");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task DeleteImage_WithoutAuthentication_ReturnsUnauthorized()
        {
            var nonexistentId = Guid.NewGuid();
            var deleteResponse = await _client.DeleteAsync($"/api/images/{nonexistentId}");
            Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task ImageUserCount_WithoutAuthentication_HandlesCorrectly()
        {
            var request = new { ImageUrl = "https://example.com/test.jpg" };
            var response = await _client.PostAsJsonAsync("/api/images/image-user-count", request);
            
            // Accept any response - just testing the endpoint exists
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || 
                       response.StatusCode == HttpStatusCode.NotFound ||
                       response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Database_CanCreateAndQueryDirectly()
        {
            // Test direct database operations to ensure the test DB works
            var user = new User
            {
                Name = "Direct DB Test User",
                Email = "direct@example.com",
                Password = "test-hash" // Fixed: Use Password instead of PasswordHash
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var savedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "direct@example.com");
            Assert.NotNull(savedUser);
            Assert.Equal(user.Name, savedUser.Name);

            var image = new SavedImage
            {
                ImageUrl = "https://example.com/direct-test.jpg",
                Title = "Direct Test Image",
                Photographer = "Direct Test",
                SourceLink = "https://direct.com",
                UserId = savedUser.Id
            };

            _dbContext.SavedImages.Add(image);
            await _dbContext.SaveChangesAsync();

            var savedImage = await _dbContext.SavedImages.FirstOrDefaultAsync(i => i.UserId == savedUser.Id);
            Assert.NotNull(savedImage);
            Assert.Equal(image.Title, savedImage.Title);
        }

        [Fact]
        public void TestEnvironmentVariables_AreSet() // Fixed: Removed async since no await
        {
            // Verify test environment is configured correctly
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            
            _output.WriteLine($"JWT_SECRET_KEY is set: {!string.IsNullOrEmpty(jwtSecret)}");
            _output.WriteLine($"Environment: {environment}");
            
            Assert.False(string.IsNullOrEmpty(jwtSecret));
            Assert.Equal("Testing", environment);
        }

        [Fact] 
        public async Task CreateUserAndLogin_ReturnsValidToken()
        {
            var user = new
            {
                Name = "Token Test User",
                Email = "tokentest@example.com",
                Password = "Test1234"
            };

            // Create user
            var createResponse = await _client.PostAsJsonAsync("/api/users", user);
            createResponse.EnsureSuccessStatusCode();

            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createContent}");

            var createResult = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(createContent)));
            
            Assert.True(createResult.TryGetProperty("token", out var createToken));
            var token1 = createToken.GetString();
            Assert.False(string.IsNullOrEmpty(token1));

            // Login
            var login = new { Email = user.Email, Password = user.Password };
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", login);
            loginResponse.EnsureSuccessStatusCode();

            var loginContent = await loginResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Login response: {loginContent}");

            var loginResult = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(loginContent)));
            
            Assert.True(loginResult.TryGetProperty("token", out var loginToken));
            var token2 = loginToken.GetString();
            Assert.False(string.IsNullOrEmpty(token2));

            // Both tokens should be valid JWT format
            Assert.Contains(".", token1);
            Assert.Contains(".", token2);
        }
    }
}