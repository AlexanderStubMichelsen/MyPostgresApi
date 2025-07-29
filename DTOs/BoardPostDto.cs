namespace MyPostgresApi.DTOs
{
    public class BoardPostDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));

        public int UserId { get; set; }  // ✅ Brugerens ID
        public UserDto? UserDto { get; set; } // ✅ Hele brugerens data
    }
}
