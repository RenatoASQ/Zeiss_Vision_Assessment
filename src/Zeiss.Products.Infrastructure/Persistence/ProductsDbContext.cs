using Microsoft.EntityFrameworkCore;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Infrastructure.Persistence.Configurations;

namespace Zeiss.Products.Infrastructure.Persistence;

public class ProductsDbContext : DbContext
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("ProductIds", "dbo")
            .StartsAt(100000)
            .IncrementsBy(1)
            .HasMin(100000)
            .HasMax(999999)
            .IsCyclic(false);

        // Each Configure method defines how an entity maps to the database.
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new StockMovementConfiguration());
    }
}
