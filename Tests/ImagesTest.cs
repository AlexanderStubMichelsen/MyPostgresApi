using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
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
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");

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
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");
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

        private async Task<int> GetUsersWithImagesCountAsync()
        {
            return await _dbContext.SavedImages
                .Select(img => img.UserId)
                .Distinct()
                .CountAsync();
        }

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

            var imagetwo = new
            {
                ImageUrl = "https://example.com/image2.jpg",
                Title = "Test Image 2",
                Photographer = "Jane Doe",
                SourceLink = "https://source2.com"
            };

            var saveResponse = await _client.PostAsJsonAsync("/api/images/save", image);
            saveResponse.EnsureSuccessStatusCode();

            var saveResponseTwo = await _client.PostAsJsonAsync("/api/images/save", imagetwo);
            saveResponseTwo.EnsureSuccessStatusCode();

            var getResponse = await _client.GetAsync("/api/images/mine");
            getResponse.EnsureSuccessStatusCode();

            var images = await getResponse.Content.ReadFromJsonAsync<List<SavedImage>>();

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            Assert.NotNull(images);
            Assert.Equal(2, images.Count);

            Assert.Equal(image.Title, images[0].Title);
            Assert.Equal(image.ImageUrl, images[0].ImageUrl);
            Assert.Equal(image.Photographer, images[0].Photographer);
            Assert.Equal(image.SourceLink, images[0].SourceLink);

            Assert.Equal(imagetwo.Title, images[1].Title);
            Assert.Equal(imagetwo.ImageUrl, images[1].ImageUrl);
            Assert.Equal(imagetwo.Photographer, images[1].Photographer);
            Assert.Equal(imagetwo.SourceLink, images[1].SourceLink);


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
            Assert.Equal(image.Photographer, images[0].Photographer);
            Assert.Equal(image.SourceLink, images[0].SourceLink);
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

            // Save image
            var saveResponse = await _client.PostAsJsonAsync("/api/images/save", image);
            saveResponse.EnsureSuccessStatusCode();

            // Create image (DO NOT include UserId)
            var image2 = new
            {
                ImageUrl = "https://example.com/image2.jpg",
                Title = "Test Image 2",
                Photographer = "Jane Doe",
                SourceLink = "https://source2.com"
            };

            // Save image
            var saveResponseTwo = await _client.PostAsJsonAsync("/api/images/save", image2);
            saveResponseTwo.EnsureSuccessStatusCode();

            // Fetch images to get both IDs
            var getResponseImages = await _client.GetAsync("/api/images/mine");
            getResponseImages.EnsureSuccessStatusCode();

            var images = await getResponseImages.Content.ReadFromJsonAsync<List<SavedImage>>();
            Assert.NotNull(images);
            Assert.Equal(2, images.Count);

            // Get the images Id's
            var imageFirstId = images![0].Id;
            var imageSecondId = images![1].Id;

            // ✅ Delete using the imageId
            var deleteResponse = await _client.DeleteAsync($"/api/images/{imageFirstId}");
            deleteResponse.EnsureSuccessStatusCode();

            // Check if the image was deleted
            var confirm = await _client.GetAsync("/api/images/mine");
            var remaining = await confirm.Content.ReadFromJsonAsync<List<SavedImage>>();

            // Assert that the first image was deleted
            Assert.Single(remaining!);
            Assert.Equal(imageSecondId, remaining![0].Id);
            Assert.Equal(image2.Title, remaining![0].Title);
            Assert.Equal(image2.ImageUrl, remaining![0].ImageUrl);
            Assert.Equal(image2.Photographer, remaining![0].Photographer);
            Assert.Equal(image2.SourceLink, remaining![0].SourceLink);

            // Delete the second image
            var deleteResponseTwo = await _client.DeleteAsync($"/api/images/{imageSecondId}");
            deleteResponseTwo.EnsureSuccessStatusCode();

            // Check if the second image was deleted    
            var confirmSecond = await _client.GetAsync("/api/images/mine");
            var remainingSecond = await confirmSecond.Content.ReadFromJsonAsync<List<SavedImage>>();

            // Assert that there is no remaining images
            Assert.Empty(remainingSecond!);
        }

        [Fact]
        public async Task GetUsersCountForSpecificImage()
        {
            AddAuthHeader();

            // Create second user
            var user2 = new
            {
                Name = "Image Tester2",
                Email = "image2@example.com",
                Password = "Test12342"
            };

            var userResponse = await _client.PostAsJsonAsync("/api/users", user2);
            userResponse.EnsureSuccessStatusCode();

            // First user saves an image
            var sharedImage = new
            {
                ImageUrl = "https://example.com/shared-image.jpg",
                Title = "Shared Image",
                Photographer = "John Doe",
                SourceLink = "https://source.com"
            };

            await _client.PostAsJsonAsync("/api/images/save", sharedImage);

            // Second user logs in and saves the same image
            var token2 = await GetJwtTokenAsync(user2.Email, user2.Password);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

            await _client.PostAsJsonAsync("/api/images/save", sharedImage);

            // Test the endpoint to count users for this specific image
            var encodedUrl = Uri.EscapeDataString(sharedImage.ImageUrl);
            var countResponse = await _client.GetAsync($"/api/images/image-user-count/{encodedUrl}");
            countResponse.EnsureSuccessStatusCode();

            var userCount = await countResponse.Content.ReadFromJsonAsync<int>();
            Assert.Equal(2, userCount);
        }
    }
}