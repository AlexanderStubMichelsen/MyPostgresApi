using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var projectDir = Directory.GetCurrentDirectory();
        builder.UseContentRoot(projectDir);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var testDbPath = Path.Combine(Path.GetTempPath(), "mypostgresapi-tests.db");
            Environment.SetEnvironmentVariable("SQLITE_PATH", testDbPath);

            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrWhiteSpace(jwtSecret))
            {
                Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "insecure-test-secret-change-me");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={testDbPath}"));

            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });
    }
}
