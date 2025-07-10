using System.ComponentModel.DataAnnotations.Schema;
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Use UTC

    public BoardPostDto ToDto()
    {
        return new BoardPostDto
        {
            Id = this.Id,
            Name = this.Name,
            Message = this.Message
        };
    }

    public static List<BoardPostDto> ToDtos(List<BoardPost> boardPosts)
    {
        return [.. boardPosts.Select(boardPost => boardPost.ToDto())];
    }
}