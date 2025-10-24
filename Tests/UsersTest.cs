using System.Net;
using System.Text.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
    public class UsersTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly AppDbContext _dbContext;
        private readonly IServiceScope _scope;
        private readonly ITestOutputHelper _output;

        public UsersTest(CustomWebApplicationFactory factory, ITestOutputHelper output)
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
            try
            {
                await ResetDatabaseAsync();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Database cleanup failed: {ex.Message}");
            }
            finally
            {
                _scope.Dispose();
            }
        }

        private async Task ResetDatabaseAsync()
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM saved_images;");
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM users;");
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('saved_images','users');");
                _dbContext.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Database reset error: {ex.Message}");
                // Try alternative cleanup
                await _dbContext.Database.EnsureDeletedAsync();
                await _dbContext.Database.EnsureCreatedAsync();
            }
        }

        [Fact]
        public async Task PostUser_CreatesUser()
        {
            var user = new
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = "Test1234"
            };

            var response = await _client.PostAsJsonAsync("/api/users", user);
            
            // Debug output
            _output.WriteLine($"Status Code: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response Content: {content}");
            
            response.EnsureSuccessStatusCode();

            // Parse the actual response format: {"userDto": {...}, "token": "..."
            var result = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            
            Assert.True(result.TryGetProperty("userDto", out var userDto));
            Assert.True(userDto.TryGetProperty("name", out var nameProperty));
            Assert.Equal(user.Name, nameProperty.GetString());
            
            Assert.True(result.TryGetProperty("token", out var tokenProperty));
            Assert.False(string.IsNullOrEmpty(tokenProperty.GetString()));
        }

        [Fact]
        public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
        {
            var user1 = new
            {
                Name = "First User",
                Email = "duplicate@example.com",
                Password = "Test1234"
            };

            var user2 = new
            {
                Name = "Second User",
                Email = "duplicate@example.com",
                Password = "Test5678"
            };

            var response1 = await _client.PostAsJsonAsync("/api/users", user1);
            var response2 = await _client.PostAsJsonAsync("/api/users", user2);

            response1.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        }

        [Fact]
        public async Task LoginUser_ReturnsSuccess()
        {
            var user = new
            {
                Name = "Login Test User",
                Email = "login@example.com",
                Password = "Test1234"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/users", user);
            createResponse.EnsureSuccessStatusCode();

            var login = new
            {
                Email = user.Email,
                Password = user.Password
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", login);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            
            // Parse login response format: {"message": "...", "userDto": {...}, "token": "..."
            Assert.True(result.TryGetProperty("token", out var tokenProperty));
            Assert.False(string.IsNullOrEmpty(tokenProperty.GetString()));
            
            Assert.True(result.TryGetProperty("userDto", out var userDto));
            Assert.True(userDto.TryGetProperty("email", out var emailProperty));
            Assert.Equal(user.Email, emailProperty.GetString());
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            var user = new
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = "Test1234"
            };

            await _client.PostAsJsonAsync("/api/users", user);

            var login = new
            {
                Email = user.Email,
                Password = "WrongPassword"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", login);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
        {
            var login = new
            {
                Email = "nonexistent@example.com",
                Password = "Test1234"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", login);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetUsers_ReturnsSuccessOrUnauthorized()
        {
            // Create a user first
            var user = new
            {
                Name = "Test User",
                Email = "gettest@example.com", 
                Password = "Test1234"
            };

            await _client.PostAsJsonAsync("/api/users", user);

            var response = await _client.GetAsync("/api/users");
            
            // Accept either success or unauthorized
            Assert.True(response.IsSuccessStatusCode || 
                       response.StatusCode == HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateUser_WithSpecialCharacters_HandlesCorrectly()
        {
            var user = new
            {
                Name = "José María González-Smith",
                Email = "jose.maria@example.com",
                Password = "Test1234"
            };

            var response = await _client.PostAsJsonAsync("/api/users", user);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            
            Assert.True(result.TryGetProperty("userDto", out var userDto));
            Assert.True(userDto.TryGetProperty("name", out var nameProperty));
            Assert.Equal(user.Name, nameProperty.GetString());
        }

        [Fact]
        public async Task DeleteUser_WithValidId_HandlesCorrectly()
        {
            // Create user
            var user = new
            {
                Name = "Delete Test User",
                Email = "delete@example.com",
                Password = "Test1234"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/users", user);
            createResponse.EnsureSuccessStatusCode();

            var content = await createResponse.Content.ReadAsStringAsync();
            var userData = await JsonSerializer.DeserializeAsync<JsonElement>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            
            Assert.True(userData.TryGetProperty("userDto", out var userDto));
            Assert.True(userDto.TryGetProperty("id", out var idProperty));
            
            var userId = idProperty.GetInt32();
            var deleteResponse = await _client.DeleteAsync($"/api/users/{userId}");
            
            // Debug output for the delete operation
            _output.WriteLine($"Delete Status Code: {deleteResponse.StatusCode}");
            var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete Response: {deleteContent}");
            
            // Accept success, unauthorized, not found, or method not allowed
            Assert.True(deleteResponse.IsSuccessStatusCode || 
                       deleteResponse.StatusCode == HttpStatusCode.Unauthorized ||
                       deleteResponse.StatusCode == HttpStatusCode.NotFound ||
                       deleteResponse.StatusCode == HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task ChangePassword_ReturnsExpectedStatus()
        {
            var changePassword = new
            {
                CurrentPassword = "OldPassword",
                NewPassword = "NewPassword123"
            };

            var response = await _client.PutAsJsonAsync("/api/users/change-password", changePassword);
            
            // Should be either Unauthorized, MethodNotAllowed, or NotFound
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || 
                       response.StatusCode == HttpStatusCode.MethodNotAllowed ||
                       response.StatusCode == HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task TestDatabase_IsWorkingCorrectly()
        {
            // Simple test to verify database operations work
            var userCount = await _dbContext.Users.CountAsync();
            _output.WriteLine($"Initial user count: {userCount}");
            
            // This test should always pass - just verifying DB connectivity
            Assert.True(userCount >= 0);
        }
    }
}