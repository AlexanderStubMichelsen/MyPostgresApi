namespace MyPostgresApi.DTOs
{
    public class BoardPostDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)); // UTC+2
        
    }
}