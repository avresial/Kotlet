---
name: kotlet-runtime
description: Start and test the Kotlet app locally. Use when working in this repository and needing to run the full app, verify frontend/backend behavior, log into the seeded development account, create API-backed test data, or perform browser/UI checks through Aspire.
---

# Kotlet Runtime

Use Aspire for full-app checks. Do not start only Angular when backend, auth, seeded data, or API behavior matters.

## Start the app

From the repo root:

```powershell
$env:Database__Provider='Sqlite'
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT='true'
dotnet run --project src/aspire/Kotlet.AppHost/Kotlet.AppHost.csproj --launch-profile http
```

Use SQLite for local UI checks unless PostgreSQL/Docker behavior is specifically under test.

Wait for the frontend:

```powershell
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:4200
```

## Login

Seeded development user:

```text
testuser@kotlet.local
TestUser123!
```

Seeded admin user:

```text
admin@kotlet.local
Admin123!
```

## API calls through the frontend proxy

Use the frontend URL so cookies/proxy behavior match the app:

```powershell
$base = 'http://127.0.0.1:4200'
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" `
  -ContentType 'application/json' `
  -Body (@{ email='testuser@kotlet.local'; password='TestUser123!' } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
```

Then call authenticated endpoints with `$headers`, for example:

```powershell
Invoke-RestMethod -Method Get -Uri "$base/api/ingredients" -Headers $headers
Invoke-RestMethod -Method Get -Uri "$base/api/recipes" -Headers $headers
```

## Frontend checks

From `src/frontend`:

```powershell
npm test -- --watch=false
npm run build
```

## Cleanup

If Aspire was started only for verification, stop the AppHost and child `ng serve` processes you started. Do not kill unrelated user processes.
