using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

namespace MyPostgresApi.Tests
{
    public class ImagesTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly AppDbContext _dbContext;
        private readonly IServiceScope _scope;
        private string? _token;

        public ImagesTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        public async Task InitializeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.saved_images RESTART IDENTITY CASCADE");
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");

            var user = new
            {
                Name = "Image Tester",
                Email = "image@example.com",
                Password = "Test1234"
            };

            var response = await _client.PostAsJsonAsync("/api/users", user);
            response.EnsureSuccessStatusCode();

            _token = await GetJwtTokenAsync(user.Email, user.Password);
        }

        public async Task DisposeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.saved_images RESTART IDENTITY CASCADE");
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE");
            _scope.Dispose();
        }

        private async Task<string> GetJwtTokenAsync(string email, string password)
        {
            var login = new { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/users/login", login);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return data?.Token ?? throw new Exception("No token received.");
        }

        private void AddAuthHeader() =>
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        public class LoginResponse { public string? Token { get; set; } }

        [Fact]
        public async Task SaveImage()
        {
            AddAuthHeader();

            var image = new
            {
                ImageUrl = "https://example.com/image.jpg",
                Title = "Test Image",
                Photographer = "John Doe",
                SourceLink = "https://source.com"
            };

            var saveResponse = await _client.PostAsJsonAsync("/api/images/save", image);
            saveResponse.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        }

        [Fact]
        public async Task GetImage()
        {
            AddAuthHeader();

            var image = new
            {
                ImageUrl = "https://example.com/image.jpg",
                Title = "Test Image",
                Photographer = "John Doe",
                SourceLink = "https://source.com"
            };

            await _client.PostAsJsonAsync("/api/images/save", image);

            var getResponse = await _client.GetAsync("/api/images/mine");
            getResponse.EnsureSuccessStatusCode();

            var images = await getResponse.Content.ReadFromJsonAsync<List<SavedImage>>();
            Assert.NotNull(images);
            Assert.Single(images);
            Assert.Equal(image.Title, images[0].Title);
            Assert.Equal(image.ImageUrl, images[0].ImageUrl);
        }

        [Fact]
public async Task DeleteImage()
{
    // Create user
    var user = new
    {
        Name = "Image Tester2",
        Email = "image2@example.com",
        Password = "Test12342"
    };

    var userResponse = await _client.PostAsJsonAsync("/api/users", user);
    userResponse.EnsureSuccessStatusCode();

    var userResult = await userResponse.Content.ReadFromJsonAsync<JsonElement>();
    var email = user.Email;
    var password = user.Password;

    // Login and set token
    _token = await GetJwtTokenAsync(email, password);
    AddAuthHeader();

    // Create image (DO NOT include UserId)
    var image = new
    {
        ImageUrl = "https://example.com/image.jpg",
        Title = "Delete Me",
        Photographer = "Jane Doe",
        SourceLink = "https://source.com"
    };

    var saveResponse = await _client.PostAsJsonAsync("/api/images/save", image);
    saveResponse.EnsureSuccessStatusCode();

    // Fetch image to get ID
    var getResponse = await _client.GetAsync("/api/images/mine");
    getResponse.EnsureSuccessStatusCode();

    var images = await getResponse.Content.ReadFromJsonAsync<List<SavedImage>>();
    Assert.NotNull(images);
    var imageId = images!.First().Id;

    // ✅ Delete using the imageId
    var deleteResponse = await _client.DeleteAsync($"/api/images/{imageId}");
    deleteResponse.EnsureSuccessStatusCode();

    // Confirm it's deleted
    var confirm = await _client.GetAsync("/api/images/mine");
    var remaining = await confirm.Content.ReadFromJsonAsync<List<SavedImage>>();
    Assert.Empty(remaining!);
}
    }
}   