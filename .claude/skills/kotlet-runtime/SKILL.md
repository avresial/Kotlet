---
name: kotlet-runtime
description: Install dependencies and run the full Kotlet app locally on SQLite (no Docker/Postgres/Aspire needed). Use when working in this repository and needing to run the app, verify frontend/backend behavior, log into a seeded development account, create API-backed test data, or take browser screenshots.
---

# Kotlet Runtime (cloud/sandbox agents)

Run the entire app — ASP.NET Core API + Angular frontend — locally with an
in-memory SQLite database. No Docker, no PostgreSQL, no Aspire orchestrator.

## Quick start

From the repo root:

```bash
.claude/skills/kotlet-runtime/scripts/install.sh   # one-time: .NET 10 SDK, Node, NuGet + npm packages
.claude/skills/kotlet-runtime/scripts/run.sh       # starts API (:5249) + frontend (:4200), waits until ready
```

When `run.sh` finishes, the app is live at **http://localhost:4200**.
Logs and PID files are in `/tmp/kotlet/`. Stop everything with
`.claude/skills/kotlet-runtime/scripts/stop.sh`. Both scripts are idempotent —
re-running them skips anything already installed/running.

## What the scripts handle for you

- **.NET 10 SDK** — installed via `apt-get` (`packages.microsoft.com` mirror);
  the `dot.net` install-script host is often blocked by sandbox network policies.
- **Node version** — the Angular CLI requires a newer Node than many sandboxes
  ship; if the system Node is too old, a Node 24 binary is installed from the
  npm registry into `~/.kotlet-node` and used automatically.
- **SQLite mode** — sets `Database__Provider=Sqlite` (shared in-memory DB,
  schema via `EnsureCreated`, dev users seeded on startup). Data is lost when
  the API restarts — that is intended for test runs.
- **JWT signing key** — normally injected by the Aspire AppHost; `run.sh`
  generates one so the API can start standalone.
- **Frontend → API proxy** — sets `services__api__http__0` so the Angular dev
  server proxies `/api` to the local API.

## Seeded logins

| Account | Email | Password |
| --- | --- | --- |
| User | `testuser@kotlet.local` | `TestUser123!` |
| Admin | `admin@kotlet.local` | `Admin123!` |

## API calls

Go through the frontend proxy so cookies/paths match the app (use
`--noproxy '*'` — sandbox HTTP proxies must not intercept localhost):

```bash
TOKEN=$(curl -s --noproxy '*' -X POST http://localhost:4200/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"testuser@kotlet.local","password":"TestUser123!"}' | jq -r .accessToken)

curl -s --noproxy '*' -H "Authorization: Bearer $TOKEN" http://localhost:4200/api/recipes
curl -s --noproxy '*' -H "Authorization: Bearer $TOKEN" http://localhost:4200/api/ingredients
```

## Browser checks / screenshots

Claude Code cloud sandboxes have Chromium preinstalled at
`/opt/pw-browsers/chromium`. Use `playwright-core` with
`executablePath: '/opt/pw-browsers/chromium'` — do not run `playwright install`.
The landing page has a **Log in** button in the top-right; click it to reach
the login form, sign in with a seeded account, and you land on `/dashboard`.

## Frontend unit tests / build

```bash
cd src/frontend
npm test -- --watch=false
npm run build
```

## Notes

- Use SQLite for all local checks unless PostgreSQL-specific behavior
  (migrations, schema, SQL) is under test — Postgres stays the source of truth
  for schema evolution and needs Docker + the Aspire AppHost
  (see `.codex/skills/kotlet-runtime/SKILL.md` for the Aspire flow).
- Backend unit/integration tests already use SQLite and need no setup:
  `dotnet test`.
- If Aspire was started only for verification, stop the processes you started;
  do not kill unrelated user processes.
