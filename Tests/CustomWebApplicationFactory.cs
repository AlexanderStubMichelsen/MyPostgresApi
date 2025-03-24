using Microsoft.AspNetCore.Hosting;
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
        // 👇 This sets the correct path to the actual project
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
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            var testConn = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(testConn))
                throw new Exception("❌ TEST_DB_CONNECTION is missing or empty!");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(testConn));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            db.Database.ExecuteSqlRaw("TRUNCATE TABLE test_schema.users RESTART IDENTITY CASCADE");
        });
    }
}
