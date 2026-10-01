using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;
using Zeiss.Products.Domain.Exceptions;
using Zeiss.Products.Infrastructure.Persistence;
using Zeiss.Products.Infrastructure.Repositories;

namespace Zeiss.Products.IntegrationTests.Persistence;

public class ProductsDbContextTests : IAsyncLifetime
{
    // Every test owns a new database, including its sequence, and deletes it afterwards.
    private readonly TestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _database.DropAsync();

    [Fact]
    public async Task SaveChanges_WithProductAndMovement_GeneratesIdsAndPersistsTheRelationship()
    {
        var product = NewProduct("Contact lenses");
        product.Stock = 20;
        product.StockMovements.Add(new StockMovement
        {
            Type = StockMovementType.Increment,
            Quantity = 20,
            StockBefore = 0,
            StockAfter = 20,
            OccurredAtUtc = product.CreatedAtUtc
        });

        await using (var context = CreateContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var saved = await verification.Products.AsNoTracking()
            .Include(value => value.StockMovements).SingleAsync();
        var movement = Assert.Single(saved.StockMovements);
        Assert.InRange(saved.Id, 100000, 999999);
        Assert.Equal(8, saved.RowVersion.Length);
        Assert.Equal(20, saved.Stock);
        Assert.True(movement.Id > 0);
        Assert.Equal(saved.Id, movement.ProductId);
        Assert.Equal(StockMovementType.Increment, movement.Type);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAtUtc.Kind);
        Assert.Equal(saved.CreatedAtUtc, movement.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, movement.OccurredAtUtc.Kind);
    }

