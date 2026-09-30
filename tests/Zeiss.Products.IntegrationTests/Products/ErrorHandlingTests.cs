using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeiss.Products.Api.Responses;
using Zeiss.Products.Infrastructure.Persistence;

namespace Zeiss.Products.IntegrationTests.Products;

public class ErrorHandlingTests
{
    [Theory]
    [InlineData("Development", "GET")]
    [InlineData("Development", "POST")]
    [InlineData("Production", "GET")]
    [InlineData("Production", "POST")]
    public async Task Products_WhenDatabaseIsMissing_ReturnGenericJsonError(string environment, string method)
    {
        // Intentionally leave this unique database uncreated to reproduce SQL Server error 4060.
        var databaseName = $"ZeissProductsMissing_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";

        await using var connection = new SqlConnection(connectionString);
        var sqlException = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());
        Assert.Equal(4060, sqlException.Number);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment).UseSetting("ConnectionStrings:Products", connectionString));

        // Verify isolation before making requests, especially the POST.
        using var scope = factory.Services.CreateScope();
        var actualConnection = new SqlConnectionStringBuilder(
            scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetConnectionString());
        Assert.Equal("(localdb)\\MSSQLLocalDB", actualConnection.DataSource);
        Assert.Equal(databaseName, actualConnection.InitialCatalog);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        // The developer exception page would expose a stack trace for this Accept header.
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");

        using var response = method == "POST"
            ? await client.PostAsJsonAsync("/api/products", new { name = "Contact lenses", stock = 20 })
            : await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("An unexpected error occurred while processing the request.", error.Message);
        Assert.Empty(error.Errors);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "errors", "message" }, body.RootElement.EnumerateObject()
            .Select(property => property.Name).OrderBy(name => name));
    }
}
