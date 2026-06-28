# Connecting Kotlet to Supabase

Kotlet shares a single Supabase PostgreSQL project with the existing Finance
Manager application. The two apps never collide because Kotlet keeps every table
and its EF Core migration history inside the dedicated `kotlet` schema, while
Finance Manager lives in `public`.

## Project

- Supabase project: **FinanceManager** (`zrosvnseymuzapysmdnc`), region `eu-central-1`
- API URL: `https://zrosvnseymuzapysmdnc.supabase.co`
- Database host (direct): `db.zrosvnseymuzapysmdnc.supabase.co`
- Schema owned by Kotlet: `kotlet`

The `kotlet` schema, all application tables, and the EF Core
`kotlet.__EFMigrationsHistory` table have already been provisioned in this
project. The migration history is seeded so the API treats every existing
migration as applied and will not attempt to recreate the tables.

## Connection string

Only the backend (`Kotlet.Api`) connects to the database. Never place the
connection string in Angular configuration or let the browser talk to Supabase
directly.

Configure the connection string as a secret on the backend host. Use the
project's database password (Supabase dashboard → Project Settings → Database):

```powershell
$env:ConnectionStrings__kotletdb = 'Host=db.zrosvnseymuzapysmdnc.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>;SSL Mode=Require;Trust Server Certificate=true'
```

For local development you can store it with .NET user secrets instead:

```powershell
dotnet user-secrets set "ConnectionStrings:kotletdb" "Host=db.zrosvnseymuzapysmdnc.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>;SSL Mode=Require;Trust Server Certificate=true" --project src/backend/Kotlet.Api
```

The direct `db.<ref>.supabase.co` host resolves over IPv6. If your network is
IPv4-only, use the connection pooler string instead — copy it from the Supabase
dashboard (Project Settings → Database → Connection string → "Session pooler"),
which uses host `…pooler.supabase.com` and username `postgres.zrosvnseymuzapysmdnc`.
Keep `SSL Mode=Require`.

Because the connection string already targets this project, run the API with the
default PostgreSQL provider (no Aspire-managed PostgreSQL container is needed):

```powershell
dotnet run --project src/backend/Kotlet.Api
```

## Applying future migrations

The schema is current as of migration `20260628000000_AddMealParticipants`. After
changing the model and adding a migration, apply it from a trusted machine with
the connection string configured:

```powershell
dotnet ef database update --project src/backend/Kotlet.Infrastructure --startup-project src/backend/Kotlet.Api
```

Review the SQL before applying it to the shared project, and confirm it only
creates or modifies objects in the `kotlet` schema:

```powershell
dotnet ef migrations script --idempotent --project src/backend/Kotlet.Infrastructure --startup-project src/backend/Kotlet.Api --output kotlet-migrations.sql
```

## Row Level Security

The `kotlet` schema is not exposed through the Supabase Data API (PostgREST only
serves the schemas configured under "Exposed schemas", which Kotlet does not
join). All access goes through the backend's authenticated PostgreSQL
connection, so the anon and publishable keys cannot read or write Kotlet tables.

If you ever add `kotlet` to the exposed schemas, enable Row Level Security and
add policies on every table first:

```sql
ALTER TABLE kotlet.users ENABLE ROW LEVEL SECURITY;
-- …repeat for every kotlet table, then add policies before exposing the schema.
```
