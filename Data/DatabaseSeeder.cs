using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

public static class DatabaseSeeder
{
    public static void SeedData(AppDbContext context)
    {
        if (!context.Items.Any()) // Prevent duplicate seeding
        {
            context.Items.AddRange(new[]
            {
                new Item { Name = "Item 1" },
                new Item { Name = "Item 2" },
                new Item { Name = "Item 3" }
            });

            context.SaveChanges();
        }
    }
}
