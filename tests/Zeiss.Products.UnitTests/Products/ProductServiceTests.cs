using Zeiss.Products.Application.Products.Interfaces;
using Zeiss.Products.Application.Products.Requests.CreateProduct;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;
using Zeiss.Products.Application.Products.Services;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;
using Zeiss.Products.Domain.Exceptions;

namespace Zeiss.Products.UnitTests.Products;

public class ProductServiceTests
{
    [Fact]
    public async Task CreateAsync_WithPositiveStock_TrimsNameAndRecordsInitialIncrement()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var product = await service.CreateAsync(
            new CreateProductRequest { Name = "  Contact lenses  ", Stock = 20 }, CancellationToken.None);

        Assert.Equal(100000, product.Id);
        Assert.Equal("Contact lenses", product.Name);
        Assert.Equal("Contact lenses", repository.CheckedName);
        Assert.Equal(20, product.Stock);
        Assert.NotNull(repository.SavedProduct);
        var movement = Assert.Single(repository.SavedProduct.StockMovements);
        Assert.Equal(StockMovementType.Increment, movement.Type);
        Assert.Equal(20, movement.Quantity);
        Assert.Equal(0, movement.StockBefore);
        Assert.Equal(20, movement.StockAfter);
        Assert.Equal(product.CreatedAtUtc, movement.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, movement.OccurredAtUtc.Kind);
    }

    [Fact]
    public async Task CreateAsync_WithZeroStock_CreatesAProductWithoutMovements()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var product = await service.CreateAsync(
            new CreateProductRequest { Name = "Contact lenses", Stock = 0 }, CancellationToken.None);

        Assert.Equal(0, product.Stock);
        Assert.NotNull(repository.SavedProduct);
        Assert.Empty(repository.SavedProduct.StockMovements);
    }

    [Fact]
    public async Task CreateAsync_WithExistingName_DoesNotAttemptToSave()
    {
        var repository = new FakeProductRepository { NameExists = true };
        var service = new ProductService(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateProductRequest { Name = "  Contact lenses  ", Stock = 20 }, CancellationToken.None));

        Assert.Equal("A product with this name already exists.", exception.Message);
        Assert.Equal("Contact lenses", repository.CheckedName);
        Assert.Null(repository.SavedProduct);
    }

    [Theory]
    [InlineData(true, 10, 5, 15)]
    [InlineData(false, 10, 5, 5)]
    [InlineData(false, 10, 10, 0)]
    [InlineData(true, 0, int.MaxValue, int.MaxValue)]
    public async Task ChangeStock_WithValidQuantity_RecordsTheBalanceChange(
        bool increment, int stockBefore, int quantity, int stockAfter)
    {
        var product = new Product { Id = 100000, Name = "Lenses", Stock = stockBefore };
        var repository = new FakeProductRepository { ExistingProduct = product };
        var service = new ProductService(repository);

        var updated = increment
            ? await service.AddToStockAsync(product.Id, quantity, CancellationToken.None)
            : await service.DecrementStockAsync(product.Id, quantity, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(stockAfter, updated.Stock);
        var movement = Assert.Single(product.StockMovements);
        Assert.Equal(increment ? StockMovementType.Increment : StockMovementType.Decrement, movement.Type);
        Assert.Equal(quantity, movement.Quantity);
        Assert.Equal(stockBefore, movement.StockBefore);
        Assert.Equal(stockAfter, movement.StockAfter);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Theory]
    [InlineData(false, 5, 6, "There is not enough stock for this operation.")]
    [InlineData(true, int.MaxValue, 1, "The resulting stock would exceed the supported limit.")]
    public async Task ChangeStock_WhenBalanceWouldBeInvalid_DoesNotChangeOrSaveAnything(
        bool increment, int stock, int quantity, string expectedMessage)
    {
        var product = new Product { Id = 100000, Name = "Lenses", Stock = stock };
        var repository = new FakeProductRepository { ExistingProduct = product };
        var service = new ProductService(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(async () =>
        {
            if (increment)
                await service.AddToStockAsync(product.Id, quantity, CancellationToken.None);
            else
                await service.DecrementStockAsync(product.Id, quantity, CancellationToken.None);
        });

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(stock, product.Stock);
        Assert.Empty(product.StockMovements);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task DeleteAsync_WithStock_PreservesTheBalanceAndHistory()
    {
        var movement = new StockMovement { Type = StockMovementType.Increment, Quantity = 20, StockAfter = 20 };
        var product = new Product { Id = 100000, Stock = 20, StockMovements = [movement] };
        var repository = new FakeProductRepository { ExistingProduct = product };

        var deleted = await new ProductService(repository).DeleteAsync(product.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.NotNull(product.DeletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, product.DeletedAtUtc.Value.Kind);
        Assert.Equal(20, product.Stock);
        Assert.Same(movement, Assert.Single(product.StockMovements));
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateAsync_TrimsNameAndExcludesItselfFromTheDuplicateCheck()
    {
        var product = new Product { Id = 100000, Name = "Lenses", Stock = 20 };
        var repository = new FakeProductRepository { ExistingProduct = product };

        var updated = await new ProductService(repository).UpdateAsync(product.Id,
            new UpdateProductRequest { Name = "  New name  ", Stock = 20 }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("New name", updated.Name);
        Assert.Equal(product.Id, repository.ExcludedProductId);
        Assert.Equal(20, product.Stock);
        Assert.Empty(product.StockMovements);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateName_LeavesTheProductUnchanged()
    {
        var product = new Product { Id = 100000, Name = "Lenses", Stock = 20 };
        var repository = new FakeProductRepository { ExistingProduct = product, NameExists = true };

        var exception = await Assert.ThrowsAsync<DomainException>(() => new ProductService(repository).UpdateAsync(product.Id,
            new UpdateProductRequest { Name = "Existing name", Stock = 5 }, CancellationToken.None));

        Assert.Equal("A product with this name already exists.", exception.Message);
        Assert.Equal("Lenses", product.Name);
        Assert.Equal(20, product.Stock);
        Assert.Empty(product.StockMovements);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Theory]
    [InlineData(10, 15, 5)]
    [InlineData(10, 3, 7)]
    [InlineData(10, 0, 10)]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    public async Task UpdateAsync_WithDifferentStock_RecordsOneAdjustment(int stockBefore, int stockAfter, int quantity)
    {
        var product = new Product { Id = 100000, Name = "Lenses", Stock = stockBefore };
        var repository = new FakeProductRepository { ExistingProduct = product };

        var updated = await new ProductService(repository).UpdateAsync(product.Id,
            new UpdateProductRequest { Name = "Updated lenses", Stock = stockAfter }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Updated lenses", updated.Name);
        Assert.Equal(stockAfter, updated.Stock);
        var movement = Assert.Single(product.StockMovements);
        Assert.Equal(StockMovementType.Adjustment, movement.Type);
        Assert.Equal(quantity, movement.Quantity);
        Assert.Equal(stockBefore, movement.StockBefore);
        Assert.Equal(stockAfter, movement.StockAfter);
        Assert.Equal(DateTimeKind.Utc, movement.OccurredAtUtc.Kind);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task ChangeStock_WithConcurrencyConflict_DoesNotRetryOrReturnSuccessData()
    {
        var saveException = new DomainException("Concurrent change.");
        var repository = new FakeProductRepository
        {
            ExistingProduct = new Product { Id = 100000, Stock = 10 },
            SaveException = saveException
        };

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            new ProductService(repository).DecrementStockAsync(100000, 5, CancellationToken.None));

        Assert.Same(saveException, exception);
        Assert.Equal(1, repository.SaveCalls);
    }

    // This fake isolates service rules. SQL behavior is covered by integration tests.
    private class FakeProductRepository : IProductRepository
    {
        public bool NameExists { get; init; }
        public Product? ExistingProduct { get; init; }
        public DomainException? SaveException { get; init; }
        public int SaveCalls { get; private set; }
        public int? ExcludedProductId { get; private set; }
        public string? CheckedName { get; private set; }
        public Product? SavedProduct { get; private set; }

        public Task<bool> ExistsByNameAsync(string name, int? excludedProductId, CancellationToken cancellationToken)
        {
            CheckedName = name;
            ExcludedProductId = excludedProductId;
            return Task.FromResult(NameExists);
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken)
        {
            product.Id = 100000;
            SavedProduct = product;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            if (SaveException is not null)
                throw SaveException;

            return Task.CompletedTask;
        }

        public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(ExistingProduct);

        public Task<List<Product>> GetAllAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<List<Product>> SearchAsync(string name, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<List<Product>> GetByStockLevelAsync(int min, int max, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
