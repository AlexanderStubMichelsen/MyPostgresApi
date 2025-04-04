// ✅ AppDbContext
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

public class AppDbContext : DbContext
{
    private readonly string _schema;

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration config) : base(options)
    {
        // Retrieve schema from configuration or use default
        _schema = config["DB_SCHEMA"] ?? "maskinen";
        Console.WriteLine($"🏗 Using schema: {_schema}");
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set default schema
        modelBuilder.HasDefaultSchema(_schema);

        // Configure the Users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users"); // Map to "users" table
            entity.HasKey(u => u.Id); // Set primary key
            entity.Property(u => u.Name).IsRequired().HasMaxLength(100); // Configure Name column
            entity.Property(u => u.Email).IsRequired().HasMaxLength(100); // Configure Email column
            entity.HasIndex(u => u.Email).IsUnique(); // Ensure Email is unique
            entity.Property(u => u.Password).IsRequired(); // Configure Password column
        });
    }
}