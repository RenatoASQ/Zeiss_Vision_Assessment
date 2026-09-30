# Zeiss.Products

Product management API for the ZEISS assessment. ASP.NET Core 10, EF Core 10,
SQL Server, FluentValidation and xUnit.

## Run locally

Requires Windows, .NET 10 SDK and SQL Server Express LocalDB.
From the folder containing `Zeiss.Products.slnx`, run in PowerShell:

```powershell
dotnet tool restore
dotnet restore
SqlLocalDB start MSSQLLocalDB
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src/Zeiss.Products.Infrastructure --startup-project src/Zeiss.Products.Api
dotnet run --project src/Zeiss.Products.Api --launch-profile http
```

If the LocalDB instance does not exist, create it with `SqlLocalDB create MSSQLLocalDB`.
The connection string is in `src/Zeiss.Products.Api/appsettings.Development.json`.

Swagger: [http://localhost:5260/swagger](http://localhost:5260/swagger).
All nine endpoints are available there.

The database update command applies migrations and seeds three sample products
only when the product table is empty. Existing rows, including deleted products,
prevent reseeding. Starting the API does not apply migrations or run the seeder.

## Architecture

- **Api:** controllers, HTTP validation responses and dependency registration.
- **Application:** request validators, product service, DTOs and repository interface.
- **Domain:** entities, shared product rules and exceptions; no project dependencies.
- **Infrastructure:** EF mappings, migrations, seeding and repository implementation.

Application depends on Domain. Infrastructure depends on Application and Domain.
Api references Application and Infrastructure for dependency registration.

## Decisions and assumptions

- SQL Server provides a shared sequence, transactions and native `rowversion`.
  Product IDs range from `100000` to `999999`, with no cycling or deliberate reuse.
  Sequence gaps consume capacity; exhaustion returns `409`.
- Names are trimmed, required and limited to 200 characters. A unique index prevents
  duplicate names among non-deleted products, ignoring case and accents.
- Stock is non-negative. Increment and decrement quantities must be positive.
  Stock changes and movement history are saved atomically in one `SaveChangesAsync`.
- PUT requires name and stock and sets an absolute balance. Changed stock creates
  an `Adjustment`; positive initial stock creates an `Increment`.
- `rowversion` detects changes between the read and save within a request and returns
  `409`, without automatic retries. PUT can overwrite changes completed before its
  own read; it does not check a version from an earlier GET.
- DELETE is logical, preserves movement history and allows products with stock to
  be deleted. Deleted products are excluded from normal queries and operations.

Unique names, movement history and logical deletion are additional design choices.
Lists are unpaginated. Authentication and idempotency keys are outside this scope;
repeating a successful stock request can apply the change again.

## Tests

```powershell
dotnet test Zeiss.Products.slnx
```

Unit tests cover validators and service behavior. Integration tests cover HTTP
endpoints, migrations, seeding, constraints, transactions and concurrency against
SQL Server LocalDB. Each integration test uses an isolated database and removes it
afterwards; the development database is not used.
