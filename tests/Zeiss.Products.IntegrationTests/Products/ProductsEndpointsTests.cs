using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Zeiss.Products.Api.Responses;
using Zeiss.Products.Application.Products.Responses;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;
using Zeiss.Products.Infrastructure.Persistence;
using Zeiss.Products.Infrastructure.Repositories;

namespace Zeiss.Products.IntegrationTests.Products;

public class ProductsEndpointsTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Products", _database.ConnectionString));

        // Check isolation before sending any requests: never write to the development database.
        using var scope = _factory.Services.CreateScope();
        var actualConnection = new SqlConnectionStringBuilder(
            scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetConnectionString());
        Assert.Equal(_database.DataSource, actualConnection.DataSource);
        Assert.Equal(_database.Name, actualConnection.InitialCatalog);

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DropAsync();
    }

    [Fact]
    public async Task Post_WithInitialStock_ReturnsCreatedProductAndPersistsHistory()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "  Contact lenses  ", stock = 20 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.InRange(created.Id, 100000, 999999);
        Assert.Equal("Contact lenses", created.Name);
        Assert.Equal(20, created.Stock);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAtUtc.Kind);
        Assert.Null(response.Headers.Location);

        var getResponse = await _client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, await getResponse.Content.ReadFromJsonAsync<ProductResponse>());

        await using var verification = CreateContext();
        var product = await verification.Products.AsNoTracking()
            .Include(value => value.StockMovements).SingleAsync();
        Assert.Equal(created.Id, product.Id);
        var movement = Assert.Single(product.StockMovements);
        Assert.Equal(StockMovementType.Increment, movement.Type);
        Assert.Equal(20, movement.Quantity);
        Assert.Equal(0, movement.StockBefore);
        Assert.Equal(20, movement.StockAfter);
        Assert.Equal(product.CreatedAtUtc, movement.OccurredAtUtc);

        var repository = new ProductRepository(verification);
        Assert.NotNull(await repository.GetByIdAsync(created.Id, CancellationToken.None));
        Assert.Single(verification.ChangeTracker.Entries<Product>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_WithZeroOrOmittedStock_CreatesNoMovement(bool includeStock)
    {
        object request = includeStock ? new { name = "Contact lenses", stock = 0 } : new { name = "Contact lenses" };

        var response = await _client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verification = CreateContext();
        var product = await verification.Products.SingleAsync();
        Assert.Equal(0, product.Stock);
        Assert.Empty(await verification.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task Post_WithInvalidInput_ReturnsValidationErrorsWithoutSaving()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "   ", stock = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The request contains invalid values.", error.Message);
        Assert.Contains("Name is required.", error.Errors["Name"]);
        Assert.Contains("Stock must be greater than or equal to zero.", error.Errors["Stock"]);
        await using var verification = CreateContext();
        Assert.Empty(await verification.Products.ToListAsync());
        Assert.Empty(await verification.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task Post_WithDuplicateNameIgnoringCaseAndAccents_ReturnsConflict()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "Ótica", stock = 5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);

        var duplicate = await _client.PostAsJsonAsync("/api/products", new { name = "  OTICA  ", stock = 10 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await duplicate.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("A product with this name already exists.", error.Message);
        Assert.Empty(error.Errors);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task DeletedProduct_ReturnsNotFoundAndAllowsNameReuseWithANewId()
    {
        var missingResponse = await _client.GetAsync("/api/products/100000");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "Ótica", stock = 5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        var deleteResponse = await _client.DeleteAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deletedResponse = await _client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedResponse.StatusCode);
        var error = await deletedResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("Product not found.", error.Message);
        Assert.Empty(error.Errors);

        var replacementResponse = await _client.PostAsJsonAsync("/api/products", new { name = "OTICA", stock = 10 });
        Assert.Equal(HttpStatusCode.Created, replacementResponse.StatusCode);
        var replacement = await replacementResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(replacement);
        Assert.NotEqual(created.Id, replacement.Id);
        await using var verification = CreateContext();
        Assert.Equal(2, await verification.Products.CountAsync());
        Assert.Equal(2, await verification.StockMovements.CountAsync());
        var original = await verification.Products.SingleAsync(product => product.Id == created.Id);
        Assert.NotNull(original.DeletedAtUtc);
        Assert.Equal(5, original.Stock);
    }

    [Fact]
    public async Task Post_WhenIdentifiersRunOut_ReturnsAnExplicitConflictWithoutPartialData()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.ExecuteSqlRawAsync("ALTER SEQUENCE [dbo].[ProductIds] RESTART WITH 999999");
        }

        var lastResponse = await _client.PostAsJsonAsync("/api/products", new { name = "Last product", stock = 1 });
        Assert.Equal(HttpStatusCode.Created, lastResponse.StatusCode);
        var last = await lastResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(last);
        Assert.Equal(999999, last.Id);

        var response = await _client.PostAsJsonAsync("/api/products", new { name = "Beyond the range", stock = 20 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The product identifier range is exhausted.", error.Message);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.CountAsync());
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task StockEndpoints_ReturnOkAndRecordEachBalanceChange()
    {
        var product = await CreateProductAsync("Stock flow", 0);

        var added = await _client.PostAsync($"/api/products/{product.Id}/add-to-stock/20", null);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Equal(20, (await added.Content.ReadFromJsonAsync<ProductResponse>())!.Stock);
        var removed = await _client.PostAsync($"/api/products/{product.Id}/decrement-stock/7", null);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(13, (await removed.Content.ReadFromJsonAsync<ProductResponse>())!.Stock);
        var emptied = await _client.PostAsync($"/api/products/{product.Id}/decrement-stock/13", null);
        Assert.Equal(HttpStatusCode.OK, emptied.StatusCode);
        Assert.Equal(0, (await emptied.Content.ReadFromJsonAsync<ProductResponse>())!.Stock);

        await using var verification = CreateContext();
        Assert.Equal(0, (await verification.Products.SingleAsync()).Stock);
        var movements = await verification.StockMovements.OrderBy(movement => movement.Id).ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.Equal(new[] { 0, 20, 13 }, movements.Select(movement => movement.StockBefore));
        Assert.Equal(new[] { 20, 13, 0 }, movements.Select(movement => movement.StockAfter));
        Assert.Equal(new[] { 20, 7, 13 }, movements.Select(movement => movement.Quantity));
        Assert.Equal(new[] { StockMovementType.Increment, StockMovementType.Decrement, StockMovementType.Decrement },
            movements.Select(movement => movement.Type));
    }

    [Theory]
    [InlineData("add-to-stock", "0", HttpStatusCode.BadRequest)]
    [InlineData("decrement-stock", "-1", HttpStatusCode.BadRequest)]
    [InlineData("add-to-stock", "abc", HttpStatusCode.BadRequest)]
    [InlineData("decrement-stock", "1.5", HttpStatusCode.BadRequest)]
    [InlineData("decrement-stock", "6", HttpStatusCode.Conflict)]
    [InlineData("add-to-stock", "2147483647", HttpStatusCode.Conflict)]
    public async Task StockEndpoints_WithInvalidOperation_LeaveBalanceAndHistoryUnchanged(
        string operation, string quantity, HttpStatusCode expected)
    {
        var product = await CreateProductAsync("Stock validation", 5);

        var response = await _client.PostAsync($"/api/products/{product.Id}/{operation}/{quantity}", null);

        Assert.Equal(expected, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        if (expected == HttpStatusCode.BadRequest)
            Assert.NotEmpty(error.Errors);
        await using var verification = CreateContext();
        Assert.Equal(5, (await verification.Products.SingleAsync()).Stock);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Put_WithUnchangedStock_RenamesWithoutMovementAndRejectsInvalidOrDuplicateNames()
    {
        await CreateProductAsync("Ótica", 0);
        var product = await CreateProductAsync("Lenses", 5);

        var sameName = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "  LENSES  ", stock = 5 });
        Assert.Equal(HttpStatusCode.OK, sameName.StatusCode);
        Assert.Equal("LENSES", (await sameName.Content.ReadFromJsonAsync<ProductResponse>())!.Name);

        var renamed = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "  Reading glasses  ", stock = 5 });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var updated = await renamed.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Reading glasses", updated.Name);
        Assert.Equal(5, updated.Stock);
        Assert.Equal(product.CreatedAtUtc, updated.CreatedAtUtc);

        var duplicate = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "OTICA", stock = 999 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var invalid = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "   ", stock = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await using var verification = CreateContext();
        var saved = await verification.Products.SingleAsync(value => value.Id == product.Id);
        Assert.Equal("Reading glasses", saved.Name);
        Assert.Equal(5, saved.Stock);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Theory]
    [InlineData(10, 15, 5)]
    [InlineData(10, 3, 7)]
    [InlineData(10, 0, 10)]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    public async Task Put_WithDifferentStock_PersistsOneAdjustmentAndDoesNotRepeatIt(
        int stockBefore, int stockAfter, int quantity)
    {
        var product = await CreateProductAsync("Lenses", stockBefore);
        var request = new { name = "Updated lenses", stock = stockAfter };

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated lenses", updated.Name);
        Assert.Equal(stockAfter, updated.Stock);
        Assert.Equal(product.CreatedAtUtc, updated.CreatedAtUtc);

        var repeated = await _client.PutAsJsonAsync($"/api/products/{product.Id}", request);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        await using var verification = CreateContext();
        var saved = await verification.Products.Include(value => value.StockMovements).SingleAsync();
        Assert.Equal("Updated lenses", saved.Name);
        Assert.Equal(stockAfter, saved.Stock);
        Assert.Equal(stockBefore > 0 ? 2 : 1, saved.StockMovements.Count);
        var adjustment = Assert.Single(saved.StockMovements, movement => movement.Type == StockMovementType.Adjustment);
        Assert.Equal(quantity, adjustment.Quantity);
        Assert.Equal(stockBefore, adjustment.StockBefore);
        Assert.Equal(stockAfter, adjustment.StockAfter);
        Assert.Equal(DateTimeKind.Utc, adjustment.OccurredAtUtc.Kind);
    }

    [Theory]
    [InlineData("{\"name\":\"New name\"}")]
    [InlineData("{\"name\":\"New name\",\"stock\":null}")]
    [InlineData("{\"name\":\"New name\",\"stock\":-1}")]
    [InlineData("{\"name\":\"New name\",\"stock\":1.5}")]
    [InlineData("{\"name\":\"New name\",\"stock\":2147483648}")]
    [InlineData("{\"name\":")]
    public async Task Put_WithInvalidInput_ReturnsOurErrorResponseWithoutChangingTheProduct(string json)
    {
        var product = await CreateProductAsync("Original name", 5);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PutAsync($"/api/products/{product.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The request contains invalid values.", error.Message);
        Assert.NotEmpty(error.Errors);
        await using var verification = CreateContext();
        var saved = await verification.Products.SingleAsync();
        Assert.Equal("Original name", saved.Name);
        Assert.Equal(5, saved.Stock);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Description_IsTrimmedReturnedAndClearedByPut()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { name = "Lenses", description = "  Daily contact lenses  ", stock = 5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal("Daily contact lenses", created!.Description);
        var fetched = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}");
        Assert.Equal("Daily contact lenses", fetched!.Description);
        var listed = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products");
        Assert.Equal("Daily contact lenses", Assert.Single(listed!).Description);

        var maximum = new string('a', 1000);
        var replaced = await _client.PutAsJsonAsync($"/api/products/{created.Id}",
            new { name = "Lenses", description = $" {maximum} ", stock = 5 });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(maximum, (await replaced.Content.ReadFromJsonAsync<ProductResponse>())!.Description);

        var blank = await _client.PutAsJsonAsync($"/api/products/{created.Id}",
            new { name = "Lenses", description = "   ", stock = 5 });
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Null((await blank.Content.ReadFromJsonAsync<ProductResponse>())!.Description);

        var restored = await _client.PutAsJsonAsync($"/api/products/{created.Id}",
            new { name = "Lenses", description = "Restored", stock = 5 });
        Assert.Equal("Restored", (await restored.Content.ReadFromJsonAsync<ProductResponse>())!.Description);
        var omitted = await _client.PutAsJsonAsync($"/api/products/{created.Id}", new { name = "Lenses", stock = 5 });
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.Null((await omitted.Content.ReadFromJsonAsync<ProductResponse>())!.Description);

        await using var verification = CreateContext();
        Assert.Null((await verification.Products.SingleAsync()).Description);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Description_LongerThanTheLimit_ReturnsValidationErrorWithoutSaving(string method)
    {
        var product = await CreateProductAsync("Lenses", 5);
        var request = new { name = "Changed", description = new string('a', 1001), stock = 1 };

        var response = method == "POST"
            ? await _client.PostAsJsonAsync("/api/products", request)
            : await _client.PutAsJsonAsync($"/api/products/{product.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("The request contains invalid values.", error!.Message);
        Assert.Equal(["Description must not exceed 1000 characters."], error.Errors["Description"]);
        await using var verification = CreateContext();
        var saved = await verification.Products.SingleAsync();
        Assert.Equal("Lenses", saved.Name);
        Assert.Null(saved.Description);
    }

    [Fact]
    public async Task ListAndSearch_IncludeStockAndExcludeDeletedProducts()
    {
        var lenses = await CreateProductAsync("Ótica lenses", 20);
        var glasses = await CreateProductAsync("Reading glasses", 0);
        var special = await CreateProductAsync("Special lenses", 30);
        var deleted = await CreateProductAsync("Deleted lenses", 20);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/products/{deleted.Id}")).StatusCode);

        var all = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products");
        Assert.NotNull(all);
        Assert.Equal(new[] { lenses.Id, glasses.Id, special.Id }, all.Select(product => product.Id));
        Assert.Equal(new[] { 20, 0, 30 }, all.Select(product => product.Stock));

        var partial = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/search?name=%20LEN%20");
        Assert.Equal(new[] { lenses.Id, special.Id }, partial!.Select(product => product.Id));
        var accent = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/search?name=OTICA");
        Assert.Equal(lenses.Id, Assert.Single(accent!).Id);

        var range = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/stock-level?min=0&max=20");
        Assert.Equal(new[] { lenses.Id, glasses.Id }, range!.Select(product => product.Id));
        var exact = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/stock-level?min=20&max=20");
        Assert.Equal(lenses.Id, Assert.Single(exact!).Id);

        Assert.Empty((await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/search?name=missing"))!);
        Assert.Empty((await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products/stock-level?min=50&max=60"))!);
    }

    [Theory]
    [InlineData("/api/products/search")]
    [InlineData("/api/products/search?name=%20")]
    [InlineData("/api/products/stock-level")]
    [InlineData("/api/products/stock-level?min=0")]
    [InlineData("/api/products/stock-level?max=20")]
    [InlineData("/api/products/stock-level?min=-1&max=20")]
    [InlineData("/api/products/stock-level?min=20&max=10")]
    [InlineData("/api/products/stock-level?min=abc&max=20")]
    public async Task Search_WithMissingOrInvalidParameters_ReturnsBadRequest(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The request contains invalid values.", error.Message);
        Assert.NotEmpty(error.Errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteEndpoints_ForMissingOrDeletedProducts_ReturnNotFound(bool deleted)
    {
        var id = 100000;
        if (deleted)
        {
            id = (await CreateProductAsync("Deleted product", 5)).Id;
            Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/products/{id}")).StatusCode);
        }

        var update = await _client.PutAsJsonAsync($"/api/products/{id}", new { name = "New name", stock = 0 });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/products/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/products/{id}/add-to-stock/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/products/{id}/decrement-stock/1", null)).StatusCode);

        await using var verification = CreateContext();
        Assert.Equal(deleted ? 1 : 0, await verification.StockMovements.CountAsync());
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("add-to-stock")]
    [InlineData("decrement-stock")]
    public async Task WriteEndpoints_WhenAnotherRequestChangesTheRow_ReturnConflictWithoutPartialChanges(string operation)
    {
        var product = await CreateProductAsync("Original name", 10);
        // Force another SQL Server context to commit between this request's read and save.
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<ProductsDbContext>(options => options.AddInterceptors(new ConcurrentRenameInterceptor(_database.Options)))));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = operation switch
        {
            "PUT" => await client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Losing name", stock = 3 }),
            "DELETE" => await client.DeleteAsync($"/api/products/{product.Id}"),
            _ => await client.PostAsync($"/api/products/{product.Id}/{operation}/3", null)
        };

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The product changed during this operation. Read it again before retrying.",
            error.Message);
        await using var verification = CreateContext();
        var saved = await verification.Products.SingleAsync();
        Assert.Equal("Concurrent rename", saved.Name);
        Assert.Equal(10, saved.Stock);
        Assert.Null(saved.DeletedAtUtc);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    private async Task<ProductResponse> CreateProductAsync(string name, int stock)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name, stock });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private ProductsDbContext CreateContext() => _database.CreateContext();

    // Test-only hook: creates an actual RowVersion conflict without timing-dependent delays.
    private class ConcurrentRenameInterceptor : SaveChangesInterceptor
    {
        private readonly DbContextOptions<ProductsDbContext> _options;

        public ConcurrentRenameInterceptor(DbContextOptions<ProductsDbContext> options)
        {
            _options = options;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entry = eventData.Context!.ChangeTracker.Entries<Product>()
                .SingleOrDefault(value => value.State == EntityState.Modified);
            if (entry is not null)
            {
                await using var concurrent = new ProductsDbContext(_options);
                var product = await concurrent.Products.SingleAsync(value => value.Id == entry.Entity.Id, cancellationToken);
                product.Name = "Concurrent rename";
                await concurrent.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
