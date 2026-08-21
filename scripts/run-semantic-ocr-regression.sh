#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GROUND_TRUTH="$REPO/docs/evaluation/semantic-regression-ground-truth-v1.json"
FIXTURES="$REPO/tests/pdf_pages_test"
OUT="$REPO/scripts/tmp/semantic-ocr-regression"
STATE="$OUT/state"
TRANSITION="$OUT/transition"
SERVICE_LOGS="$OUT/service-logs"
SERVING_BUILD="$OUT/serving-images"

LAYOUT_BASE_IMAGE="document-processing-ppstructurev3:3.7.0-paddle3.2.2-cpu"
OCR_BASE_IMAGE="document-processing-paddleocr:3.7.0-paddle3.2.2-cpu"
LAYOUT_SERVING_IMAGE="document-processing-ppstructurev3-serving:3.7.0-paddle3.2.2-cpu"
OCR_SERVING_IMAGE="document-processing-paddleocr-serving:3.7.0-paddle3.2.2-cpu"

LAYOUT_CACHE="$REPO/scripts/tmp/model-cache/ppstructurev3-3.7.0-paddle3.2.2"
OCR_CACHE="$REPO/scripts/tmp/model-cache/paddleocr-3.7.0-paddle3.2.2"

OCR_PROFILE_ID="paddleocr-3.7.0-ppocrv6-medium-cpu-v1"

PDFTOPPM_EXECUTABLE="${PDFTOPPM_EXECUTABLE:-/usr/bin/pdftoppm}"
PDFTOPPM_REQUIRED_VERSION="26.01.0"

MODEL_MEMORY_LIMIT="12g"
MIN_AVAILABLE_MB="12288"

LAYOUT_CONTAINER=""
OCR_CONTAINER=""

fail() {
  printf '\nERROR: %s\n' "$*" >&2
  exit 2
}

