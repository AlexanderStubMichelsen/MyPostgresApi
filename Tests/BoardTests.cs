using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.DTOs;
using MyPostgresApi.Models;
using Xunit;

namespace MyPostgresApi.Tests;

[Collection("NonParallelCollection")]
public class BoardPostsTest : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScope _scope;
    private string _jwtToken = "";
    private int _userId;

    public BoardPostsTest(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.board_posts RESTART IDENTITY CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");

        var user = new User
        {
            Name = "TestUser",
            Email = "test@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("test1234")
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _userId = user.Id;

        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Email = "test@example.com",
            Password = "test1234"
        });

        // 🔥 Fix: Read raw JSON into a dictionary of <string, object>
        var tokenJson = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _jwtToken = tokenJson!["token"]!.ToString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
    }


    public async Task DisposeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.board_posts RESTART IDENTITY CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");
        _scope.Dispose();
    }

    [Fact]
    public async Task PostBoardPost_CreatesNewPost()
    {
        var post = new { Name = "Tester", Message = "Hello world!" };

        var response = await _client.PostAsJsonAsync("/api/boardposts", post);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<BoardPostDto>();
        Assert.NotNull(created);
        Assert.Equal(post.Name, created!.Name);
        Assert.Equal(post.Message, created.Message);
        Assert.Equal(_userId, created.UserId);
        Assert.NotNull(created.UserDto);
        Assert.Equal("TestUser", created.UserDto!.Name);
    }

    [Fact]
    public async Task GetBoardPosts_ReturnsList()
    {
        _dbContext.BoardPosts.Add(new BoardPost
        {
            Name = "Test",
            Message = "Message",
            UserId = _userId,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var response = await _client.GetAsync("/api/boardposts/mine");
        response.EnsureSuccessStatusCode();

        var posts = await response.Content.ReadFromJsonAsync<List<BoardPostDto>>();
        Assert.NotNull(posts);
        Assert.Single(posts);
        Assert.Equal(_userId, posts![0].UserId);
        Assert.NotNull(posts[0].UserDto);
    }

    [Fact]
    public async Task UpdateBoardPost_ChangesData()
    {
        var post = new BoardPost
        {
            Name = "Old",
            Message = "Message",
            UserId = _userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.BoardPosts.Add(post);
        await _dbContext.SaveChangesAsync();

        var updated = new BoardPostDto
        {
            Id = post.Id,
            Name = "New",
            Message = "Updated"
        };

        var response = await _client.PutAsJsonAsync($"/api/boardposts/{post.Id}", updated);
        response.EnsureSuccessStatusCode();

        var check = await _dbContext.BoardPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == post.Id);
        Assert.Equal("New", check!.Name);
    }

    [Fact]
    public async Task DeleteBoardPost_RemovesEntry()
    {
        var post = new BoardPost
        {
            Name = "Delete",
            Message = "Me",
            UserId = _userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.BoardPosts.Add(post);
        await _dbContext.SaveChangesAsync();

        var response = await _client.DeleteAsync($"/api/boardposts/{post.Id}");
        response.EnsureSuccessStatusCode();

        var check = await _dbContext.BoardPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == post.Id);

        Assert.Null(check);
    }
}
