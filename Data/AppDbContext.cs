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
    public DbSet<SavedImage> SavedImages { get; set; }
    public DbSet<BoardPost> BoardPosts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ✅ Use configured schema
        modelBuilder.HasDefaultSchema(_schema);

        // ✅ Configure BoardPost entity properly
        modelBuilder.Entity<BoardPost>(entity =>
        {
            entity.ToTable("board_posts"); // Will use default schema
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasConversion(
                      v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                      v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                  );
        });

        // ✅ Users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users"); // Use default schema, not hardcoded "maskinen"
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Password).HasColumnName("password").IsRequired();
        });

        // ✅ SavedImages table
        modelBuilder.Entity<SavedImage>(entity =>
        {
            entity.ToTable("saved_images"); // Use default schema, not hardcoded "maskinen"
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasColumnName("id");
            entity.Property(i => i.UserId).HasColumnName("user_id");
            entity.Property(i => i.ImageUrl).HasColumnName("image_url");
            entity.Property(i => i.Title).HasColumnName("title");
            entity.Property(i => i.Photographer).HasColumnName("photographer");
            entity.Property(i => i.SourceLink).HasColumnName("source_link");
            entity.Property(i => i.SavedAt).HasColumnName("saved_at");
        });
    }
}
