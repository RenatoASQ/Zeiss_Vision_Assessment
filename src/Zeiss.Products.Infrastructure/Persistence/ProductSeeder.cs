using Microsoft.EntityFrameworkCore;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;

namespace Zeiss.Products.Infrastructure.Persistence;

internal static class ProductSeeder
{
    public static void Seed(ProductsDbContext context)
    {
        // Include deleted products so setup never restores a catalog the user has cleared.
        if (context.Products.Any())
        {
            return;
        }

        context.Products.AddRange(CreateProducts());
        context.SaveChanges();
    }

    public static async Task SeedAsync(ProductsDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Products.AddRange(CreateProducts());
        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<Product> CreateProducts()
    {
        var createdAtUtc = DateTime.UtcNow;
        var products = new List<Product>
        {
            new() { Name = "Daily contact lenses", Stock = 25, CreatedAtUtc = createdAtUtc },
            new() { Name = "Reading glasses", Stock = 10, CreatedAtUtc = createdAtUtc },
            new() { Name = "Lens cleaning spray", Stock = 0, CreatedAtUtc = createdAtUtc }
        };

        foreach (var product in products)
        {
            if (product.Stock > 0)
            {
                product.StockMovements.Add(new StockMovement
                {
                    Type = StockMovementType.Increment,
                    Quantity = product.Stock,
                    StockBefore = 0,
                    StockAfter = product.Stock,
                    OccurredAtUtc = createdAtUtc
                });
            }
        }

        return products;
    }
}
