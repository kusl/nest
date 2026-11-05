#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1

port=5080
work=/work
app=/tmp/app

mkdir -p "$work"
tar -C /src \
    --exclude=./.git --exclude=./artifacts \
    --exclude='*/bin' --exclude='*/obj' --exclude='*/node_modules' \
    -cf - . | tar -C "$work" -xf -
cd "$work"

dotnet publish src/Nest.Web --configuration Release --output "$app"

env \
    ASPNETCORE_URLS="http://127.0.0.1:$port" \
    ASPNETCORE_ENVIRONMENT=Production \
    Nest__PostBurst=1000 \
    Nest__PostsPerSecond=1000 \
    Nest__MaxConnectionsPerClient=50 \
    Nest__RequestBurst=100000 \
    Nest__RequestsPerSecond=100000 \
    dotnet "$app/Nest.Web.dll" >/out/app.log 2>&1 &
server=$!
trap 'kill "$server" 2>/dev/null || true' EXIT

for _ in $(seq 1 60); do
    if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
        break
    fi
    if ! kill -0 "$server" 2>/dev/null; then
        printf 'error: the app exited during startup\n' >&2
        cat /out/app.log >&2
        exit 1
    fi
    sleep 1
done

status=0
NEST_BASE_URL="http://127.0.0.1:$port/" \
    dotnet test --project tests/Nest.E2E --configuration Release --results-directory /out/results || status=$?

if [ "$status" -ne 0 ]; then
    printf '\n==> Last 50 lines of the app log\n' >&2
    tail -n 50 /out/app.log >&2
fi

exit "$status"
