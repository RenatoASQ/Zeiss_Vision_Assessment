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
        // Intentionally leave this unique database uncreated, so opening a connection fails.
        // Checked through master: LocalDB can report error 233 instead of 4060 for the failed login.
        var database = new TestDatabase();

        var master = new SqlConnectionStringBuilder(database.ConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT DB_ID(@name)", connection);
        command.Parameters.AddWithValue("@name", database.Name);
        Assert.IsType<DBNull>(await command.ExecuteScalarAsync());

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment).UseSetting("ConnectionStrings:Products", database.ConnectionString));

        // Verify isolation before making requests, especially the POST.
        using var scope = factory.Services.CreateScope();
        var actualConnection = new SqlConnectionStringBuilder(
            scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetConnectionString());
        Assert.Equal(database.DataSource, actualConnection.DataSource);
        Assert.Equal(database.Name, actualConnection.InitialCatalog);

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
