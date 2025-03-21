using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.test");
            DotNetEnv.Env.Load(envPath);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
            Console.WriteLine("🧪 TEST_DB_CONNECTION = " + connectionString);

            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("❌ TEST_DB_CONNECTION is missing or empty!");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate(); // Optional
            }
        });
    }
}
