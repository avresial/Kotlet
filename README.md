# Kotlet

Kotlet is a personal web application for storing recipes, tracking ingredient prices,
and planning a home menu and cooking calendar.

## Technology

- Backend: ASP.NET Core on .NET 10
- Frontend: Angular 21
- Database: Supabase (PostgreSQL)
- Local orchestration and observability: .NET Aspire

The initial runnable version uses an in-memory menu reader. Supabase integration will
be added behind the interfaces owned by the Application layer, keeping database code
inside Infrastructure.

## Architecture

The backend follows Clean Architecture. Dependencies point inward:

```text
Api -> Application <- Infrastructure
          |
          v
        Domain
```

- `Domain` contains business entities and rules.
- `Application` contains use cases and contracts required by those use cases.
- `Infrastructure` implements Application contracts, including future Supabase access.
- `Api` is the HTTP entry point and composition root.

Each layer is organized by vertical business modules rather than technical folders.
For example, the current menu use case lives under `Application/Menu/GetMenu`; future
modules will include `Recipes`, `Calendar`, `Ingredients`, and `Pricing`. A feature's
commands, queries, handlers, validation, and contracts stay together within its slice.

## Repository structure

```text
src/
  backend/
    Kotlet.Domain/
    Kotlet.Application/
    Kotlet.Infrastructure/
    Kotlet.Api/
  frontend/                 # Angular application
  aspire/
    Kotlet.AppHost/         # Development orchestrator
    Kotlet.ServiceDefaults/ # Health checks, telemetry, service discovery
```

## Run locally

Prerequisites: .NET 10 SDK, Node.js 22.22+ or 24.x, npm, and a Docker-compatible container runtime.

```powershell
dotnet run --project src/aspire/Kotlet.AppHost
```

Aspire starts the API and Angular development server and displays their endpoints in
the Aspire dashboard. It also starts PostgreSQL and supplies the `kotletdb` connection
string to the API. A background worker applies EF Core migrations when the API starts.
The sample API endpoint is `GET /api/menu`; authentication endpoints are under
`/api/auth`. In development, the interactive Scalar API client is available at
`/scalar/v1`.

After migrations, the development database seeder creates two local accounts if
they do not already exist:

- `admin@kotlet.local` / `Admin123!`
- `testuser@kotlet.local` / `TestUser123!`

The seeder does not run outside the Development environment.

To run the API without Aspire, configure `ConnectionStrings__kotletdb` with a
PostgreSQL connection string.

## Shared Supabase database

Kotlet isolates every application table and its EF Core migration history in the
`kotlet` PostgreSQL schema. The API is the only component that connects to the
database; do not put the database connection string in Angular configuration or
allow the browser to write directly to Supabase tables.

The shared Supabase project (`FinanceManager`, schema `kotlet`) is already
provisioned. See [docs/supabase-connection.md](docs/supabase-connection.md) for
the project host, the exact connection-string settings, and how to apply future
migrations.

Configure the Supabase PostgreSQL connection string as a secret on the backend host:

```powershell
$env:ConnectionStrings__kotletdb = 'Host=<host>;Port=5432;Database=postgres;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true'
```

Use a local environment variable, .NET user secrets, or an Azure App Service
application setting. Never commit the database password. Apply migrations from a
trusted machine with network access to Supabase:

```powershell
dotnet ef database update --project src/backend/Kotlet.Infrastructure --startup-project src/backend/Kotlet.Api
```

Review the SQL first when targeting a shared project:

```powershell
dotnet ef migrations script --idempotent --project src/backend/Kotlet.Infrastructure --startup-project src/backend/Kotlet.Api --output kotlet-migrations.sql
```

The script must only create or modify objects in `kotlet`, including
`kotlet.__EFMigrationsHistory`. Existing application schemas and Supabase-managed
objects must remain untouched. If the `kotlet` schema is ever exposed through the
Supabase Data API, review and enable appropriate Row Level Security policies first.

## Authentication configuration

Access tokens are short-lived JWTs. Refresh tokens are sent only through the
`kotlet_refresh` HTTP-only cookie and persisted as SHA-256 hashes. Production must
provide a unique signing key of at least 32 bytes through secure configuration; do
not add it to an appsettings file:

```powershell
$env:Jwt__SigningKey = '<a-random-secret-of-at-least-32-bytes>'
```

The issuer, audience, access-token lifetime, refresh-token lifetime, and cookie name
are configured under `Jwt` and `Auth` in `appsettings.json`. Aspire generates an
ephemeral development signing key on every run and supplies it to the API; restarting
Aspire therefore invalidates existing access tokens. For direct local API launches,
set `Jwt__SigningKey` in the environment or .NET user secrets.

PostgreSQL is the default database provider. For temporary development with an
in-memory SQLite database, use the API's `sqlite` launch profile:

```powershell
dotnet run --project src/backend/Kotlet.Api --launch-profile sqlite
```

The same provider can be selected when running through Aspire. The SQLite database
exists only for the lifetime of the API process, and Aspire will not start PostgreSQL:

```powershell
$env:Database__Provider = 'Sqlite'
dotnet run --project src/aspire/Kotlet.AppHost
```

Remove the environment variable to return to the default PostgreSQL setup.

To create a new PostgreSQL migration after changing the model:

```powershell
dotnet ef migrations add <MigrationName> --project src/backend/Kotlet.Infrastructure --startup-project src/backend/Kotlet.Api --output-dir Persistence/Migrations
```

### Visual Studio Code

1. Open the repository root in VS Code and install the recommended extensions.
2. Press `F5` and select `.NET Aspire AppHost` to run with the debugger.
3. Alternatively, run `Tasks: Run Task` -> `run Aspire AppHost` from the Command
   Palette. `Ctrl+Shift+B` builds the complete solution.

## Build

```powershell
dotnet build Kotlet.slnx
npm --prefix src/frontend run build
```
