using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

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

            configBuilder.AddJsonFile("appsettings.Test.json", optional: true);
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
                var verbose = Environment.GetEnvironmentVariable("VERBOSE_TEST_LOGS") == "true";

                if (verbose)
                    Console.WriteLine($"🔌 DB Connection: {db.Database.GetDbConnection().ConnectionString}");

                // Force EF to create schema and tables from model
                db.Database.EnsureCreated();

                if (verbose)
                {
                    var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Distinct();
                    Console.WriteLine("📦 Tables registered with EF:");
                    foreach (var t in tables)
                        Console.WriteLine($" - {t}");
                }

                // Validate required tables
                string[] expectedTables = { "users", "saved_images", "board_posts" };
                foreach (var table in expectedTables)
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw($"SELECT 1 FROM test_schema.{table} LIMIT 1;");
                        if (verbose)
                            Console.WriteLine($"✅ Table test_schema.{table} exists");
                    }
                    catch
                    {
                        Console.WriteLine($"❌ Missing: test_schema.{table}");
                    }
                }

                // Truncate test tables
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.saved_images RESTART IDENTITY CASCADE");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.board_posts RESTART IDENTITY CASCADE");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing test schema: {ex.Message}");
                throw;
            }
        });
    }
}
