using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;
using System.Net.Http.Json;

namespace MyPostgresApi.Tests
{
    public class UsersTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly AppDbContext _dbContext;
        private string _token;

        public UsersTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        // ✅ This runs before each test
        public async Task InitializeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");

            // Create a test user
            var newUser = new
            {
                Name = "Test User",
                Email = "testuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            // Retrieve a JWT token
            _token = await GetJwtTokenAsync();
        }

        // ✅ This runs after each test
        public async Task DisposeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");
            _scope.Dispose();
        }

        private async Task<string> GetJwtTokenAsync()
        {
            var loginRequest = new
            {
                Email = "testuser@example.com",
                Password = "TestPassword123"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();

            var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginData?.Token == null)
            {
                throw new InvalidOperationException("Login response did not contain a valid token.");
            }
            return loginData.Token;
        }

        private void AddAuthorizationHeader()
        {
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        [Fact]
        public async Task PostUser_CreatesUser()
        {
            AddAuthorizationHeader();

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
        public async Task GetUsers_ReturnsSuccessStatusCode()
        {
            AddAuthorizationHeader();

            var response = await _client.GetAsync("/api/users");
            response.EnsureSuccessStatusCode();
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
                Email = "logintest@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<object>();
            Assert.NotNull(loginResponse);
        }

        [Fact]
        public async Task UpdateUser_ReturnsSuccess()
        {
            AddAuthorizationHeader();

            var newUser = new
            {
                Name = "Unique Test User",
                Email = "uniqueuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            // Update the user we just created
            var updatedUser = new
            {
                Name = "Updated Test User",
                Email = "uniqueuserupdated@example.com",
                Password = "UpdatedPassword"
            };

            var updateResponse = await _client.PutAsJsonAsync("/api/users/uniqueuser@example.com", updatedUser);
            updateResponse.EnsureSuccessStatusCode();

            // Verify the update by fetching the user again
            var getUserResponse = await _client.GetAsync("/api/users");
            getUserResponse.EnsureSuccessStatusCode();

            var users = await getUserResponse.Content.ReadFromJsonAsync<IEnumerable<dynamic>>();
            var updatedUserResponse = users.FirstOrDefault(u => u.GetProperty("email").GetString() == updatedUser.Email);

            Assert.NotNull(updatedUserResponse);
            Assert.Equal(updatedUser.Name, updatedUserResponse.GetProperty("name").GetString());
        }

        [Fact]
        public async Task ChangePassword()
        {
            AddAuthorizationHeader();

            var newUser = new
            {
                Name = "Unique Test User",
                Email = "uniqueuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            // Change the password of the user we just created
            var changePasswordRequest = new
            {
                OldPassword = "TestPassword123",
                NewPassword = "NewTestPassword123"
            };
            var changePasswordResponse = await _client.PutAsJsonAsync("/api/users/uniqueuser@example.com/changepassword", changePasswordRequest);
            changePasswordResponse.EnsureSuccessStatusCode();

            Assert.NotNull(changePasswordResponse);
            Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);
        }

        public class LoginResponse
        {
            public string? Token { get; set; }
        }
    }
}