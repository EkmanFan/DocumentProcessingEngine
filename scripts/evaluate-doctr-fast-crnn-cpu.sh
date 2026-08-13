#!/usr/bin/env bash
set -Eeuo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json"
SOURCE=""
WORK_DIR="${REPO}/scripts/tmp/ocr-0c-doctr"
IMAGE="document-processing-doctr:1.0.1-torch2.8.0-cpu"
fail(){ printf 'ERROR: %s\n' "$*" >&2; exit 1; }
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) [[ $# -ge 2 ]] || fail "Missing value for --source."; SOURCE="$2"; shift 2;;
    --help|-h) echo "Usage: bash scripts/evaluate-doctr-fast-crnn-cpu.sh --source /absolute/path/ehrman.pdf"; exit 0;;
    *) fail "Unknown option: $1";;
  esac
done
for cmd in docker dotnet python3 realpath; do command -v "$cmd" >/dev/null 2>&1 || fail "$cmd is required."; done
[[ -n "$SOURCE" ]] || fail "--source is required."
SOURCE="$(realpath "$SOURCE")"; [[ -f "$SOURCE" ]] || fail "Source PDF not found."
rm -rf "$WORK_DIR"; mkdir -p "$WORK_DIR/inputs" "$WORK_DIR/model-cache"
cd "$REPO"
bash scripts/prepare-ocr-benchmark-inputs.sh --source "$SOURCE" --manifest "$MANIFEST" --output-dir "$WORK_DIR/inputs"
printf '\n== Building pinned docTR CPU benchmark image ==\n'
docker build --pull --tag "$IMAGE" tools/ocr-benchmarks/doctr
RESULT="$WORK_DIR/doctr-result.json"; REPORT="$WORK_DIR/doctr-evaluation.json"
printf '\n== Running docTR fast_base + crnn_vgg16_bn ==\n'
UID_NOW="$(id -u)"; GID_NOW="$(id -g)"
docker run --rm --user "$UID_NOW:$GID_NOW" --env HOME=/home/benchmark   --volume "$REPO:/workspace:Z"   --volume "$WORK_DIR/model-cache:/home/benchmark/.cache/doctr:Z"   "$IMAGE"   --manifest /workspace/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json   --input-index /workspace/scripts/tmp/ocr-0c-doctr/inputs/input-index.json   --input-dir /workspace/scripts/tmp/ocr-0c-doctr/inputs   --output /workspace/scripts/tmp/ocr-0c-doctr/doctr-result.json
printf '\n== Evaluating neutral docTR result ==\n'
dotnet run --no-build --project tools/DocumentProcessing.EvaluationCli --   evaluate-ocr-benchmark --manifest "$MANIFEST" --input-index "$WORK_DIR/inputs/input-index.json"   --result "$RESULT" --report "$REPORT"
python3 - "$REPORT" <<'PY'
import json,sys
from pathlib import Path
r=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8")); c=r["coverage"]; x=r["rasterReference"]; t=r["outlineTitles"]; p=r.get("performance") or {}
print("\nRESULT: OCR-0C DOCTR SUMMARY")
print(f'  completed / failed / text pages: {c["completedPages"]} / {c["failedPages"]} / {c["pagesWithText"]}')
print(f'  raster reference recovered: {x["pagesWithText"]}/{x["pageCount"]}')
print(f'  raster chars / historical EasyOCR chars: {x["characterCount"]} / {x["historicalEasyOcrCharacterCount"]}')
print(f'  outline plausible / exploratory / none: {t["plausibleMatches"]} / {t["exploratoryMatches"]} / {t["noCandidate"]}')
print(f'  total elapsed ms: {c["totalElapsedMilliseconds"]:.1f}')
print(f'  startup ms: {p.get("startupMilliseconds")}')
print(f'  peak process bytes: {p.get("processPeakWorkingSetBytes")}')
print("\nRaster page-recovery gate: " + ("PASS (7/7)" if x["pagesWithText"]==x["pageCount"] else "FAIL"))
PY
printf '\nArtifacts:\n  %s\n  %s\n' "$RESULT" "$REPORT"
