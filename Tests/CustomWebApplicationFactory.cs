using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Set the content root to the project directory
        var projectDir = Directory.GetCurrentDirectory();
        builder.UseContentRoot(projectDir);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_SCHEMA"] = "test_schema"
            });

            configBuilder.AddEnvironmentVariables();
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing AppDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Read test database connection string
            var testConn = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(testConn))
                throw new Exception("❌ TEST_DB_CONNECTION is missing or empty!");

            // Add test AppDbContext
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(testConn));

            // Build service provider and initialize database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // Log actual DB connection
                Console.WriteLine($"🔌 DB Connection: {db.Database.GetDbConnection().ConnectionString}");

                // Force EF to create schema and tables from model
                db.Database.EnsureCreated();

                // Log tables registered by EF Core
                var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Distinct();
                Console.WriteLine("📦 Tables registered with EF:");
                foreach (var t in tables)
                    Console.WriteLine($" - {t}");

                // Test table existence manually
                try
                {
                    db.Database.ExecuteSqlRaw("SELECT 1 FROM test_schema.saved_images LIMIT 1;");
                    Console.WriteLine("🧪 Table test_schema.saved_images exists ✅");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Table access failed: {ex.Message}");
                }

                // Truncate test tables
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.saved_images RESTART IDENTITY CASCADE");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing test schema: {ex.Message}");
                throw;
            }
        });
    }
}
