#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0h-ehrman-mixed-content"

MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-mixed-content-v1.json"
GROUND_TRUTH="${REPO}/docs/evaluation/corpora/ehrman-mixed-content-ground-truth-v1.json"
STRUCTURE="${REPO}/docs/evaluation/corpora/ehrman-mixed-content-structure-v1.json"

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
  bash scripts/evaluate-ehrman-mixed-content.sh \
    --source /absolute/path/ehrman.pdf

Runs OCR-0H on physical PDF page 233 / printed page 202.

The ancient papyrus facsimile is measured as untrusted OCR evidence and is not
included in modern narrative CER/WER ground truth.
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
  "${WORK_DIR}/model-cache"

cd "${REPO}"

printf '\n== Building EvaluationCli ==\n'
dotnet build DocumentProcessingEngine.sln -warnaserror

printf '\n== Rendering exact mixed-content page at benchmark DPI ==\n'

bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source "${SOURCE}" \
  --manifest "${MANIFEST}" \
  --output-dir "${WORK_DIR}/inputs"

printf '\n== Building pinned PaddleOCR CPU image ==\n'

docker build \
  --pull \
  --tag "${IMAGE}" \
  tools/ocr-benchmarks/paddleocr

OCR_RESULT="${WORK_DIR}/paddleocr-mixed-content-result.json"
GT_REPORT="${WORK_DIR}/paddleocr-mixed-content-ground-truth.json"
MIXED_REPORT="${WORK_DIR}/paddleocr-mixed-content-evaluation.json"

printf '\n== Running PaddleOCR on full mixed-content page ==\n'

docker run \
  --rm \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${WORK_DIR}/model-cache:/root/.paddlex:Z" \
  "${IMAGE}" \
  /workspace/tools/ocr-benchmarks/paddleocr/run_paddleocr_benchmark.py \
  --manifest /workspace/docs/evaluation/corpora/ehrman-mixed-content-v1.json \
  --input-index /workspace/scripts/tmp/ocr-0h-ehrman-mixed-content/inputs/input-index.json \
  --input-dir /workspace/scripts/tmp/ocr-0h-ehrman-mixed-content/inputs \
  --output /workspace/scripts/tmp/ocr-0h-ehrman-mixed-content/paddleocr-mixed-content-result.json

UID_NOW="$(id -u)"
GID_NOW="$(id -g)"

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${IMAGE}" \
  -lc "chown -R ${UID_NOW}:${GID_NOW} /workspace/scripts/tmp/ocr-0h-ehrman-mixed-content"

printf '\n== Evaluating curated modern text and caption CER/WER ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-ground-truth \
  --ground-truth "${GROUND_TRUTH}" \
  --result "${OCR_RESULT}" \
  --report "${GT_REPORT}"

printf '\n== Evaluating mixed-content separation and narrative continuity ==\n'

python3 \
  tools/ocr-benchmarks/mixed-content/evaluate_ehrman_mixed_content.py \
  --structure "${STRUCTURE}" \
  --ocr-result "${OCR_RESULT}" \
  --ground-truth-report "${GT_REPORT}" \
  --output "${MIXED_REPORT}"

printf '\nArtifacts:\n'
printf '  %s\n' "${OCR_RESULT}"
printf '  %s\n' "${GT_REPORT}"
printf '  %s\n' "${MIXED_REPORT}"
