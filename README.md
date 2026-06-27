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
`/api/auth`.

To run the API without Aspire, configure `ConnectionStrings__kotletdb` with a
PostgreSQL connection string. To create a new migration after changing the model:

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
