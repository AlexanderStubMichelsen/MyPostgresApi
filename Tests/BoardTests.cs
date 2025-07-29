using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;
using Xunit;

namespace MyPostgresApi.Tests
{
    [Collection("NonParallelCollection")]
    public class BoardPostsTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly AppDbContext _dbContext;
        private readonly IServiceScope _scope;

        public BoardPostsTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        public async Task InitializeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.board_posts RESTART IDENTITY CASCADE");
        }

        public async Task DisposeAsync()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.board_posts RESTART IDENTITY CASCADE");
            _scope.Dispose();
        }

        [Fact]
        public async Task PostBoardPost_CreatesNewPost()
        {
            var post = new { Name = "Tester", Message = "Hello world!" };

            var response = await _client.PostAsJsonAsync("/api/boardposts", post);
            response.EnsureSuccessStatusCode();

            var created = await response.Content.ReadFromJsonAsync<BoardPost>();

            Assert.NotNull(created);
            Assert.Equal(post.Name, created!.Name);
            Assert.Equal(post.Message, created.Message);
        }

        [Fact]
        public async Task GetBoardPosts_ReturnsList()
        {
            var post = new BoardPost { Name = "Test", Message = "Message", CreatedAt = DateTime.UtcNow };
            _dbContext.BoardPosts.Add(post);
            await _dbContext.SaveChangesAsync();

            var response = await _client.GetAsync("/api/boardposts");
            response.EnsureSuccessStatusCode();

            var posts = await response.Content.ReadFromJsonAsync<List<BoardPost>>();
            Assert.NotNull(posts);
            Assert.Single(posts);
        }

        [Fact]
        public async Task GetBoardPost_ReturnsCorrectItem()
        {
            var post = new BoardPost { Name = "One", Message = "Post", CreatedAt = DateTime.UtcNow };
            _dbContext.BoardPosts.Add(post);
            await _dbContext.SaveChangesAsync();

            var response = await _client.GetAsync($"/api/boardposts/{post.Id}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BoardPost>();
            Assert.NotNull(result);
            Assert.Equal(post.Name, result!.Name);
        }

        [Fact]
        public async Task UpdateBoardPost_ChangesData()
        {
            var post = new BoardPost { Name = "Old", Message = "Message", CreatedAt = DateTime.UtcNow };
            _dbContext.BoardPosts.Add(post);
            await _dbContext.SaveChangesAsync();

            var updated = new BoardPost { Id = post.Id, Name = "New", Message = "Updated" }; // ✅ no CreatedAt
            var response = await _client.PutAsJsonAsync($"/api/boardposts/{post.Id}", updated);

            response.EnsureSuccessStatusCode(); // Optional: clearer failure message

            var check = await _dbContext.BoardPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == post.Id); // ✅ use AsNoTracking
            Assert.Equal("New", check!.Name);
        }


        [Fact]
        public async Task DeleteBoardPost_RemovesEntry()
        {
            var post = new BoardPost { Name = "Delete", Message = "Me", CreatedAt = DateTime.UtcNow };
            _dbContext.BoardPosts.Add(post);
            await _dbContext.SaveChangesAsync();

            var response = await _client.DeleteAsync($"/api/boardposts/{post.Id}");
            response.EnsureSuccessStatusCode();

            // Use AsNoTracking and FirstOrDefaultAsync instead of FindAsync
            var check = await _dbContext.BoardPosts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == post.Id);

            Assert.Null(check);
        }
    }
}