cleanup() {
  set +e

  mkdir -p "$SERVICE_LOGS" >/dev/null 2>&1 || true

  if [[ -n "$LAYOUT_CONTAINER" ]] &&
      docker ps -a --format '{{.Names}}' 2>/dev/null |
      grep -Fx "$LAYOUT_CONTAINER" >/dev/null 2>&1; then
    docker logs "$LAYOUT_CONTAINER" \
      >"$SERVICE_LOGS/layout-final.log" 2>&1 || true
    docker rm -f "$LAYOUT_CONTAINER" >/dev/null 2>&1 || true
  fi

  if [[ -n "$OCR_CONTAINER" ]] &&
      docker ps -a --format '{{.Names}}' 2>/dev/null |
      grep -Fx "$OCR_CONTAINER" >/dev/null 2>&1; then
    docker logs "$OCR_CONTAINER" \
      >"$SERVICE_LOGS/ocr-final.log" 2>&1 || true
    docker rm -f "$OCR_CONTAINER" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

for command in \
  docker curl dotnet python3 awk grep seq; do
  command -v "$command" >/dev/null 2>&1 ||
    fail "$command is required."
done

[[ "$PDFTOPPM_EXECUTABLE" = /* ]] ||
  fail "PDFTOPPM_EXECUTABLE must be an absolute path."

[[ -x "$PDFTOPPM_EXECUTABLE" ]] ||
  fail "The required pdftoppm executable is unavailable: $PDFTOPPM_EXECUTABLE"

observed_pdftoppm_version="$(
  "$PDFTOPPM_EXECUTABLE" -v 2>&1 |
    awk 'NR == 1 { print $3 }'
)"

[[ "$observed_pdftoppm_version" == "$PDFTOPPM_REQUIRED_VERSION" ]] ||
  fail "pdftoppm $PDFTOPPM_REQUIRED_VERSION is required for exact PNG output; observed $observed_pdftoppm_version at $PDFTOPPM_EXECUTABLE"

export PATH="${PDFTOPPM_EXECUTABLE%/*}:$PATH"

[[ "$(command -v pdftoppm)" == "$PDFTOPPM_EXECUTABLE" ]] ||
  fail "The pinned pdftoppm executable is not first on PATH."

[[ -f "$GROUND_TRUTH" ]] ||
  fail "Ground-truth manifest is missing: $GROUND_TRUTH"

[[ -d "$FIXTURES" ]] ||
  fail "Fixture directory is missing: $FIXTURES"

rm -rf "$OUT"
mkdir -p \
  "$STATE" \
  "$TRANSITION" \
  "$SERVICE_LOGS" \
  "$SERVING_BUILD" \
  "$LAYOUT_CACHE" \
  "$OCR_CACHE"

printf 'DPEngine semantic real-OCR regression\n'
printf 'Ground truth: %s\n' "$GROUND_TRUTH"
printf 'Controls: Ehrman p233 / p380 / p405\n'
printf 'Model residency: PP-StructureV3 then PaddleOCR, never concurrent\n\n'
printf 'Pinned rasterizer: %s (%s)\n\n' \
  "$PDFTOPPM_EXECUTABLE" \
  "$observed_pdftoppm_version"

printf '[1/5] Building EvaluationCli before model startup...\n'
dotnet build \
  "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  -warnaserror \
  --nologo

printf '\n[2/5] Preparing pinned serving images...\n'
docker info >/dev/null 2>&1 ||
  fail "Docker daemon is unavailable."

if ! docker image inspect "$LAYOUT_BASE_IMAGE" >/dev/null 2>&1; then
  docker build \
    --tag "$LAYOUT_BASE_IMAGE" \
    "$REPO/tools/layout-benchmarks/ppstructurev3"
fi

if ! docker image inspect "$OCR_BASE_IMAGE" >/dev/null 2>&1; then
  docker build \
    --tag "$OCR_BASE_IMAGE" \
    "$REPO/tools/ocr-benchmarks/paddleocr"
fi

if ! docker image inspect "$LAYOUT_SERVING_IMAGE" >/dev/null 2>&1; then
  cat >"$SERVING_BUILD/Dockerfile.layout" <<EOF
FROM $LAYOUT_BASE_IMAGE
RUN paddlex --install serving
ENTRYPOINT ["paddlex"]
EOF

  docker build \
    --tag "$LAYOUT_SERVING_IMAGE" \
    --file "$SERVING_BUILD/Dockerfile.layout" \
    "$SERVING_BUILD"
fi

if ! docker image inspect "$OCR_SERVING_IMAGE" >/dev/null 2>&1; then
  cat >"$SERVING_BUILD/Dockerfile.ocr" <<EOF
FROM $OCR_BASE_IMAGE
RUN paddlex --install serving
ENTRYPOINT ["paddlex"]
EOF

  docker build \
    --tag "$OCR_SERVING_IMAGE" \
    --file "$SERVING_BUILD/Dockerfile.ocr" \
    "$SERVING_BUILD"
fi

assert_no_model_services() {
  local running_images

  running_images="$(
    docker ps \
      --format '{{.Image}}' \
      2>/dev/null || true
  )"

  if printf '%s\n' "$running_images" |
      grep -E \
        "^(${LAYOUT_SERVING_IMAGE}|${OCR_SERVING_IMAGE})$" \
        >/dev/null 2>&1; then
    printf '%s\n' "$running_images" >&2
    fail "A DPE layout/OCR serving container is already running."
  fi
}

require_available_memory() {
  local label="$1"
  local available_kb available_mb

  available_kb="$(
    awk '/^MemAvailable:/ {print $2}' /proc/meminfo
  )"

  [[ "$available_kb" =~ ^[0-9]+$ ]] ||
    fail "Could not read MemAvailable before $label."

  available_mb=$(( available_kb / 1024 ))

  printf 'Available memory before %s: %s MiB\n' \
    "$label" \
    "$available_mb"

  if (( available_mb < MIN_AVAILABLE_MB )); then
    fail "$label requires at least ${MIN_AVAILABLE_MB} MiB available."
  fi
}

allocate_port() {
  python3 - <<'PY'
import socket

with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
}

wait_for_service() {
  local container="$1"
  local port="$2"
  local label="$3"

  local attempt

  for attempt in $(seq 1 240); do
    if ! docker inspect \
        --format '{{.State.Running}}' \
        "$container" 2>/dev/null |
        grep -qx 'true'; then
      docker inspect \
        --format 'status={{.State.Status}} exit={{.State.ExitCode}} oomKilled={{.State.OOMKilled}} error={{.State.Error}}' \
        "$container" >&2 || true
      docker logs --tail 120 "$container" >&2 || true
      fail "$label container stopped before readiness."
    fi

    if curl \
        --fail \
        --silent \
        --show-error \
        --max-time 5 \
        "http://127.0.0.1:${port}/openapi.json" \
        >/dev/null 2>&1; then
      printf '%s ready on port %s.\n' "$label" "$port"
      return
    fi

    sleep 5
  done

  docker logs --tail 120 "$container" >&2 || true
  fail "$label did not become ready within 20 minutes."
}

stop_layout() {
  local log_path="$1"

  if [[ -n "$LAYOUT_CONTAINER" ]] &&
      docker ps -a --format '{{.Names}}' |
      grep -Fx "$LAYOUT_CONTAINER" >/dev/null 2>&1; then
    docker inspect "$LAYOUT_CONTAINER" \
      >"${log_path%.log}-inspect.json" 2>&1 || true
    docker logs "$LAYOUT_CONTAINER" \
      >"$log_path" 2>&1 || true
    docker rm -f "$LAYOUT_CONTAINER" >/dev/null 2>&1 || true
  fi

  LAYOUT_CONTAINER=""
}

stop_ocr() {
  local log_path="$1"

  if [[ -n "$OCR_CONTAINER" ]] &&
      docker ps -a --format '{{.Names}}' |
      grep -Fx "$OCR_CONTAINER" >/dev/null 2>&1; then
    docker inspect "$OCR_CONTAINER" \
      >"${log_path%.log}-inspect.json" 2>&1 || true
    docker logs "$OCR_CONTAINER" \
      >"$log_path" 2>&1 || true
    docker rm -f "$OCR_CONTAINER" >/dev/null 2>&1 || true
  fi

  OCR_CONTAINER=""
}

start_layout() {
  local case_id="$1"
  local port="$2"

  assert_no_model_services
  require_available_memory \
    "PP-StructureV3 $case_id"

  LAYOUT_CONTAINER="dpe-semantic-ocr-${case_id}-layout-$RANDOM-$$"

  docker run \
    --detach \
    --name "$LAYOUT_CONTAINER" \
    --memory "$MODEL_MEMORY_LIMIT" \
    --memory-swap "$MODEL_MEMORY_LIMIT" \
    --shm-size=2g \
    --publish "127.0.0.1:${port}:8080" \
    --volume "$LAYOUT_CACHE:/root/.paddlex:Z" \
    "$LAYOUT_SERVING_IMAGE" \
    --serve \
    --pipeline PP-StructureV3 \
    --device cpu \
    --host 0.0.0.0 \
    --port 8080 \
    >/dev/null

  wait_for_service \
    "$LAYOUT_CONTAINER" \
    "$port" \
    "PP-StructureV3/$case_id"
}

start_ocr() {
  local case_id="$1"
  local port="$2"

  assert_no_model_services
  require_available_memory \
    "PaddleOCR $case_id"

  OCR_CONTAINER="dpe-semantic-ocr-${case_id}-ocr-$RANDOM-$$"

  docker run \
    --detach \
    --name "$OCR_CONTAINER" \
    --memory "$MODEL_MEMORY_LIMIT" \
    --memory-swap "$MODEL_MEMORY_LIMIT" \
    --shm-size=2g \
    --publish "127.0.0.1:${port}:8080" \
    --volume "$OCR_CACHE:/root/.paddlex:Z" \
    "$OCR_SERVING_IMAGE" \
    --serve \
    --pipeline OCR \
    --device cpu \
    --host 0.0.0.0 \
    --port 8080 \
    >/dev/null

  wait_for_service \
    "$OCR_CONTAINER" \
    "$port" \
    "PaddleOCR/$case_id"
}

wait_for_layout_marker_or_exit() {
  local marker="$1"
  local pid="$2"
  local console_log="$3"
  local case_id="$4"

  local attempt

  for attempt in $(seq 1 1800); do
    if [[ -f "$marker" ]]; then
      printf '%s layout complete; switching model residency.\n' "$case_id"
      return 0
    fi

    if ! kill -0 "$pid" >/dev/null 2>&1; then
      wait "$pid" || true
      printf '\n%s evaluator exited before layout handoff:\n' "$case_id" >&2
      cat "$console_log" >&2 || true
      return 1
    fi

    sleep 1
  done

  printf '\n%s timed out waiting for layout marker.\n' "$case_id" >&2
  cat "$console_log" >&2 || true
  return 1
}

run_case() {
  local control_id="$1"
  local fixture_name="$2"

  local fixture="$FIXTURES/$fixture_name"
  local case_transition="$TRANSITION/$control_id"
  local report="$STATE/${control_id}.json"
  local console_log="$STATE/${control_id}-console.log"
  local layout_log="$SERVICE_LOGS/${control_id}-layout.log"
  local ocr_log="$SERVICE_LOGS/${control_id}-ocr.log"

  [[ -f "$fixture" ]] ||
    fail "Fixture is missing: $fixture"

  rm -rf "$case_transition"
  mkdir -p "$case_transition"

  local layout_marker="$case_transition/layout-complete"
  local ocr_ready_marker="$case_transition/ocr-ready"

  local layout_port ocr_port
  layout_port="$(allocate_port)"
  ocr_port="$(allocate_port)"

  printf '\n--- %s ---\n' "$control_id"

  start_layout \
    "$control_id" \
    "$layout_port"

  dotnet \
    "$REPO/tools/DocumentProcessing.EvaluationCli/bin/Release/net10.0/DocumentProcessing.EvaluationCli.dll" \
    evaluate-semantic-ocr-regression \
    --control "$control_id" \
    --ground-truth "$GROUND_TRUTH" \
    --fixture "$fixture" \
    --layout-endpoint "http://127.0.0.1:${layout_port}/layout-parsing" \
    --ocr-endpoint "http://127.0.0.1:${ocr_port}/ocr" \
    --ocr-profile "$OCR_PROFILE_ID" \
    --layout-complete-marker "$layout_marker" \
    --ocr-ready-marker "$ocr_ready_marker" \
    --report "$report" \
    >"$console_log" 2>&1 &

  local evaluator_pid=$!

  if ! wait_for_layout_marker_or_exit \
      "$layout_marker" \
      "$evaluator_pid" \
      "$console_log" \
      "$control_id"; then
    stop_layout \
      "$SERVICE_LOGS/${control_id}-layout-failed.log"
    fail "$control_id failed before OCR handoff."
  fi

  stop_layout \
    "$layout_log"

  assert_no_model_services

  start_ocr \
    "$control_id" \
    "$ocr_port"

  printf 'ocr-ready\n' >"$ocr_ready_marker"

  if ! wait "$evaluator_pid"; then
    printf '\n%s semantic OCR evaluator failed:\n' \
      "$control_id" >&2

    cat "$console_log" >&2 || true

    stop_ocr \
      "$SERVICE_LOGS/${control_id}-ocr-failed.log"

    fail "$control_id semantic OCR regression failed."
  fi

  cat "$console_log"

  stop_ocr \
    "$ocr_log"

  assert_no_model_services

  [[ -f "$report" ]] ||
    fail "$control_id produced no semantic OCR report."
}

printf '\n[3/5] Running permanent real-OCR controls...\n'

run_case \
  "ehrman-p233" \
  "ehrman-p0233.pdf"

run_case \
  "ehrman-p380" \
  "ehrman-p0380.pdf"

run_case \
  "ehrman-p405" \
  "ehrman-p0405.pdf"

printf '\n[4/5] Verifying persisted semantic reports...\n'

python3 - \
  "$STATE/ehrman-p233.json" \
  "$STATE/ehrman-p380.json" \
  "$STATE/ehrman-p405.json" \
  "$STATE/summary.json" <<'PY'
from pathlib import Path
import json
import sys

paths = [Path(value) for value in sys.argv[1:4]]
summary_path = Path(sys.argv[4])

rows = [
    json.loads(
        path.read_text(
            encoding="utf-8"))
    for path in paths
]

by_id = {
    row["controlId"]: row
    for row in rows
}

expected_ids = {
    "ehrman-p233",
    "ehrman-p380",
    "ehrman-p405",
}

if set(by_id) != expected_ids:
    raise SystemExit(
        f"Unexpected semantic OCR controls: {sorted(by_id)}")

for control_id, row in by_id.items():
    if row["schemaVersion"] != "document-processing-semantic-ocr-regression-v1":
        raise SystemExit(
            f"{control_id}: unexpected report schema.")

    if row["pass"] is not True:
        raise SystemExit(
            f"{control_id}: semantic OCR control is not PASS.")

    if row["figureOcrCount"] != 0:
        raise SystemExit(
            f"{control_id}: Figure OCR is not zero.")

p233 = by_id["ehrman-p233"]

if p233["route"] != "LayoutWithTargetedOcrRecovery":
    raise SystemExit(
        "p233 route changed.")

if p233["readingOrder"] is None or p233["readingOrder"]["pass"] is not True:
    raise SystemExit(
        "p233 reading-order oracle failed.")

preserved = p233["preservedVisual"]

if preserved is None:
    raise SystemExit(
        "p233 preserved visual report is missing.")

if (
    preserved["width"] != 841
    or preserved["height"] != 1398
    or preserved["bytes"] != 1505768
    or preserved["sha256"]
    != "c4170e36da6d0bfdec419f8db199ba972baf3075887a264aa2e9e4d46e6e4e77"
):
    raise SystemExit(
        "p233 exact visual custody changed.")

p380 = by_id["ehrman-p380"]["reconciliation"]

if p380 is None:
    raise SystemExit(
        "p380 reconciliation report is missing.")

if (
    p380["decision"] != "Conflict"
    or p380["selectedOrigin"] != "None"
    or p380["resolved"] is not False
    or p380["divergence"] is not True
    or p380["nativeBlockSourceSequence"] != 2
):
    raise SystemExit(
        "p380 reconciliation oracle failed.")

p405 = by_id["ehrman-p405"]["reconciliation"]

if p405 is None:
    raise SystemExit(
        "p405 reconciliation report is missing.")

if (
    p405["decision"] != "Agreement"
    or p405["selectedOrigin"] != "Native"
    or p405["resolved"] is not True
    or p405["divergence"] is not False
    or p405["nativeBlockSourceSequence"] != 6
):
    raise SystemExit(
        "p405 reconciliation oracle failed.")

summary = {
    "schemaVersion":
        "document-processing-semantic-ocr-regression-summary-v1",
    "pass":
        True,
    "controls":
        rows,
}

summary_path.write_text(
    json.dumps(
        summary,
        indent=2,
        ensure_ascii=False) +
    "\n",
    encoding="utf-8")

print("REAL OCR SEMANTIC ACCEPTANCE: PASS")
print("  p233 targeted OCR recovery + exact visual + reading order: PASS")
print("  p380 Conflict/None/unresolved: PASS")
print("  p405 Agreement/Native/resolved: PASS")
print("  Figure OCR: 0 across all three controls")
print(f"  summary={summary_path}")
PY

printf '\n[5/5] Result...\n'
printf 'Semantic OCR regression: PASS\n'
printf 'Reports: %s\n' "$STATE"
printf 'Service logs: %s\n' "$SERVICE_LOGS"
