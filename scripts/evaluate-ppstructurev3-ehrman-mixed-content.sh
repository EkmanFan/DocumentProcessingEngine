#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/layout-0a-ppstructurev3-ehrman"
INPUT_DIR="${WORK_DIR}/inputs"

MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-mixed-content-v1.json"
STRUCTURE="${REPO}/docs/evaluation/corpora/ehrman-mixed-content-structure-v1.json"

IMAGE="document-processing-ppstructurev3:3.7.0-paddle3.2.2-cpu"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] ||
        fail "Missing value for --source."
      SOURCE="$2"
      shift 2
      ;;
    --help|-h)
      cat <<'HELP'
Usage:
  bash scripts/evaluate-ppstructurev3-ehrman-mixed-content.sh \
    --source /absolute/path/ehrman.pdf

Runs LAYOUT-0A with PP-StructureV3 only on physical PDF page 233.

Outputs:
  raw-result.json
  assessment.json
  annotated.png
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in docker python3 realpath; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

[[ -n "${SOURCE}" ]] ||
  fail "--source is required."

SOURCE="$(realpath "${SOURCE}")"

[[ -f "${SOURCE}" ]] ||
  fail "Source PDF not found: ${SOURCE}"

rm -rf "${WORK_DIR}"
mkdir -p \
  "${INPUT_DIR}" \
  "${WORK_DIR}/model-cache"

cd "${REPO}"

printf '\n== Rendering exact OCR-0H mixed-content page ==\n'

bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source "${SOURCE}" \
  --manifest "${MANIFEST}" \
  --output-dir "${INPUT_DIR}"

printf '\n== Building pinned PP-StructureV3 CPU image ==\n'

docker build \
  --pull \
  --tag "${IMAGE}" \
  tools/layout-benchmarks/ppstructurev3

printf '\n== Running PP-StructureV3 layout/parser ==\n'

docker run \
  --rm \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${WORK_DIR}/model-cache:/root/.paddlex:Z" \
  "${IMAGE}" \
  /workspace/tools/layout-benchmarks/ppstructurev3/run_ppstructurev3_benchmark.py \
  --input-index /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman/inputs/input-index.json \
  --input-dir /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman/inputs \
  --structure /workspace/docs/evaluation/corpora/ehrman-mixed-content-structure-v1.json \
  --raw-output /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman/raw-result.json \
  --assessment-output /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman/assessment.json \
  --annotated-output /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman/annotated.png

UID_NOW="$(id -u)"
GID_NOW="$(id -g)"

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${IMAGE}" \
  -lc "chown -R ${UID_NOW}:${GID_NOW} /workspace/scripts/tmp/layout-0a-ppstructurev3-ehrman"

printf '\nArtifacts:\n'
printf '  %s\n' "${WORK_DIR}/raw-result.json"
printf '  %s\n' "${WORK_DIR}/assessment.json"
printf '  %s\n' "${WORK_DIR}/annotated.png"
