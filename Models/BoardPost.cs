using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using MyPostgresApi.DTOs;

namespace MyPostgresApi.Models;

public class BoardPost
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key to User
    [Column("user_id")]
    public int UserId { get; set; }

    // Navigation property
    [JsonIgnore] // Prevents circular reference during serialization
    public User? User { get; set; }

    public BoardPostDto ToDto()
    {
        return new BoardPostDto
        {
            Id = this.Id,
            Name = this.Name,
            Message = this.Message,
            CreatedAt = this.CreatedAt,
            UserId = this.UserId,
            UserDto = this.User == null ? null : new UserDto
            {
                Id = this.User.Id,
                Name = this.User.Name,
                Email = this.User.Email
            }
        };
    }

    public static List<BoardPostDto> ToDtos(List<BoardPost> boardPosts)
    {
        return boardPosts.Select(bp => bp.ToDto()).ToList();
    }
}
