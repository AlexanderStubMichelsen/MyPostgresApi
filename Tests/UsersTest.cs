using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;
// Ensure the Models namespace exists in the project or remove this line if unnecessary
using MyPostgresApi.Models;
using System.Text.Json;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
    public class UsersTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly AppDbContext _dbContext;
        private string? _token;

        public UsersTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        // ✅ This runs before each test
        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync();

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
            await ResetDatabaseAsync();
            _scope.Dispose();
        }

        private async Task ResetDatabaseAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM board_posts;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM saved_images;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM users;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('board_posts','saved_images','users');");
            _dbContext.ChangeTracker.Clear();
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
            var userDto = createdUser.GetProperty("userDto");
            Assert.Equal(newUser.Name, userDto.GetProperty("name").GetString());
            Assert.Equal(newUser.Email, userDto.GetProperty("email").GetString());
            Assert.True(userDto.GetProperty("id").GetInt32() > 0);
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

            var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            var userDto = responseJson.GetProperty("userDto");

            var newUser2 = new
            {
                Name = "Unique Test User2",
                Email = "uniqueuser2@example.com",
                Password = "TestPassword123"
            };

            var response2 = await _client.PostAsJsonAsync("/api/users", newUser2);
            response2.EnsureSuccessStatusCode();

            var response2Json = await response2.Content.ReadFromJsonAsync<JsonElement>();
            var userDto2 = response2Json.GetProperty("userDto");

            Console.WriteLine(await response.Content.ReadAsStringAsync());
            Console.WriteLine(await response2.Content.ReadAsStringAsync());

            var responseGet = await _client.GetAsync("/api/users");
            responseGet.EnsureSuccessStatusCode();

            var users = await responseGet.Content.ReadFromJsonAsync<List<UserResponse>>();

            // 3 because we have the one we created for the token
            Assert.NotNull(users);
            Assert.Equal(3, users!.Count());

            Assert.Equal(newUser.Name, users[1].Name);
            Assert.Equal(newUser.Email, users[1].Email);
            Assert.True(userDto.GetProperty("id").GetInt32() > 0);
            Assert.Equal(newUser.Name, userDto.GetProperty("name").GetString());
            Assert.Equal(newUser.Email, userDto.GetProperty("email").GetString());

            Assert.Equal(newUser2.Name, users[2].Name);
            Assert.Equal(newUser2.Email, users[2].Email);
            Assert.True(userDto.GetProperty("id").GetInt32() > 0);
            Assert.Equal(newUser2.Name, userDto2.GetProperty("name").GetString());
            Assert.Equal(newUser2.Email, userDto2.GetProperty("email").GetString());
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

            var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();

            var userDto = responseJson.GetProperty("userDto");

            var token = responseJson.GetProperty("token").GetString();

            var message = responseJson.GetProperty("message").GetString();

            Assert.Equal("Login successful!", message);

            Assert.True(userDto.GetProperty("id").GetInt32() > 0);
            Assert.Equal(testUser.Name, userDto.GetProperty("name").GetString());
            Assert.Equal(testUser.Email, userDto.GetProperty("email").GetString());

            Assert.NotNull(token); // Ensure the token is not null
        }

        [Fact]
        public async Task UpdateUser_ReturnsSuccess()
        {
            AddAuthorizationHeader();

            var updatedUser = new
            {
                Name = "Updated Test User",
                Email = "testuser@example.com",
                Password = "TestPassword123"
            };

            var updateResponse = await _client.PutAsJsonAsync("/api/users/update", updatedUser);
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
            var changePasswordResponse = await _client.PutAsJsonAsync("/api/users/changepassword", changePasswordRequest);
            changePasswordResponse.EnsureSuccessStatusCode();

            // Check message from response
            var changePasswordResponseJson = await changePasswordResponse.Content.ReadFromJsonAsync<JsonElement>();
            var message = changePasswordResponseJson.GetProperty("message").GetString();
            Assert.Equal("Password updated successfully!", message);

            Assert.NotNull(changePasswordResponse);
            Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteUser_ReturnsSuccess()
        {
            AddAuthorizationHeader();

            var newUser = new
            {
                Name = "Unique Test User",
                Email = "email@test.user",
                Password = "TestPassword123"
            };
            var response = await _client.PostAsJsonAsync("/api/users", newUser);
            response.EnsureSuccessStatusCode();

            // Use HttpRequestMessage to send a body with DELETE
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/users/delete")
            {
                Content = JsonContent.Create(new
                {
                    Email = "email@test.user",
                    Password = "TestPassword123"
                })
            };

            var deleteResponse = await _client.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();

            var deleteResponseJson = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
            var deleteMessage = deleteResponseJson.GetProperty("message").GetString();
            Assert.Equal("User deleted successfully", deleteMessage);
            Assert.NotNull(deleteResponse);
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        }
    }
}