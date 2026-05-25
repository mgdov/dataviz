using DataViz.Api.Data;
using DataViz.Api.Infrastructure;
using DataViz.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext ctx, ILogger logger)
    {
        if (await ctx.Categories.AnyAsync())
        {
            logger.LogInformation("Seed skipped: categories already exist");
            return;
        }

        logger.LogInformation("Seeding initial data...");

        var categories = new[]
        {
            new Category { Name = "Электроника", Description = "Смартфоны, ноутбуки, планшеты" },
            new Category { Name = "Бытовая техника", Description = "Холодильники, стиральные машины" },
            new Category { Name = "Одежда", Description = "Мужская и женская одежда" },
            new Category { Name = "Книги", Description = "Художественная и техническая литература" },
            new Category { Name = "Спорт", Description = "Спортивные товары и инвентарь" },
        };
        ctx.Categories.AddRange(categories);
        await ctx.SaveChangesAsync();

        var regions = new[] { "MSK", "SPB", "NSK", "EKB", "KZN", "RND" };
        var rnd = new Random(42);

        var products = new List<Product>();
        var productSeeds = new (string name, string region, decimal price, int stock, int catIdx)[]
        {
            ("Ноутбук ProBook 14",        "MSK", 75000, 50, 0),
            ("Смартфон Aurora X",         "SPB", 35000, 120, 0),
            ("Планшет TabLite 10",        "NSK", 22000, 80, 0),
            ("Беспроводные наушники",     "MSK", 6500, 200, 0),
            ("Умные часы FitBand",        "EKB", 8900, 150, 0),
            ("Холодильник Frost-300",     "MSK", 42000, 30, 1),
            ("Стиральная машина WashPro", "SPB", 28000, 40, 1),
            ("Микроволновая печь",        "KZN", 7500, 90, 1),
            ("Пылесос CleanMaster",       "NSK", 15000, 60, 1),
            ("Куртка зимняя",             "MSK", 9500, 200, 2),
            ("Джинсы классические",       "SPB", 3500, 300, 2),
            ("Кроссовки Runner Pro",      "EKB", 6800, 180, 2),
            ("Футболка хлопок",           "KZN", 1500, 500, 2),
            ("Книга «Чистый код»",        "MSK", 1800, 100, 3),
            ("Книга «Алгоритмы»",         "SPB", 2400, 80, 3),
            ("Художественная литература", "RND", 850, 250, 3),
            ("Велосипед горный",          "EKB", 25000, 35, 4),
            ("Гантели 10 кг",             "NSK", 4500, 70, 4),
            ("Коврик для йоги",           "MSK", 1200, 200, 4),
            ("Мяч футбольный",            "KZN", 2200, 150, 4),
        };

        foreach (var (name, region, price, stock, catIdx) in productSeeds)
        {
            products.Add(new Product
            {
                Name = name,
                Description = $"{name} — описание товара",
                Price = price,
                StockQuantity = stock,
                RegionCode = region,
                CategoryId = categories[catIdx].Id,
            });
        }
        ctx.Products.AddRange(products);
        await ctx.SaveChangesAsync();

        // Create a demo user
        var demoUser = new User
        {
            Name = "Demo",
            Email = "demo@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo12345"),
            Role = UserRoles.Admin,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.Add(demoUser);
        await ctx.SaveChangesAsync();

        // Generate ~1500 orders across the last 365 days
        var startDate = DateTime.UtcNow.Date.AddDays(-365);
        var orders = new List<Order>();
        for (int day = 0; day < 365; day++)
        {
            var date = startDate.AddDays(day);
            // 2-6 orders per day with weekend boost
            var perDay = rnd.Next(2, date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 8 : 6);
            for (int i = 0; i < perDay; i++)
            {
                var region = regions[rnd.Next(regions.Length)];
                var order = new Order
                {
                    UserId = demoUser.Id,
                    CreatedAt = date.AddHours(rnd.Next(9, 22)).AddMinutes(rnd.Next(0, 60)),
                    RegionCode = region,
                };

                var itemCount = rnd.Next(1, 5);
                decimal total = 0;
                var pickedIds = new HashSet<int>();
                for (int k = 0; k < itemCount; k++)
                {
                    Product p;
                    do { p = products[rnd.Next(products.Count)]; }
                    while (!pickedIds.Add(p.Id));

                    var qty = rnd.Next(1, 4);
                    order.Items.Add(new OrderItem
                    {
                        ProductId = p.Id,
                        Quantity = qty,
                        UnitPrice = p.Price,
                    });
                    total += p.Price * qty;
                }
                order.TotalPrice = total;
                orders.Add(order);
            }
        }
        ctx.Orders.AddRange(orders);
        await ctx.SaveChangesAsync();

        logger.LogInformation("Seed complete: {Cats} categories, {Prods} products, {Orders} orders",
            categories.Length, products.Count, orders.Count);
    }
}
