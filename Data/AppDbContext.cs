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
        // ✅ Use custom schema for all tables
        modelBuilder.HasDefaultSchema(_schema);

        // ✅ Ensure EF migration history also uses this schema
        modelBuilder.HasAnnotation("Relational:HistoryTableSchema", _schema);

        // ✅ Configure BoardPost entity
        modelBuilder.Entity<BoardPost>(entity =>
        {
            entity.ToTable("board_posts");
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
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.BoardPosts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ✅ Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Password).HasColumnName("password").IsRequired();
        });

        // ✅ Configure SavedImage entity
        modelBuilder.Entity<SavedImage>(entity =>
        {
            entity.ToTable("saved_images");
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
