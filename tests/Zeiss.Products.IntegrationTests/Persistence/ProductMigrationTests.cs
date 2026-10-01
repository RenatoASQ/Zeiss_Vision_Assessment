using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Zeiss.Products.IntegrationTests.Persistence;

public class ProductMigrationTests : IAsyncLifetime
{
    private const string SchemaBeforeDescription = "20260928151246_AddStockAdjustment";

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _database.DropAsync();

    [Fact]
    public void Model_HasNoChangesMissingFromTheMigrations()
    {
        using var context = _database.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Migrate_FromTheSchemaBeforeDescription_KeepsExistingProductsWithoutDescription()
    {
        await using var context = _database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(SchemaBeforeDescription);

        // The current model already has Description, so the old rows are inserted with SQL.
        await context.Database.ExecuteSqlRawAsync("""
            DECLARE @id int = NEXT VALUE FOR [dbo].[ProductIds];
            INSERT INTO [dbo].[Products] ([Id], [Name], [Stock], [CreatedAtUtc])
            VALUES (@id, N'Existing lenses', 7, SYSUTCDATETIME());
            INSERT INTO [dbo].[StockMovements] ([ProductId], [Type], [Quantity], [StockBefore], [StockAfter], [OccurredAtUtc])
            VALUES (@id, N'Increment', 7, 0, 7, SYSUTCDATETIME());
            """);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        var product = await context.Products.AsNoTracking().Include(value => value.StockMovements).SingleAsync();
        Assert.Equal("Existing lenses", product.Name);
        Assert.Equal(7, product.Stock);
        Assert.Null(product.Description);
        Assert.Equal(7, Assert.Single(product.StockMovements).StockAfter);
    }
}
