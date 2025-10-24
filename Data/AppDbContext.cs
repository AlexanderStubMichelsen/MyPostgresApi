using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<User> Users { get; set; }
    public DbSet<SavedImage> SavedImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {


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
