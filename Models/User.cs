// ✅ User model
using System.ComponentModel.DataAnnotations.Schema;

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
}