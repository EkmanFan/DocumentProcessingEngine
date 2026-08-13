#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0e-v2-zone-crops"

MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json"
GROUND_TRUTH="${REPO}/docs/evaluation/corpora/ehrman-ocr-ground-truth-v1.json"

PADDLE_IMAGE="document-processing-paddleocr:3.7.0-paddle3.2.2-cpu"
DOCTR_IMAGE="document-processing-doctr:1.0.1-torch2.8.0-cpu"

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
  bash scripts/evaluate-ocr-zone-crops.sh \
    --source /absolute/path/ehrman.pdf

Runs OCR-0E v2.

The seven OCR-0D rectangles are cropped exactly once from the committed OCR-0A
300-DPI page renders. PaddleOCR and docTR then OCR the same crop PNG bytes.

This is an evaluation-only comparison and does not modify production code.
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in docker dotnet python3 realpath; do
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
  "${WORK_DIR}/crops" \
  "${WORK_DIR}/paddle-model-cache" \
  "${WORK_DIR}/doctr-model-cache"

cd "${REPO}"

printf '\n== Building EvaluationCli ==\n'
dotnet build DocumentProcessingEngine.sln -warnaserror

printf '\n== Rendering exact OCR-0A benchmark pages ==\n'

bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source "${SOURCE}" \
  --manifest "${MANIFEST}" \
  --output-dir "${WORK_DIR}/inputs"

printf '\n== Building pinned OCR images ==\n'

docker build \
  --pull \
  --tag "${PADDLE_IMAGE}" \
  tools/ocr-benchmarks/paddleocr

docker build \
  --pull \
  --tag "${DOCTR_IMAGE}" \
  tools/ocr-benchmarks/doctr

UID_NOW="$(id -u)"
GID_NOW="$(id -g)"

printf '\n== Preparing the seven crop PNGs exactly once ==\n'

docker run \
  --rm \
  --user "${UID_NOW}:${GID_NOW}" \
  --env HOME=/home/benchmark \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  "${DOCTR_IMAGE}" \
  /workspace/tools/ocr-benchmarks/zone-crops/run_zone_crop_benchmark.py \
  prepare \
  --ground-truth /workspace/docs/evaluation/corpora/ehrman-ocr-ground-truth-v1.json \
  --input-index /workspace/scripts/tmp/ocr-0e-v2-zone-crops/inputs/input-index.json \
  --input-dir /workspace/scripts/tmp/ocr-0e-v2-zone-crops/inputs \
  --crop-dir /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crops \
  --crop-index /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crop-index.json

PADDLE_RESULT="${WORK_DIR}/paddle-zone-crop-result.json"
PADDLE_REPORT="${WORK_DIR}/paddle-zone-crop-ground-truth.json"
DOCTR_RESULT="${WORK_DIR}/doctr-zone-crop-result.json"
DOCTR_REPORT="${WORK_DIR}/doctr-zone-crop-ground-truth.json"

printf '\n== Running PaddleOCR on the exact crop PNGs ==\n'

docker run \
  --rm \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${WORK_DIR}/paddle-model-cache:/root/.paddlex:Z" \
  "${PADDLE_IMAGE}" \
  /workspace/tools/ocr-benchmarks/zone-crops/run_zone_crop_benchmark.py \
  run \
  --engine paddleocr \
  --ground-truth /workspace/docs/evaluation/corpora/ehrman-ocr-ground-truth-v1.json \
  --crop-index /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crop-index.json \
  --crop-dir /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crops \
  --output /workspace/scripts/tmp/ocr-0e-v2-zone-crops/paddle-zone-crop-result.json

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${PADDLE_IMAGE}" \
  -lc "chown -R ${UID_NOW}:${GID_NOW} /workspace/scripts/tmp/ocr-0e-v2-zone-crops"

