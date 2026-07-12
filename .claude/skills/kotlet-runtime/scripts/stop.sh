#!/usr/bin/env bash
# Stop the Kotlet API and frontend started by run.sh.
set -uo pipefail

RUN_DIR="${KOTLET_RUN_DIR:-/tmp/kotlet}"

for name in api web; do
  pidfile="$RUN_DIR/$name.pid"
  if [ -f "$pidfile" ]; then
    pid="$(cat "$pidfile")"
    if kill -0 "$pid" 2>/dev/null; then
      # Kill the whole process tree (dotnet run / npm start spawn children).
      pkill -TERM -P "$pid" 2>/dev/null
      kill -TERM "$pid" 2>/dev/null
      echo "[kotlet-stop] stopped $name (pid $pid)"
    fi
    rm -f "$pidfile"
  fi
done

# Belt and braces: anything still bound to the app ports.
for port in "${KOTLET_API_PORT:-5249}" "${KOTLET_WEB_PORT:-4200}"; do
  fuser -k -TERM "$port/tcp" 2>/dev/null && echo "[kotlet-stop] freed port $port"
done
true
