using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;
using Zeiss.Products.Infrastructure;
using Zeiss.Products.Infrastructure.Persistence;

namespace Zeiss.Products.IntegrationTests.Persistence;

public class ProductSeedingTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private readonly ServiceProvider _services;

    public ProductSeedingTests()
    {
        _services = new ServiceCollection().AddInfrastructure(_database.ConnectionString).BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DropAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrate_OnNewDatabase_SeedsProductsWithGeneratedIdsAndInitialHistory(bool asynchronous)
    {
        await MigrateAsync(asynchronous);

        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        var products = await context.Products.AsNoTracking().Include(product => product.StockMovements).ToListAsync();

        Assert.Equal(3, products.Count);
        Assert.Equal(3, products.Select(product => product.Id).Distinct().Count());
        Assert.Equal(25, products.Single(product => product.Name == "Daily contact lenses").Stock);
        Assert.Equal(10, products.Single(product => product.Name == "Reading glasses").Stock);
        Assert.Equal(0, products.Single(product => product.Name == "Lens cleaning spray").Stock);
        Assert.All(products, product =>
        {
            Assert.InRange(product.Id, 100000, 999999);
            Assert.Equal(8, product.RowVersion.Length);
            Assert.Null(product.DeletedAtUtc);
            Assert.Equal(DateTimeKind.Utc, product.CreatedAtUtc.Kind);
            if (product.Stock == 0)
            {
                Assert.Empty(product.StockMovements);
            }
            else
            {
                var movement = Assert.Single(product.StockMovements);
                Assert.Equal(StockMovementType.Increment, movement.Type);
                Assert.Equal(product.Stock, movement.Quantity);
                Assert.Equal(0, movement.StockBefore);
                Assert.Equal(product.Stock, movement.StockAfter);
                Assert.Equal(product.Id, movement.ProductId);
                Assert.Equal(product.CreatedAtUtc, movement.OccurredAtUtc);
            }
        });

        var next = new Product { Name = "Created after setup", CreatedAtUtc = DateTime.UtcNow };
        context.Products.Add(next);
        await context.SaveChangesAsync();
        Assert.InRange(next.Id, 100000, 999999);
        Assert.DoesNotContain(products, product => product.Id == next.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrate_WhenRepeated_PreservesEditsAndDoesNotRestoreDeletedProducts(bool asynchronous)
    {
        await MigrateAsync(asynchronous);
        int[] originalIds;
        await using (var scope = _services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
            var products = await context.Products.OrderBy(product => product.Id).ToListAsync();
            originalIds = products.Select(product => product.Id).ToArray();
            products[0].Name = "Renamed by the user";
            foreach (var product in products)
            {
                product.DeletedAtUtc = DateTime.UtcNow;
            }
            await context.SaveChangesAsync();
        }

        await MigrateAsync(asynchronous);

        await using var verificationScope = _services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        var saved = await verification.Products.AsNoTracking().OrderBy(product => product.Id).ToListAsync();
        Assert.Equal(originalIds, saved.Select(product => product.Id));
        Assert.Equal("Renamed by the user", saved[0].Name);
        Assert.All(saved, product => Assert.NotNull(product.DeletedAtUtc));
        Assert.Equal(2, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Migrate_WithExistingCatalog_LeavesItUnchanged()
    {
        // Prepare a database that already has user data, without running the seeding callbacks.
        await using (var setup = _database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            setup.Products.Add(new Product { Name = "Existing product", CreatedAtUtc = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        await MigrateAsync(true);

        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        Assert.Equal("Existing product", (await context.Products.SingleAsync()).Name);
        Assert.Empty(await context.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task Migrate_FromConcurrentContexts_DoesNotDuplicateSeedData()
    {
        await using (var setup = _database.CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        await Task.WhenAll(MigrateAsync(true), MigrateAsync(true));

        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        Assert.Equal(3, await context.Products.CountAsync());
        Assert.Equal(2, await context.StockMovements.CountAsync());
    }

    private async Task MigrateAsync(bool asynchronous)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        if (asynchronous)
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            context.Database.Migrate();
        }
    }
}
