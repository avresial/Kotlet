#!/usr/bin/env bash
# Install everything needed to run Kotlet locally with SQLite (no Docker/Postgres).
# Safe to re-run; each step is skipped when already satisfied.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
NODE_PREFIX="${KOTLET_NODE_PREFIX:-$HOME/.kotlet-node}"
MIN_NODE_MAJOR=22

log() { echo "[kotlet-install] $*"; }

# --- .NET 10 SDK ---------------------------------------------------------
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  log ".NET 10 SDK already installed: $(dotnet --version)"
else
  log "Installing .NET 10 SDK..."
  if command -v apt-get >/dev/null 2>&1; then
    # Ubuntu 24.04+ ships dotnet-sdk-10.0 in universe; packages.microsoft.com also carries it.
    apt-get update -qq || true
    apt-get install -y dotnet-sdk-10.0
  else
    # Fallback: official install script (host may be blocked by restrictive network policies).
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
    log "Installed to \$HOME/.dotnet — add it to PATH: export PATH=\"\$HOME/.dotnet:\$PATH\""
  fi
  log ".NET SDK: $(dotnet --version)"
fi

# --- Node.js (Angular CLI needs a recent 22.x/24.x) ----------------------
node_ok() {
  command -v "$1" >/dev/null 2>&1 || return 1
  local v major minor patch
  v="$("$1" --version)"; v="${v#v}"
  IFS=. read -r major minor patch <<<"$v"
  # Angular CLI 20 requires >= 22.22.3 (or 24.15+); be strict enough to cover it.
  [ "$major" -gt "$MIN_NODE_MAJOR" ] || { [ "$major" -eq "$MIN_NODE_MAJOR" ] && [ "$minor" -ge 23 ]; }
}

if node_ok node; then
  log "Node is recent enough: $(node --version)"
elif node_ok "$NODE_PREFIX/node_modules/node/bin/node"; then
  log "Node 24 already installed at $NODE_PREFIX"
else
  log "System Node ($(node --version 2>/dev/null || echo 'missing')) too old for the Angular CLI; installing Node 24 from the npm registry..."
  mkdir -p "$NODE_PREFIX"
  npm install --prefix "$NODE_PREFIX" node@24 >/dev/null
  log "Installed $("$NODE_PREFIX/node_modules/node/bin/node" --version) at $NODE_PREFIX (run.sh picks it up automatically)"
fi

# --- Backend NuGet restore ------------------------------------------------
log "Restoring and building backend..."
dotnet build "$REPO_ROOT/src/backend/Kotlet.Api/Kotlet.Api.csproj"

# --- Frontend npm packages ------------------------------------------------
log "Installing frontend npm packages..."
cd "$REPO_ROOT/src/frontend"
npm ci

log "Done. Start the app with: .claude/skills/kotlet-runtime/scripts/run.sh"
