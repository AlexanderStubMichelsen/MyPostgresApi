// ✅ AppDbContext
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

public class AppDbContext : DbContext
{
    private readonly string _schema;

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration config) : base(options)
    {
        _schema = config["DB_SCHEMA"] ?? "maskinen";
        Console.WriteLine($"🏗 Using schema: {_schema}");
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.Entity<User>().ToTable("users");
    }
}
