using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;
// Ensure the Models namespace exists in the project or remove this line if unnecessary
using MyPostgresApi.Models;
using System.Text.Json;

namespace MyPostgresApi.Tests
{
    public class UsersTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly AppDbContext _dbContext;
        private string? _token;
        private const string TestUserEmail = "testuser@example.com";

        public UsersTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        // ✅ This runs before all tests
        public async Task InitializeAsync()
        {
            // Explicitly specify the schema if required
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");

            // Create a single test user
            var newUser = new
            {
                Name = "Test User",
                Email = TestUserEmail,
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            // Retrieve a JWT token
            _token = await GetJwtTokenAsync();
        }

        // ✅ This runs after all tests
        public async Task DisposeAsync()
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "testuser@example.com"); // Corrected email syntax
            if (user != null)
            {
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
            }

            _scope.Dispose();
        }

        private async Task<string> GetJwtTokenAsync()
        {
            var loginRequest = new
            {
                Email = "testuser@example.com", // Corrected email syntax
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

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var createdUser = await response.Content.ReadFromJsonAsync<JsonElement>();
            var userDto = createdUser.GetProperty("userDto");
            Assert.Equal(newUser.Name, userDto.GetProperty("name").GetString());
            Assert.Equal(newUser.Email, userDto.GetProperty("email").GetString());
        }

        public class UserResponse
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
        }

        [Fact]
        public async Task GetUsers_ReturnsSuccessStatusCode()
        {
            AddAuthorizationHeader();

            var responseGet = await _client.GetAsync("/api/users");
            responseGet.EnsureSuccessStatusCode();
            var users = await responseGet.Content.ReadFromJsonAsync<List<UserResponse>>();

            // Adjust the expected user count to match the actual number of users
            Assert.NotNull(users);
            Assert.Single(users); // Expect only 1 user
            Assert.Equal(TestUserEmail, users[0].Email);
            Assert.Equal("Test User", users[0].Name);
        }
        

        public class LoginResponse
        {
            public string? Token { get; set; }
        }

        [Fact]
        public async Task LoginUser_ReturnsSuccess()
        {
            var loginRequest = new
            {
                Email = TestUserEmail,
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginResponse);
            Assert.NotNull(loginResponse!.Token); // Ensure the token is not null
        }

        [Fact]
        public async Task UpdateUser_ReturnsSuccess()
        {
            AddAuthorizationHeader();

            // Update the user we just created
            var updatedUser = new
            {
                Name = "Updated Test User",
                Email = TestUserEmail, // Keep the same email to identify the user
                Password = "TestPassword123"
            };

            var updateResponse = await _client.PutAsJsonAsync($"/api/users/{TestUserEmail}", updatedUser);
            updateResponse.EnsureSuccessStatusCode();

            // Fetch the updated user
            var getUserResponse = await _client.GetAsync("/api/users");
            getUserResponse.EnsureSuccessStatusCode();

            var users = await getUserResponse.Content.ReadFromJsonAsync<List<User>>();
            Assert.NotNull(users);

            // Convert to DTOs using the ToDtos method in the User class
            var userDtos = User.ToDtos(users);
            // Verify the updated user
            var updatedUserResponse = userDtos.FirstOrDefault(u => u.Email == updatedUser.Email);
            Assert.NotNull(updatedUserResponse);
            Assert.Equal(updatedUser.Name, updatedUserResponse!.Name);
            Assert.Equal(updatedUser.Email, updatedUserResponse.Email);
        }

        [Fact]
        public async Task ChangePassword()
        {
            AddAuthorizationHeader();

            // Change the password of the user we just created
            var changePasswordRequest = new
            {
                OldPassword = "TestPassword123",
                NewPassword = "NewTestPassword123"
            };
            var changePasswordResponse = await _client.PutAsJsonAsync($"/api/users/{TestUserEmail}/changepassword", changePasswordRequest);
            changePasswordResponse.EnsureSuccessStatusCode();

            Assert.NotNull(changePasswordResponse);
            Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);
        }
    }
}