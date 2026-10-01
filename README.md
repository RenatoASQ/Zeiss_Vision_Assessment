# Zeiss.Products

Product management API for the ZEISS assessment. ASP.NET Core 10, EF Core 10,
SQL Server, FluentValidation and xUnit.

## Prerequisites

- .NET 10 SDK.
- One SQL Server option:
  - **Docker** with Docker Compose, using `compose.yaml` (SQL Server 2025, linux/amd64).
  - **SQL Server Express LocalDB** on Windows.
  - Any other reachable SQL Server instance.

The SQL Server image is published for linux/amd64 only. On ARM machines such as
Apple Silicon it is not supported and can only run under emulation; use a remote
SQL Server instead.

Commands below are for PowerShell, run from the folder containing `Zeiss.Products.slnx`.

## Database

### Option A: SQL Server in Docker

```powershell
$env:MSSQL_SA_PASSWORD = "<strong password>"   # 8+ characters: upper, lower, digit, symbol
docker compose up --detach --wait
$env:ConnectionStrings__Products = "Server=localhost,1433;Database=ZeissProducts;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
```

On Linux or macOS (bash), the same setup is:

```bash
export MSSQL_SA_PASSWORD='<strong password>'
docker compose up --detach --wait
export ConnectionStrings__Products="Server=localhost,1433;Database=ZeissProducts;User ID=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
```

In bash, write the other `$env:NAME = "value"` lines below as `export NAME="value"`.
The password can also go in a git-ignored `.env` file. `--wait` returns once the
healthcheck can query SQL Server. Data is kept in the `sqlserver-data` volume;
`docker compose down --volumes` removes it. Set `MSSQL_PORT` to use another host port.

### Option B: LocalDB

```powershell
SqlLocalDB start MSSQLLocalDB
```

If the instance does not exist, create it with `SqlLocalDB create MSSQLLocalDB`.
No further configuration is needed: `appsettings.Development.json` points to LocalDB.

### Connection string

The API reads `ConnectionStrings:Products`. The environment variable
`ConnectionStrings__Products` overrides `appsettings.Development.json`. For Visual Studio,
store it as a user secret instead:

```powershell
dotnet user-secrets set "ConnectionStrings:Products" "<connection string>" --project src/Zeiss.Products.Api
```

## Migrations and seed data

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src/Zeiss.Products.Infrastructure --startup-project src/Zeiss.Products.Api
```

This applies the migrations and seeds three sample products only when the product
table is empty. Existing rows, including deleted products, prevent reseeding.
Starting the API does not apply migrations or run the seeder.

## Run the API

```powershell
dotnet run --project src/Zeiss.Products.Api --launch-profile http
```

Swagger: [http://localhost:5260/swagger](http://localhost:5260/swagger).
All nine endpoints are available there. Visual Studio can use the same `http` profile.

## Tests

```powershell
dotnet test Zeiss.Products.slnx
```

Unit tests cover validators and service behavior. Integration tests cover HTTP
endpoints, validation and error responses, migrations, seeding, constraints,
transactions and concurrency against a real SQL Server.

Integration tests use LocalDB by default. To use Docker or another server, set
`ZEISS_PRODUCTS_TEST_SQLSERVER` to a server connection string; any database name
in it is ignored:

```powershell
$env:ZEISS_PRODUCTS_TEST_SQLSERVER = "Server=localhost,1433;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet test Zeiss.Products.slnx
```

Each test creates its own `ZeissProductsTests_<guid>` database and drops only that
database afterwards; the development database is never used.

## Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request on Ubuntu x64:
restore, Release build, unit tests, then integration tests against SQL Server
started from the same `compose.yaml`, with a password generated for each run.

## Architecture

- **Api:** controller, error handling and dependency registration.
- **Application:** request validators, product service, DTOs and repository interface.
- **Domain:** entities, shared product rules and exceptions; no project dependencies.
- **Infrastructure:** EF mappings, migrations, seeding and repository implementation.

Application depends on Domain. Infrastructure depends on Application and Domain.
Api references Application and Infrastructure for dependency registration.

Requests and their validators live in the same file. Each action calls its validator
explicitly before the service. This repeats a few lines per action, but every endpoint
reads top to bottom without hidden pipeline steps or reflection.
Model binding and validation errors return `400`. `DomainException` is handled in one
place and returns `409`; unknown products return `404`. Unexpected errors are logged and
return a generic `500`. All errors use the same `ErrorResponse` body.

## Decisions and assumptions

- SQL Server provides a shared sequence, transactions and native `rowversion`.
  Product IDs range from `100000` to `999999`, with no cycling or deliberate reuse.
  Sequence gaps consume capacity; exhaustion returns `409`. The limit is permanent,
  so retrying does not help.
- Names are trimmed, required and limited to 200 characters. A unique index prevents
  duplicate names among non-deleted products, ignoring case and accents.
- Description is an optional extension, not an assessment requirement. It is trimmed,
  limited to 1000 characters, and blank values are stored as `null`.
  Price was left out because the assessment does not specify it.
- Stock is a non-negative 32-bit integer. Increment and decrement quantities must be
  positive. An increment beyond 2,147,483,647 returns `409`, as it depends on the
  current balance. Stock changes and movement history are saved atomically in one
  `SaveChangesAsync`.
- PUT replaces name, description and stock: it requires name and stock, sets an absolute
  balance, and clears the description when it is omitted or `null`. Changed stock
  creates an `Adjustment`; positive initial stock creates an `Increment`.
- `rowversion` detects changes between the read and save within a request and returns
  `409`, without automatic retries. PUT can overwrite changes completed before its
  own read; it does not check a version from an earlier GET.
- DELETE is logical, preserves movement history and allows products with stock to
  be deleted. Deleted products are excluded from normal queries and operations.

Unique names, movement history and logical deletion are additional design choices.
Lists are unpaginated. Authentication and idempotency keys are outside this scope;
repeating a successful stock request can apply the change again.
