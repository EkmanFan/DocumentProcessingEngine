#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GROUND_TRUTH="${REPO}/docs/evaluation/corpora/ehrman-ocr-ground-truth-v1.json"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0d-ground-truth"

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
  bash scripts/evaluate-ocr-ground-truth.sh \
    --source /absolute/path/ehrman.pdf

Runs both committed CPU challengers on the existing OCR-0A corpus, then
computes CER/WER over the curated OCR-0D zones.

Artifacts:
  scripts/tmp/ocr-0d-ground-truth/paddleocr-ground-truth.json
  scripts/tmp/ocr-0d-ground-truth/doctr-ground-truth.json
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in dotnet python3 realpath docker; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

[[ -n "${SOURCE}" ]] ||
  fail "--source is required."

SOURCE="$(realpath "${SOURCE}")"

[[ -f "${SOURCE}" ]] ||
  fail "Source PDF not found: ${SOURCE}"

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"

cd "${REPO}"

printf '\n== Building EvaluationCli for OCR-0D ==\n'
dotnet build DocumentProcessingEngine.sln -warnaserror

printf '\n== Running PaddleOCR OCR-0B challenger ==\n'
bash scripts/evaluate-paddleocr-ppocrv6-cpu.sh \
  --source "${SOURCE}"

printf '\n== Running docTR OCR-0C challenger ==\n'
bash scripts/evaluate-doctr-fast-crnn-cpu.sh \
  --source "${SOURCE}"

PADDLE_RESULT="${REPO}/scripts/tmp/ocr-0b-paddleocr/paddleocr-result.json"
DOCTR_RESULT="${REPO}/scripts/tmp/ocr-0c-doctr/doctr-result.json"

PADDLE_REPORT="${WORK_DIR}/paddleocr-ground-truth.json"
DOCTR_REPORT="${WORK_DIR}/doctr-ground-truth.json"

printf '\n== Evaluating PaddleOCR against curated ground truth ==\n'
dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-ground-truth \
  --ground-truth "${GROUND_TRUTH}" \
  --result "${PADDLE_RESULT}" \
  --report "${PADDLE_REPORT}"

printf '\n== Evaluating docTR against curated ground truth ==\n'
dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-ground-truth \
  --ground-truth "${GROUND_TRUTH}" \
  --result "${DOCTR_RESULT}" \
  --report "${DOCTR_REPORT}"

python3 - \
  "${PADDLE_REPORT}" \
  "${DOCTR_REPORT}" <<'PY'
import json
import sys
from pathlib import Path

paddle = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
doctr = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

def pct(value):
    return f"{value * 100:.3f}%"

print()
print("RESULT: OCR-0D GROUND-TRUTH COMPARISON")
print(
    "Reference: "
    f'{paddle["zoneCount"]} zones, '
    f'{paddle["referenceCharacterCount"]} chars, '
    f'{paddle["referenceWordCount"]} words'
)
print()
print(
    "PaddleOCR: "
    f'CER={pct(paddle["characterErrorRate"])} '
    f'WER={pct(paddle["wordErrorRate"])} '
    f'charEdits={paddle["characterEdits"]} '
    f'wordEdits={paddle["wordEdits"]}'
)
print(
    "docTR:     "
    f'CER={pct(doctr["characterErrorRate"])} '
    f'WER={pct(doctr["wordErrorRate"])} '
    f'charEdits={doctr["characterEdits"]} '
    f'wordEdits={doctr["wordEdits"]}'
)

cer_delta = doctr["characterErrorRate"] - paddle["characterErrorRate"]
wer_delta = doctr["wordErrorRate"] - paddle["wordErrorRate"]

print()
print(
    "docTR - PaddleOCR: "
    f'CER delta={cer_delta * 100:+.3f} pp, '
    f'WER delta={wer_delta * 100:+.3f} pp'
)

if cer_delta < 0 and wer_delta < 0:
    conclusion = "docTR is lower-error on both CER and WER for this curated corpus."
elif cer_delta > 0 and wer_delta > 0:
    conclusion = "PaddleOCR is lower-error on both CER and WER for this curated corpus."
else:
    conclusion = "CER/WER are mixed; inspect per-zone evidence before ranking textual fidelity."

print(f"Comparison: {conclusion}")
print()
print("This is evaluation evidence only; it does not select a production OCR engine.")
PY

printf '\nArtifacts:\n'
printf '  %s\n' "${PADDLE_REPORT}"
printf '  %s\n' "${DOCTR_REPORT}"
