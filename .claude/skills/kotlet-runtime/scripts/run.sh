#!/usr/bin/env bash
# Run the whole Kotlet app locally on SQLite: API on :5249, Angular frontend on :4200.
# No Docker, Postgres, or Aspire needed. Blocks until both are ready, then returns;
# servers keep running in the background (logs + PIDs under /tmp/kotlet).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
NODE_PREFIX="${KOTLET_NODE_PREFIX:-$HOME/.kotlet-node}"
RUN_DIR="${KOTLET_RUN_DIR:-/tmp/kotlet}"
API_PORT="${KOTLET_API_PORT:-5249}"
WEB_PORT="${KOTLET_WEB_PORT:-4200}"

log() { echo "[kotlet-run] $*"; }
probe() {
  # curl prints 000 itself on connection failure; ${c:-000} covers curl not running at all.
  local c
  c="$(curl -s -o /dev/null -w '%{http_code}' --noproxy '*' --max-time 5 "$1" 2>/dev/null)" || true
  echo "${c:-000}"
}

mkdir -p "$RUN_DIR"

# Prefer the Node installed by install.sh when the system one is too old.
if [ -x "$NODE_PREFIX/node_modules/node/bin/node" ]; then
  export PATH="$NODE_PREFIX/node_modules/node/bin:$PATH"
fi
[ -d "$HOME/.dotnet" ] && export PATH="$HOME/.dotnet:$PATH"

# --- API (SQLite, in-memory) ---------------------------------------------
if [ "$(probe "http://localhost:$API_PORT/api/ingredients")" != 000 ]; then
  log "API already responding on :$API_PORT — leaving it as is"
else
  # The Aspire AppHost normally injects the JWT signing key; generate one here.
  JWT_KEY="$(openssl rand -base64 32 2>/dev/null || head -c 32 /dev/urandom | base64)"
  log "Building and starting API on :$API_PORT (SQLite in-memory)..."
  dotnet build "$REPO_ROOT/src/backend/Kotlet.Api/Kotlet.Api.csproj" >"$RUN_DIR/api-build.log" 2>&1
  (
    cd "$REPO_ROOT/src/backend/Kotlet.Api"
    ASPNETCORE_ENVIRONMENT=Development \
    Database__Provider=Sqlite \
    ASPNETCORE_URLS="http://localhost:$API_PORT" \
    Jwt__SigningKey="$JWT_KEY" \
    OAuth__Issuer="http://localhost:$API_PORT" \
    OAuth__Resource="http://localhost:$API_PORT/mcp" \
    nohup dotnet run --no-build >"$RUN_DIR/api.log" 2>&1 &
    echo $! >"$RUN_DIR/api.pid"
  )
  for _ in $(seq 1 60); do
    [ "$(probe "http://localhost:$API_PORT/api/ingredients")" != 000 ] && break
    sleep 2
  done
  if [ "$(probe "http://localhost:$API_PORT/api/ingredients")" = 000 ]; then
    log "ERROR: API did not come up; last log lines:"; tail -30 "$RUN_DIR/api.log"; exit 1
  fi
  log "API is up (seeded dev users included)"
fi

# --- Frontend (Angular dev server, proxies /api to the local API) --------
if [ "$(probe "http://localhost:$WEB_PORT")" = 200 ]; then
  log "Frontend already responding on :$WEB_PORT — leaving it as is"
else
  log "Starting Angular dev server on :$WEB_PORT..."
  (
    cd "$REPO_ROOT/src/frontend"
    services__api__http__0="http://localhost:$API_PORT" \
    nohup npm start -- --host 0.0.0.0 --port "$WEB_PORT" >"$RUN_DIR/web.log" 2>&1 &
    echo $! >"$RUN_DIR/web.pid"
  )
  for _ in $(seq 1 90); do
    [ "$(probe "http://localhost:$WEB_PORT")" = 200 ] && break
    sleep 3
  done
  if [ "$(probe "http://localhost:$WEB_PORT")" != 200 ]; then
    log "ERROR: frontend did not come up; last log lines:"; tail -30 "$RUN_DIR/web.log"; exit 1
  fi
  log "Frontend is up"
fi

log "App ready:"
log "  Frontend:  http://localhost:$WEB_PORT"
log "  API:       http://localhost:$API_PORT"
log "  Logs/PIDs: $RUN_DIR"
log "  Login:     testuser@kotlet.local / TestUser123!  (admin: admin@kotlet.local / Admin123!)"
