#!/usr/bin/env bash
set -Eeuo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOST_PROJECT="${REPO_ROOT}/src/DocumentProcessing.Manager.Host/DocumentProcessing.Manager.Host.csproj"
UI_PROJECT="${REPO_ROOT}/src/DocumentProcessing.Manager.Blazor/DocumentProcessing.Manager.Blazor.csproj"

POSTGRES_CONTAINER="${DPE_MANAGER_POSTGRES_CONTAINER:-dpengine-manager-postgres-dev}"
POSTGRES_IMAGE="${DPE_MANAGER_POSTGRES_IMAGE:-postgres:18.4-alpine}"
POSTGRES_VOLUME="${DPE_MANAGER_POSTGRES_VOLUME:-dpengine-manager-postgres18-data}"
POSTGRES_DATA_ROOT="${DPE_MANAGER_POSTGRES_DATA_ROOT:-/var/lib/postgresql}"
POSTGRES_PORT="${DPE_MANAGER_POSTGRES_PORT:-5432}"
POSTGRES_DATABASE="${DPE_MANAGER_POSTGRES_DATABASE:-dpengine_manager}"
POSTGRES_USER="${DPE_MANAGER_POSTGRES_USER:-dpengine}"
POSTGRES_PASSWORD="${DPE_MANAGER_POSTGRES_PASSWORD:-dpengine-dev}"

HOST_PORT="${DPE_MANAGER_HOST_PORT:-5080}"
UI_PORT="${DPE_MANAGER_UI_PORT:-5092}"
HOST_URL="http://127.0.0.1:${HOST_PORT}"
UI_URL="http://127.0.0.1:${UI_PORT}"
API_KEY="${DPE_MANAGER_API_KEY:-dpengine-manager-local-development-key-2026}"
CUSTODY_ROOT="${DPE_MANAGER_CUSTODY_ROOT:-${REPO_ROOT}/tests/document_manager_custody}"
SOURCE_ROOT="${CUSTODY_ROOT}/sources"
RESULT_ROOT="${CUSTODY_ROOT}/results"

HOST_PID=""
UI_PID=""

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

require_positive_port() {
  local name="$1"
  local value="$2"

  [[ "$value" =~ ^[0-9]+$ ]] || fail "$name must be a positive TCP port."
  ((value >= 1 && value <= 65535)) || fail "$name must be between 1 and 65535."
}

stop_process() {
  local process_id="$1"

  if [[ -n "$process_id" ]] && kill -0 "$process_id" 2>/dev/null; then
    kill "$process_id" 2>/dev/null || true
  fi

  if [[ -n "$process_id" ]]; then
    wait "$process_id" 2>/dev/null || true
  fi
}

cleanup() {
  local status=$?

  trap - EXIT INT TERM
  stop_process "$UI_PID"
  stop_process "$HOST_PID"

  printf '\nManager Host and Blazor UI stopped.\n'
  printf 'PostgreSQL container %s remains available for the next run.\n' \
    "$POSTGRES_CONTAINER"

  exit "$status"
}

wait_for_postgres() {
  local attempt

  for ((attempt = 1; attempt <= 60; attempt++)); do
    if docker exec "$POSTGRES_CONTAINER" \
      pg_isready \
      --host 127.0.0.1 \
      --port 5432 \
      --username "$POSTGRES_USER" \
      --dbname "$POSTGRES_DATABASE" >/dev/null 2>&1; then
      return
    fi

    if [[ "$(docker inspect --format '{{.State.Running}}' "$POSTGRES_CONTAINER")" != "true" ]]; then
      fail "PostgreSQL container stopped before becoming ready: $POSTGRES_CONTAINER"
    fi

    sleep 1
  done

  fail "PostgreSQL did not become ready within 60 seconds."
}

wait_for_http() {
  local service_name="$1"
  local process_id="$2"
  local health_url="$3"
  local attempt

  for ((attempt = 1; attempt <= 120; attempt++)); do
    if curl --fail --silent --show-error "$health_url" >/dev/null 2>&1; then
      return
    fi

    if ! kill -0 "$process_id" 2>/dev/null; then
      wait "$process_id" 2>/dev/null || true
      fail "$service_name stopped before becoming ready."
    fi

    sleep 1
  done

  fail "$service_name did not become ready within 120 seconds."
}

