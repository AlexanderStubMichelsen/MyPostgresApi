using System.ComponentModel.DataAnnotations.Schema;
using MyPostgresApi.DTOs;

namespace MyPostgresApi.Models;

public class User
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    private string? _password;

    [Column("password")]
    public string? Password
    {
        get => _password;
        set => _password = value;
    }

    // Navigation property for related BoardPosts
    public List<BoardPost> BoardPosts { get; set; } = new();

    // Mapping method to convert User to UserDto
    public UserDto ToDto()
    {
        return new UserDto
        {
            Id = this.Id,
            Name = this.Name,
            Email = this.Email
        };
    }

    // Convert a list of Users to a list of UserDtos
    public static List<UserDto> ToDtos(List<User> users)
    {
        return users.Select(user => user.ToDto()).ToList();
    }
}
