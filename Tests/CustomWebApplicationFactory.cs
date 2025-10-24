using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MyPostgresApi.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private string _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add test database
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={_testDbPath}"));

                // Set test environment variables
                Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-secret-key-for-testing-purposes-only");
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            });

            builder.UseEnvironment("Testing");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up test database
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            base.Dispose(disposing);
        }
    }

    [CollectionDefinition("NonParallelCollection", DisableParallelization = true)]
    public class NonParallelCollectionDefinition { }
}
