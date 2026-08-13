#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0b-paddleocr"
IMAGE="document-processing-paddleocr:3.7.0-paddle3.2.2-cpu"

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
  bash scripts/evaluate-paddleocr-ppocrv6-cpu.sh \
    --source /absolute/path/ehrman.pdf

Runs the OCR-0B PaddleOCR CPU challenger against the fixed OCR-0A inputs.

Pinned stack:
  paddlepaddle/paddle:3.2.2
  paddleocr==3.7.0
  PP-OCRv6_medium_det
  PP-OCRv6_medium_rec

Generated artifacts live under scripts/tmp/ocr-0b-paddleocr/.
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in docker dotnet python3; do
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
  "${WORK_DIR}/inputs" \
  "${WORK_DIR}/model-cache"

cd "${REPO}"

bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source "${SOURCE}" \
  --manifest "${MANIFEST}" \
  --output-dir "${WORK_DIR}/inputs"

printf '\n== Building pinned PaddleOCR CPU benchmark image ==\n'

docker build \
  --pull \
  --tag "${IMAGE}" \
  tools/ocr-benchmarks/paddleocr

RESULT="${WORK_DIR}/paddleocr-result.json"
REPORT="${WORK_DIR}/paddleocr-evaluation.json"

printf '\n== Running PP-OCRv6 medium on fixed benchmark inputs ==\n'
printf 'Docker bind mounts use :Z for Fedora/SELinux compatibility.\n'

HOST_UID="$(id -u)"
HOST_GID="$(id -g)"

docker run \
  --rm \
  --volume "${REPO}:/workspace:Z" \
  --volume "${WORK_DIR}/model-cache:/root/.paddlex:Z" \
  "${IMAGE}" \
  --manifest "/workspace/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json" \
  --input-index "/workspace/scripts/tmp/ocr-0b-paddleocr/inputs/input-index.json" \
  --input-dir "/workspace/scripts/tmp/ocr-0b-paddleocr/inputs" \
  --output "/workspace/scripts/tmp/ocr-0b-paddleocr/paddleocr-result.json"

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${IMAGE}" \
  -lc "chown -R ${HOST_UID}:${HOST_GID} /workspace/scripts/tmp/ocr-0b-paddleocr"

printf '\n== Evaluating neutral PaddleOCR result ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-benchmark \
  --manifest "${MANIFEST}" \
  --input-index "${WORK_DIR}/inputs/input-index.json" \
  --result "${RESULT}" \
  --report "${REPORT}"

python3 - \
  "${REPORT}" <<'PY'
import json
import sys
from pathlib import Path

report = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))

coverage = report["coverage"]
raster = report["rasterReference"]
titles = report["outlineTitles"]
performance = report.get("performance") or {}

print()
print("RESULT: OCR-0B PADDLEOCR SUMMARY")
print(
    "  completed / failed / text pages: "
    f'{coverage["completedPages"]} / '
    f'{coverage["failedPages"]} / '
    f'{coverage["pagesWithText"]}'
)
print(
    "  raster reference recovered: "
    f'{raster["pagesWithText"]}/{raster["pageCount"]}'
)
print(
    "  raster chars / historical EasyOCR chars: "
    f'{raster["characterCount"]} / '
    f'{raster["historicalEasyOcrCharacterCount"]}'
)
print(
    "  outline plausible / exploratory / none: "
    f'{titles["plausibleMatches"]} / '
    f'{titles["exploratoryMatches"]} / '
    f'{titles["noCandidate"]}'
)
print(
    "  total elapsed ms: "
    f'{coverage["totalElapsedMilliseconds"]:.1f}'
)
print(
    "  startup ms: "
    f'{performance.get("startupMilliseconds")}'
)
print(
    "  peak process bytes: "
    f'{performance.get("processPeakWorkingSetBytes")}'
)

if coverage["failedPages"] != 0:
    print()
    print("QUALITY NOTE: one or more pages failed. OCR-0B is a challenger benchmark;")
    print("the script reports this result rather than hiding it.")

if raster["pagesWithText"] < raster["pageCount"]:
    print()
    print("QUALITY NOTE: PaddleOCR did not reach the historical 7/7 raster page-recovery gate.")
else:
    print()
    print("Raster page-recovery gate: PASS (7/7)")
PY

printf '\nArtifacts:\n'
printf '  %s\n' "${RESULT}"
printf '  %s\n' "${REPORT}"
