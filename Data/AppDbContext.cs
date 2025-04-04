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
    public DbSet<SavedImage> SavedImages { get; set; } // ✅ Add SavedImages table

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ✅ Use configured schema
        modelBuilder.HasDefaultSchema(_schema);

        // ✅ Users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Password).IsRequired();
        });

        // ✅ SavedImages table
        modelBuilder.Entity<SavedImage>(entity =>
        {
            entity.ToTable("saved_images"); // Table name
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ImageUrl).IsRequired();
            entity.Property(i => i.Title).HasMaxLength(200);
            entity.Property(i => i.Photographer).HasMaxLength(100);
            entity.Property(i => i.SourceLink).HasMaxLength(300);
            entity.Property(i => i.SavedAt).IsRequired();

            // Optional: foreign key constraint (comment out if not using full navigation)
            entity.HasOne(i => i.User)
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
