using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Zeiss.Products.Infrastructure.Persistence;

namespace Zeiss.Products.IntegrationTests;

// Each test owns a database on the configured server. The name is always generated here,
// so tests never run against or drop the development database.
public sealed class TestDatabase
{
    // Connection string to the SQL Server used by the tests. Any database name in it is replaced.
    public const string ServerVariable = "ZEISS_PRODUCTS_TEST_SQLSERVER";

    private const string LocalDbServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    public TestDatabase()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? LocalDbServer : server)
        {
            InitialCatalog = $"ZeissProductsTests_{Guid.NewGuid():N}"
        };

        Name = builder.InitialCatalog;
        DataSource = builder.DataSource;
        ConnectionString = builder.ConnectionString;
        Options = new DbContextOptionsBuilder<ProductsDbContext>().UseSqlServer(ConnectionString).Options;
    }

    public string Name { get; }

    public string DataSource { get; }

    public string ConnectionString { get; }

    public DbContextOptions<ProductsDbContext> Options { get; }

    public ProductsDbContext CreateContext() => new(Options);

    public async Task DropAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
