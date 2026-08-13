#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0a-self-test"

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
  bash scripts/validate-ocr-benchmark-harness.sh \
    --source /absolute/path/ehrman.pdf

This is an OCR-0A harness self-test. It renders the committed benchmark pages,
verifies the native/raster corpus assumptions, generates a synthetic
contract-conforming OCR result, evaluates it, and checks the expected summary.

It does not invoke a real OCR engine.
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

[[ -n "${SOURCE}" ]] ||
  fail "--source is required."

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"

INPUT_DIR="${WORK_DIR}/inputs"
CORPUS_REPORT="${WORK_DIR}/corpus-verification.json"
SYNTHETIC_RESULT="${WORK_DIR}/synthetic-ocr-result.json"
EVALUATION_REPORT="${WORK_DIR}/synthetic-evaluation.json"

cd "${REPO}"

bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source "${SOURCE}" \
  --manifest "${MANIFEST}" \
  --output-dir "${INPUT_DIR}"

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  verify-ocr-benchmark-corpus \
  --manifest "${MANIFEST}" \
  --source "${SOURCE}" \
  --report "${CORPUS_REPORT}"

python3 - \
  "${MANIFEST}" \
  "${INPUT_DIR}/input-index.json" \
  "${SYNTHETIC_RESULT}" <<'PY'
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
index_path = Path(sys.argv[2])
result_path = Path(sys.argv[3])

manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
index = json.loads(index_path.read_text(encoding="utf-8"))

manifest_page = {
    page["pageNumber"]: page
    for page in manifest["pages"]
}

pages = []

for input_page in index["pages"]:
    number = input_page["pageNumber"]
    spec = manifest_page[number]
    regions = []

    if spec.get("expectedTitle"):
        title = spec["expectedTitle"]

        if number == 32:
            parts = [
                "Introduction",
                "Why Study the New Testament?",
            ]
        elif title[0].isdigit():
            number_prefix, remainder = title.split(".", 1)
            parts = [
                f"Chapter {number_prefix.strip()}:",
                remainder.strip(),
            ]
        else:
            parts = [title]

        for sequence, text in enumerate(parts):
            regions.append(
                {
                    "sequence": sequence,
                    "text": text,
                    "confidence": 0.99,
                    "bounds": {
                        "left": 0.10,
                        "top": 0.08 + sequence * 0.05,
                        "right": 0.90,
                        "bottom": 0.12 + sequence * 0.05,
                    },
                }
            )
    else:
        regions.append(
            {
                "sequence": 0,
                "text": f"Synthetic OCR text for benchmark page {number}.",
                "confidence": 0.99,
                "bounds": {
                    "left": 0.10,
                    "top": 0.10,
                    "right": 0.90,
                    "bottom": 0.15,
                },
            }
        )

    pages.append(
        {
            "pageNumber": number,
            "inputSha256": input_page["sha256"],
            "status": "Completed",
            "elapsedMilliseconds": 1.0,
            "imageWidth": input_page["width"],
            "imageHeight": input_page["height"],
            "regions": regions,
            "diagnostics": [],
        }
    )

result = {
    "schemaVersion": "document-processing-ocr-engine-result-v1",
    "benchmarkId": manifest["benchmarkId"],
    "sourceSha256": manifest["source"]["sha256"],
    "engine": {
        "id": "synthetic-contract-self-test",
        "version": "1",
        "model": "none",
        "backend": "deterministic-fixture",
        "device": "none",
        "metadata": {
            "purpose": "OCR-0A harness validation only"
        },
    },
    "performance": {
        "startupMilliseconds": 0.0,
        "processPeakWorkingSetBytes": None,
        "acceleratorPeakMemoryBytes": None,
    },
    "pages": pages,
}

result_path.write_text(
    json.dumps(result, indent=2, ensure_ascii=False) + "\n",
    encoding="utf-8",
)
PY

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-benchmark \
  --manifest "${MANIFEST}" \
  --input-index "${INPUT_DIR}/input-index.json" \
  --result "${SYNTHETIC_RESULT}" \
  --report "${EVALUATION_REPORT}"

python3 - \
  "${CORPUS_REPORT}" \
  "${EVALUATION_REPORT}" <<'PY'
import json
import sys
from pathlib import Path

corpus = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
evaluation = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

def require(condition, message):
    if not condition:
        raise SystemExit(f"SELF-TEST ERROR: {message}")

require(corpus["pageCount"] == 19, "expected 19 corpus pages")
require(corpus["matchingPages"] == 19, "all corpus native-state expectations must match")
require(corpus["mismatchingPages"] == 0, "corpus must contain no native-state mismatch")

coverage = evaluation["coverage"]
raster = evaluation["rasterReference"]
titles = evaluation["outlineTitles"]
controls = evaluation["bornDigitalControls"]

require(coverage["expectedPages"] == 19, "expected 19 evaluation pages")
require(coverage["completedPages"] == 19, "synthetic result must complete all pages")
require(coverage["failedPages"] == 0, "synthetic result must contain no failure")
require(coverage["pagesWithText"] == 19, "synthetic result must contain text on all pages")

require(raster["pageCount"] == 7, "expected 7 raster-reference pages")
require(raster["pagesWithText"] == 7, "synthetic result must recover 7/7 raster pages")
require(raster["historicalEasyOcrRecoveredPages"] == 7, "historical EasyOCR page baseline changed")
require(raster["historicalEasyOcrCharacterCount"] == 12393, "historical EasyOCR char baseline changed")

require(titles["pageCount"] == 7, "expected 7 outline-title pages")
require(titles["plausibleMatches"] == 7, "synthetic title recovery must be 7/7 plausible")
require(titles["exploratoryMatches"] == 0, "synthetic title recovery must contain no exploratory match")
require(titles["noCandidate"] == 0, "synthetic title recovery must contain no missing candidate")

require(controls["pageCount"] == 5, "expected 5 born-digital control pages")

for page in evaluation["pages"]:
    require(page["inputIntegrityMatches"], f"input integrity failed for page {page['pageNumber']}")

print()
print("RESULT: OCR-0A HARNESS SELF-TEST PASSED")
print("  corpus pages: 19/19 native-state expectations")
print("  synthetic completed pages: 19/19")
print("  raster-reference text recovery: 7/7")
print("  outline-title plausible recovery: 7/7")
print("  born-digital controls: 5")
print("  rendered input integrity: 19/19")
PY
