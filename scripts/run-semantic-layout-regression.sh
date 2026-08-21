#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GROUND_TRUTH="$REPO/docs/evaluation/semantic-regression-ground-truth-v1.json"
FIXTURES="$REPO/tests/document_corpus/pdf/pages"
OUT="$REPO/scripts/tmp/semantic-layout-regression"
REPORT="$OUT/layout-report.json"
SERVICE_LOG="$OUT/ppstructurev3.log"
SERVING_BUILD="$OUT/serving-image"

BASE_IMAGE="document-processing-ppstructurev3:3.7.0-paddle3.2.2-cpu"
SERVING_IMAGE="document-processing-ppstructurev3-serving:3.7.0-paddle3.2.2-cpu"
MODEL_CACHE="$REPO/scripts/tmp/model-cache/ppstructurev3-3.7.0-paddle3.2.2"

MEMORY_LIMIT="12g"
MIN_AVAILABLE_MB="12288"

MODE=""
CONTAINER="dpe-semantic-layout-$RANDOM-$$"
CONTAINER_STARTED=false

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 2
}

cleanup() {
  local status=$?
  set +e

  if [[ "$CONTAINER_STARTED" == true ]]; then
    docker logs "$CONTAINER" >"$SERVICE_LOG" 2>&1 || true
    docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  fi

  exit "$status"
}

trap cleanup EXIT

usage() {
  cat <<'TXT'
Usage:
  scripts/run-semantic-layout-regression.sh --mode baseline
  scripts/run-semantic-layout-regression.sh --mode all-pass

Modes:
  baseline  Succeeds only when live semantic PASS/FAIL results reproduce the
            baseline classifications recorded in the independent ground-truth
            manifest. Used before remediation to prove known red controls exist.

  all-pass  Succeeds only when every evaluated semantic control satisfies the
            independent ground truth. Intended after remediation.
TXT
}

while (($# > 0)); do
  case "$1" in
    --mode)
      (($# >= 2)) ||
        fail "--mode requires a value."

      MODE="$2"
      shift 2
      ;;

    --help|-h)
      usage
      exit 0
      ;;

    *)
      fail "Unknown option: $1"
      ;;
  esac
done

case "$MODE" in
  baseline|all-pass)
    ;;

  *)
    fail "--mode must be 'baseline' or 'all-pass'."
    ;;
esac

for command in \
  docker curl dotnet python3 awk grep seq; do
  command -v "$command" >/dev/null 2>&1 ||
    fail "$command is required."
done

[[ -f "$GROUND_TRUTH" ]] ||
  fail "Ground-truth manifest is missing: $GROUND_TRUTH"

[[ -d "$FIXTURES" ]] ||
  fail "Fixture directory is missing: $FIXTURES"

mkdir -p \
  "$OUT" \
  "$SERVING_BUILD" \
  "$MODEL_CACHE"

rm -f \
  "$REPORT" \
  "$SERVICE_LOG"

printf 'DPEngine semantic layout regression\n'
printf 'Mode: %s\n' "$MODE"
printf 'Ground truth: %s\n' "$GROUND_TRUTH"
printf 'Fixtures: %s\n\n' "$FIXTURES"

printf '[1/5] Building EvaluationCli before model startup...\n'
dotnet build \
  "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  -warnaserror \
  --nologo

printf '\n[2/5] Verifying Docker/model residency...\n'
docker info >/dev/null 2>&1 ||
  fail "Docker daemon is unavailable."

RUNNING_MODEL_IMAGES="$(
  docker ps \
    --format '{{.Image}}' |
    grep -E \
      '^(document-processing-ppstructurev3-serving:|document-processing-paddleocr-serving:)' ||
    true
)"

if [[ -n "$RUNNING_MODEL_IMAGES" ]]; then
  printf '%s\n' "$RUNNING_MODEL_IMAGES" >&2
  fail "A DPE layout/OCR serving container is already running."
fi

available_kb="$(
  awk '/^MemAvailable:/ {print $2}' /proc/meminfo
)"

[[ "$available_kb" =~ ^[0-9]+$ ]] ||
  fail "Could not read MemAvailable."

available_mb=$(( available_kb / 1024 ))

printf 'Available memory: %s MiB\n' "$available_mb"

if (( available_mb < MIN_AVAILABLE_MB )); then
  fail "Need at least ${MIN_AVAILABLE_MB} MiB available for PP-StructureV3."
fi

printf '\n[3/5] Preparing pinned PP-StructureV3 serving image...\n'
if ! docker image inspect "$BASE_IMAGE" >/dev/null 2>&1; then
  docker build \
    --tag "$BASE_IMAGE" \
    "$REPO/tools/layout-benchmarks/ppstructurev3"
fi

if ! docker image inspect "$SERVING_IMAGE" >/dev/null 2>&1; then
  cat >"$SERVING_BUILD/Dockerfile" <<EOF
FROM $BASE_IMAGE
RUN paddlex --install serving
ENTRYPOINT ["paddlex"]
EOF

  docker build \
    --tag "$SERVING_IMAGE" \
    --file "$SERVING_BUILD/Dockerfile" \
    "$SERVING_BUILD"
fi

PORT="$(
  python3 - <<'PY'
import socket

with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
)"

ENDPOINT="http://127.0.0.1:${PORT}/layout-parsing"

docker run \
  --detach \
  --name "$CONTAINER" \
  --memory "$MEMORY_LIMIT" \
  --memory-swap "$MEMORY_LIMIT" \
  --shm-size=2g \
  --publish "127.0.0.1:${PORT}:8080" \
  --volume "$MODEL_CACHE:/root/.paddlex:Z" \
  "$SERVING_IMAGE" \
  --serve \
  --pipeline PP-StructureV3 \
  --device cpu \
  --host 0.0.0.0 \
  --port 8080 \
  >/dev/null

CONTAINER_STARTED=true

ready=false

for attempt in $(seq 1 240); do
  if ! docker inspect \
      --format '{{.State.Running}}' \
      "$CONTAINER" 2>/dev/null |
      grep -qx 'true'; then
    docker logs "$CONTAINER" >&2 || true
    fail "PP-StructureV3 exited during startup."
  fi

  if curl \
      --fail \
      --silent \
      --show-error \
      --max-time 5 \
      "http://127.0.0.1:${PORT}/openapi.json" \
      >/dev/null 2>&1; then
    ready=true
    break
  fi

  sleep 5
done

[[ "$ready" == true ]] ||
  fail "PP-StructureV3 did not become ready within 20 minutes."

printf 'PP-StructureV3 ready on port %s.\n' "$PORT"

printf '\n[4/5] Running live semantic layout evaluation...\n'
set +e

dotnet \
  "$REPO/tools/DocumentProcessing.EvaluationCli/bin/Release/net10.0/DocumentProcessing.EvaluationCli.dll" \
  evaluate-semantic-layout-regression \
  --ground-truth "$GROUND_TRUTH" \
  --fixtures "$FIXTURES" \
  --layout-endpoint "$ENDPOINT" \
  --report "$REPORT" \
  --mode "$MODE"

evaluation_status=$?

set -e

docker logs "$CONTAINER" >"$SERVICE_LOG" 2>&1 || true
docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
CONTAINER_STARTED=false

printf '\n[5/5] Result...\n'
[[ -f "$REPORT" ]] ||
  fail "Semantic layout evaluator produced no report."

printf 'Report: %s\n' "$REPORT"
printf 'PP log: %s\n' "$SERVICE_LOG"
printf 'Exit code: %s\n' "$evaluation_status"

exit "$evaluation_status"