    [Fact]
    public async Task SaveChanges_FromIndependentConcurrentContexts_GeneratesDistinctSixDigitIds()
    {
        var tasks = Enumerable.Range(1, 8).Select(async number =>
        {
            await using var context = CreateContext();
            var product = NewProduct($"Product {number}");
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        var ids = await Task.WhenAll(tasks);

        Assert.Equal(8, ids.Distinct().Count());
        Assert.All(ids, id => Assert.InRange(id, 100000, 999999));
    }

    [Fact]
    public async Task SaveChanges_WithDuplicateNameIgnoringCaseAndAccents_IsRejected()
    {
        await using var context = CreateContext();
        var existing = NewProduct("Ótica");
        context.Products.Add(existing);
        await context.SaveChangesAsync();
        context.Products.Add(NewProduct("OTICA"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sqlException.Number, new[] { 2601, 2627 });
    }

    [Fact]
    public async Task SaveChanges_WithConcurrentDuplicateNames_AllowsOnlyOneProduct()
    {
        async Task<bool> InsertAsync(string name)
        {
            await using var context = CreateContext();
            context.Products.Add(NewProduct(name));
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception)
            {
                var sqlException = Assert.IsType<SqlException>(exception.InnerException);
                Assert.Contains(sqlException.Number, new[] { 2601, 2627 });
                return false;
            }
        }

        var results = await Task.WhenAll(InsertAsync("Ótica"), InsertAsync("OTICA"));

        Assert.Single(results, saved => saved);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_AfterSoftDeletion_AllowsNameReuseWithANewId()
    {
        await using var context = CreateContext();
        var original = NewProduct("Ótica");
        context.Products.Add(original);
        await context.SaveChangesAsync();
        original.DeletedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var replacement = NewProduct("OTICA");
        context.Products.Add(replacement);
        await context.SaveChangesAsync();

        Assert.NotEqual(original.Id, replacement.Id);
        await using var verification = CreateContext();
        Assert.Equal(2, await verification.Products.CountAsync());
        var deleted = await verification.Products.SingleAsync(product => product.Id == original.Id);
        Assert.NotNull(deleted.DeletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, deleted.DeletedAtUtc.Value.Kind);
    }

    [Theory]
    [InlineData(99999)]
    [InlineData(1000000)]
    public async Task SaveChanges_WithProductIdOutsideTheRange_IsRejected(int id)
    {
        await using var context = CreateContext();
        var product = NewProduct("Invalid identifier");
        product.Id = id;
        context.Products.Add(product);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
    }

    [Fact]
    public async Task SaveChanges_WithNegativeStock_IsRejectedByTheDatabase()
    {
        await using var context = CreateContext();
        var product = NewProduct("Invalid stock");
        product.Stock = -1;
        context.Products.Add(product);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
    }

    [Theory]
    [InlineData(StockMovementType.Increment, 0, 19, 20)]
    [InlineData(StockMovementType.Adjustment, 10, 3, 6)]
    [InlineData(StockMovementType.Adjustment, 3, 10, 6)]
    [InlineData(StockMovementType.Adjustment, 10, 10, 0)]
    public async Task SaveChanges_WithInconsistentMovement_RollsBackTheProductInsert(
        StockMovementType type, int stockBefore, int stockAfter, int quantity)
    {
        await using var context = CreateContext();
        var product = NewProduct("Failed creation");
        product.Stock = stockAfter;
        product.StockMovements.Add(new StockMovement
        {
            Type = type,
            Quantity = quantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            OccurredAtUtc = product.CreatedAtUtc
        });
        context.Products.Add(product);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
        await using var verification = CreateContext();
        Assert.Empty(await verification.Products.ToListAsync());
        Assert.Empty(await verification.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_WithStaleRowVersion_DoesNotChangeStockOrRecordTheLosingMovement()
    {
        await using (var setup = CreateContext())
        {
            setup.Products.Add(NewProduct("Concurrent stock"));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await firstContext.Products.SingleAsync();
        var second = await secondContext.Products.SingleAsync();

        first.Stock = 5;
        first.StockMovements.Add(new StockMovement
        {
            Type = StockMovementType.Increment,
            Quantity = 5,
            StockBefore = 0,
            StockAfter = 5,
            OccurredAtUtc = DateTime.UtcNow
        });
        await firstContext.SaveChangesAsync();

        second.Stock = 3;
        second.StockMovements.Add(new StockMovement
        {
            Type = StockMovementType.Increment,
            Quantity = 3,
            StockBefore = 0,
            StockAfter = 3,
            OccurredAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());

        await using var verification = CreateContext();
        var saved = await verification.Products.Include(product => product.StockMovements).SingleAsync();
        Assert.Equal(5, saved.Stock);
        Assert.Equal(5, Assert.Single(saved.StockMovements).Quantity);
        Assert.False(second.RowVersion.SequenceEqual(saved.RowVersion));
    }

    [Fact]
    public async Task DeleteProduct_WithExistingHistory_IsRejectedByTheForeignKey()
    {
        await using (var setup = CreateContext())
        {
            var product = NewProduct("Product with history");
            product.Stock = 1;
            product.StockMovements.Add(new StockMovement
            {
                Type = StockMovementType.Increment,
                Quantity = 1,
                StockBefore = 0,
                StockAfter = 1,
                OccurredAtUtc = product.CreatedAtUtc
            });
            setup.Products.Add(product);
            await setup.SaveChangesAsync();
        }

        await using var context = CreateContext();
        context.Products.Remove(await context.Products.SingleAsync());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_WhenSequenceIsExhausted_FailsWithoutCycling()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE [dbo].[ProductIds] RESTART WITH 999999");
        var last = NewProduct("Last identifier");
        context.Products.Add(last);
        await context.SaveChangesAsync();
        Assert.Equal(999999, last.Id);

        context.Products.Add(NewProduct("Beyond the identifier range"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(11728, Assert.IsType<SqlException>(exception.InnerException).Number);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
    }

    [Fact]
    public async Task AddAsync_WhenConcurrentRequestsBothFindTheNameAvailable_ReturnsOneDuplicate()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstRepository = new ProductRepository(firstContext);
        var secondRepository = new ProductRepository(secondContext);
        Assert.False(await firstRepository.ExistsByNameAsync("Ótica", null, CancellationToken.None));
        Assert.False(await secondRepository.ExistsByNameAsync("OTICA", null, CancellationToken.None));

        async Task<bool> TryAddAsync(ProductRepository repository, string name)
        {
            try
            {
                await repository.AddAsync(NewProduct(name), CancellationToken.None);
                return true;
            }
            catch (DomainException exception)
            {
                Assert.Equal("A product with this name already exists.", exception.Message);
                var databaseException = Assert.IsType<DbUpdateException>(exception.InnerException);
                Assert.IsType<SqlException>(databaseException.InnerException);
                return false;
            }
        }

        var results = await Task.WhenAll(
            TryAddAsync(firstRepository, "Ótica"),
            TryAddAsync(secondRepository, "OTICA"));

        Assert.Single(results, saved => saved);
        Assert.Single(results, saved => !saved);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
    }

    private ProductsDbContext CreateContext() => _database.CreateContext();

    private static Product NewProduct(string name) => new()
    {
        Name = name,
        CreatedAtUtc = DateTime.UtcNow
    };
}