printf '\n== Evaluating PaddleOCR crop output against committed OCR-0D ground truth ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-ground-truth \
  --ground-truth "${GROUND_TRUTH}" \
  --result "${PADDLE_RESULT}" \
  --report "${PADDLE_REPORT}"

printf '\n== Running docTR on the SAME crop PNGs ==\n'

docker run \
  --rm \
  --user "${UID_NOW}:${GID_NOW}" \
  --env HOME=/home/benchmark \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${WORK_DIR}/doctr-model-cache:/home/benchmark/.cache/doctr:Z" \
  "${DOCTR_IMAGE}" \
  /workspace/tools/ocr-benchmarks/zone-crops/run_zone_crop_benchmark.py \
  run \
  --engine doctr \
  --ground-truth /workspace/docs/evaluation/corpora/ehrman-ocr-ground-truth-v1.json \
  --crop-index /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crop-index.json \
  --crop-dir /workspace/scripts/tmp/ocr-0e-v2-zone-crops/crops \
  --output /workspace/scripts/tmp/ocr-0e-v2-zone-crops/doctr-zone-crop-result.json

printf '\n== Evaluating docTR crop output against committed OCR-0D ground truth ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-ground-truth \
  --ground-truth "${GROUND_TRUTH}" \
  --result "${DOCTR_RESULT}" \
  --report "${DOCTR_REPORT}"

python3 - \
  "${WORK_DIR}/crop-index.json" \
  "${PADDLE_REPORT}" \
  "${DOCTR_REPORT}" <<'PY'
import json
import sys
from pathlib import Path

crop_index = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
paddle = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
doctr = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))

def pct(value):
    return f"{value * 100:.3f}%"

print()
print("RESULT: OCR-0E V2 ZONE-CROP COMPARISON")
print(f'Exact shared crop files: {len(crop_index["crops"])}')
print()
print(
    "PaddleOCR crops: "
    f'CER={pct(paddle["characterErrorRate"])} '
    f'WER={pct(paddle["wordErrorRate"])}'
)
print(
    "docTR crops:     "
    f'CER={pct(doctr["characterErrorRate"])} '
    f'WER={pct(doctr["wordErrorRate"])}'
)
print()
print(
    "docTR - PaddleOCR: "
    f'CER delta={(doctr["characterErrorRate"] - paddle["characterErrorRate"]) * 100:+.3f} pp, '
    f'WER delta={(doctr["wordErrorRate"] - paddle["wordErrorRate"]) * 100:+.3f} pp'
)
print()
print("Committed full-page OCR-0D observations:")
print("  PaddleOCR full-page: CER=2.953%  WER=4.354%")
print("  docTR full-page:     CER=37.972% WER=39.185%")
print()
print("Per-zone crop CER / WER:")
paddle_by_id = {zone["id"]: zone for zone in paddle["zones"]}
doctr_by_id = {zone["id"]: zone for zone in doctr["zones"]}

for crop in crop_index["crops"]:
    zone_id = crop["zoneId"]
    p = paddle_by_id[zone_id]
    d = doctr_by_id[zone_id]
    print(
        f'  {zone_id}: '
        f'Paddle {pct(p["characterErrorRate"])}/{pct(p["wordErrorRate"])} | '
        f'docTR {pct(d["characterErrorRate"])}/{pct(d["wordErrorRate"])}'
    )

print()
print("Interpretation:")
print("  These numbers remove full-page column mixing and geometric zone selection.")
print("  They still measure each engine's detector + recognizer + local reconstruction")
print("  inside the isolated crop; they are not a pure recognizer-only benchmark.")
PY

printf '\nArtifacts:\n'
printf '  %s\n' "${WORK_DIR}/crop-index.json"
printf '  %s\n' "${PADDLE_RESULT}"
printf '  %s\n' "${PADDLE_REPORT}"
printf '  %s\n' "${DOCTR_RESULT}"
printf '  %s\n' "${DOCTR_REPORT}"