resolve_postgres_port() {
  local published_endpoint

  published_endpoint="$(
    docker port "$POSTGRES_CONTAINER" 5432/tcp 2>/dev/null |
      tail -n 1
  )"

  [[ -n "$published_endpoint" ]] ||
    fail "PostgreSQL container does not publish port 5432: $POSTGRES_CONTAINER"

  POSTGRES_PORT="${published_endpoint##*:}"
  require_positive_port "Published PostgreSQL port" "$POSTGRES_PORT"
}

for command in curl docker dotnet; do
  command -v "$command" >/dev/null 2>&1 || fail "$command was not found."
done

docker info >/dev/null 2>&1 ||
  fail "Docker is unavailable. Start Docker and ensure the current user can access it."

[[ -f "$HOST_PROJECT" ]] || fail "Manager Host project not found: $HOST_PROJECT"
[[ -f "$UI_PROJECT" ]] || fail "Manager Blazor project not found: $UI_PROJECT"
[[ "${#API_KEY}" -ge 32 ]] || fail "DPE_MANAGER_API_KEY must contain at least 32 characters."
[[ "$POSTGRES_DATA_ROOT" == /* ]] || fail "DPE_MANAGER_POSTGRES_DATA_ROOT must be an absolute container path."

require_positive_port "DPE_MANAGER_POSTGRES_PORT" "$POSTGRES_PORT"
require_positive_port "DPE_MANAGER_HOST_PORT" "$HOST_PORT"
require_positive_port "DPE_MANAGER_UI_PORT" "$UI_PORT"

[[ "$HOST_PORT" != "$UI_PORT" ]] || fail "Host and Blazor UI ports must be distinct."

mkdir -p "$SOURCE_ROOT" "$RESULT_ROOT"

printf 'DPEngine Manager development launcher\n'
printf '=====================================\n\n'

if docker container inspect "$POSTGRES_CONTAINER" >/dev/null 2>&1; then
  if [[ "$(docker inspect --format '{{.State.Running}}' "$POSTGRES_CONTAINER")" == "true" ]]; then
    printf 'PostgreSQL container is already running: %s\n' "$POSTGRES_CONTAINER"
  else
    printf 'Starting existing PostgreSQL container: %s\n' "$POSTGRES_CONTAINER"
    docker start "$POSTGRES_CONTAINER" >/dev/null
  fi
else
  printf 'Creating PostgreSQL container: %s\n' "$POSTGRES_CONTAINER"
  docker run \
    --name "$POSTGRES_CONTAINER" \
    --detach \
    --publish "127.0.0.1:${POSTGRES_PORT}:5432" \
    --env "POSTGRES_DB=${POSTGRES_DATABASE}" \
    --env "POSTGRES_USER=${POSTGRES_USER}" \
    --env "POSTGRES_PASSWORD=${POSTGRES_PASSWORD}" \
    --volume "${POSTGRES_VOLUME}:${POSTGRES_DATA_ROOT}" \
    "$POSTGRES_IMAGE" >/dev/null
fi

resolve_postgres_port
wait_for_postgres

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

CONNECTION_STRING="Host=127.0.0.1;Port=${POSTGRES_PORT};Database=${POSTGRES_DATABASE};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

printf 'PostgreSQL ready on 127.0.0.1:%s.\n' "$POSTGRES_PORT"
printf 'Starting Manager Host on %s...\n' "$HOST_URL"

env \
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="$HOST_URL" \
  ConnectionStrings__ManagerPostgres="$CONNECTION_STRING" \
  ManagerHost__ApiKey="$API_KEY" \
  ManagerHost__SourceRoot="$SOURCE_ROOT" \
  ManagerHost__ResultRoot="$RESULT_ROOT" \
  dotnet run \
    --project "$HOST_PROJECT" \
    --no-launch-profile &
HOST_PID=$!

wait_for_http \
  "Manager Host" \
  "$HOST_PID" \
  "${HOST_URL}/health/ready"

printf 'Manager Host ready.\n'
printf 'Starting Blazor UI on %s...\n' "$UI_URL"

env \
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="$UI_URL" \
  ManagerApi__BaseAddress="$HOST_URL" \
  ManagerApi__ApiKey="$API_KEY" \
  dotnet run \
    --project "$UI_PROJECT" \
    --no-launch-profile &
UI_PID=$!

wait_for_http \
  "Manager Blazor UI" \
  "$UI_PID" \
  "$UI_URL"

printf '\nManager is ready.\n'
printf 'Open: %s\n' "$UI_URL"
printf 'Press Ctrl+C to stop the Host and UI.\n\n'

set +e
wait -n "$HOST_PID" "$UI_PID"
PROCESS_STATUS=$?
set -e

fail "A Manager process stopped unexpectedly with exit code $PROCESS_STATUS."
