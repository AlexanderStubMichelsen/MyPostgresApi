using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;

namespace MyPostgresApi.Tests
{
    public class UsersTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly AppDbContext _dbContext;

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
        }

        // ✅ This runs after each test
        public async Task DisposeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");
            _scope.Dispose();
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
        public async Task GetUsers_ReturnsSuccessStatusCode()
        {
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
                testUser.Email,
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            Assert.Equal(updatedUser.Name, updatedUserResponse.GetProperty("name").GetString());
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Equal(updatedUser.Email, updatedUserResponse.GetProperty("email").GetString());
        }

        [Fact]
        public async Task ChangePassword()
        {
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
    }
}
