using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var createdUser = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(newUser.Name, createdUser.GetProperty("name").GetString());
            Assert.Equal(newUser.Email, createdUser.GetProperty("email").GetString());
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

            var newUser = new
            {
                Name = "Unique Test User",
                Email = "uniqueuser@example.com",
                Password = "TestPassword123"
            };

            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            var newUser2 = new
            {
                Name = "Unique Test User2",
                Email = "uniqueuser2@example.com",
                Password = "TestPassword123"
            };

            var response2 = await _client.PostAsJsonAsync("/api/users", newUser2);
            response2.EnsureSuccessStatusCode();

            Console.WriteLine(await response.Content.ReadAsStringAsync());
            Console.WriteLine(await response2.Content.ReadAsStringAsync());

            var responseGet = await _client.GetAsync("/api/users");
            responseGet.EnsureSuccessStatusCode();

            var users = await responseGet.Content.ReadFromJsonAsync<List<UserResponse>>();

            // 3 because we have the one we created for the token
            Assert.NotNull(users);
            Assert.Equal(3, users!.Count());
            Assert.Equal(newUser.Email, users[1].Email);
            Assert.Equal(newUser2.Name, users.Last().Name);

        }

        public class LoginResponse
        {
            public string? Token { get; set; }
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

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginResponse);
            Assert.NotNull(loginResponse!.Token); // Ensure the token is not null
        }

        [Fact]
        public async Task UpdateUser_ReturnsSuccess()
        {
            AddAuthorizationHeader();

            // Create a new user
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
                Email = "uniqueuser@example.com", // Keep the same email to identify the user
                Password = "UpdatedPassword"
            };

            var updateResponse = await _client.PutAsJsonAsync($"/api/users/{newUser.Email}", updatedUser);
            updateResponse.EnsureSuccessStatusCode();

            // Fetch the updated user
            var getUserResponse = await _client.GetAsync("/api/users");
            getUserResponse.EnsureSuccessStatusCode();

            var users = await getUserResponse.Content.ReadFromJsonAsync<List<UserResponse>>();
            Assert.NotNull(users);

            // Verify the updated user
            var updatedUserResponse = users.FirstOrDefault(u => u.Email == updatedUser.Email);
            Assert.NotNull(updatedUserResponse);
            Assert.Equal(updatedUser.Name, updatedUserResponse!.Name);
            Assert.Equal(updatedUser.Email, updatedUserResponse.Email);

            // Debug output for inspection
            Console.WriteLine($"Updated User: Name={updatedUserResponse.Name}, Email={updatedUserResponse.Email}");
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
    }
}