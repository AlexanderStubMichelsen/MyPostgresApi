using System;

namespace MyPostgresApi.Models
{
    public class SavedImage
    {
        public int Id { get; set; }                    // Primary key
        public int UserId { get; set; }                // FK to Users table
        public string ImageUrl { get; set; } = "";
        public string Title { get; set; } = "";
        public string Photographer { get; set; } = "";
        public string SourceLink { get; set; } = "";
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        // Optional: Navigation property if you want relationships
        public User? User { get; set; }
    }
}
