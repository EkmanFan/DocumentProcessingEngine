#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${REPO}/docs/evaluation/corpora/ocr-d-diversification-v1.json"
WORK_DIR="${REPO}/scripts/tmp/ocr-0f-diversified"
DATASET_DIR="${WORK_DIR}/gt_structure_text"
REPORT="${WORK_DIR}/paddleocr-diversification-report.json"
IMAGE="document-processing-paddleocr:3.7.0-paddle3.2.2-cpu"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

for command in git docker python3 dotnet; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

[[ -f "${MANIFEST}" ]] ||
  fail "Diversification manifest is missing."

readarray -t VALUES < <(
  python3 - "${MANIFEST}" <<'PY'
import json
import sys
from pathlib import Path

manifest = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
print(manifest["upstream"]["repository"])
print(manifest["upstream"]["commit"])

for specimen in manifest["specimens"]:
    print(Path(specimen["pageXmlPath"]).parent.as_posix())
PY
)

UPSTREAM_REPO="${VALUES[0]}"
UPSTREAM_COMMIT="${VALUES[1]}"

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"

printf '\n== Fetching exact OCR-D corpus revision with sparse checkout ==\n'

git init --quiet "${DATASET_DIR}"
git -C "${DATASET_DIR}" remote add origin "${UPSTREAM_REPO}"
git -C "${DATASET_DIR}" config core.sparseCheckout true

{
  printf '/LICENSE.md\n'
  for (( index=2; index<${#VALUES[@]}; index++ )); do
    printf '/%s/\n' "${VALUES[$index]}"
  done
} | awk '!seen[$0]++' > "${DATASET_DIR}/.git/info/sparse-checkout"

git -C "${DATASET_DIR}" fetch \
  --quiet \
  --depth 1 \
  origin "${UPSTREAM_COMMIT}"

git -C "${DATASET_DIR}" checkout \
  --quiet \
  --detach FETCH_HEAD

ACTUAL_COMMIT="$(git -C "${DATASET_DIR}" rev-parse HEAD)"

[[ "${ACTUAL_COMMIT}" == "${UPSTREAM_COMMIT}" ]] ||
  fail "Upstream corpus commit mismatch."

printf 'Corpus commit: %s\n' "${ACTUAL_COMMIT}"

printf '\n== Building pinned PaddleOCR CPU benchmark image ==\n'

docker build \
  --pull \
  --tag "${IMAGE}" \
  "${REPO}/tools/ocr-benchmarks/paddleocr"

printf '\n== Running PaddleOCR on diversified real scans ==\n'

docker run \
  --rm \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${DATASET_DIR}:/dataset:ro,Z" \
  "${IMAGE}" \
  /workspace/tools/ocr-benchmarks/diversified/run_ocrd_diversification.py \
  run \
  --dataset-root /dataset \
  --manifest /workspace/docs/evaluation/corpora/ocr-d-diversification-v1.json \
  --output /workspace/scripts/tmp/ocr-0f-diversified/paddleocr-diversification-report.json

UID_NOW="$(id -u)"
GID_NOW="$(id -g)"

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${IMAGE}" \
  -lc "chown -R ${UID_NOW}:${GID_NOW} /workspace/scripts/tmp/ocr-0f-diversified"

printf '\n== OCR-0F report summary ==\n'

python3 - "${REPORT}" <<'PY'
import json
import sys
from pathlib import Path

report = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))

def pct(value):
    return f"{value * 100:.3f}%"

print(
    "Aggregate CER / WER: "
    f'{pct(report["characterErrorRate"])} / '
    f'{pct(report["wordErrorRate"])}'
)
print(
    "Reference chars / words: "
    f'{report["referenceCharacterCount"]} / '
    f'{report["referenceWordCount"]}'
)
print("Per specimen:")

for specimen in report["specimens"]:
    print(
        f'  {specimen["id"]} {specimen["category"]}: '
        f'CER={pct(specimen["characterErrorRate"])} '
        f'WER={pct(specimen["wordErrorRate"])} '
        f'year={specimen["year"]}'
    )

print()
print("No production acceptance threshold is applied by OCR-0F.")
print("Interpret corpus-specific failure modes before backend selection.")
PY

printf '\nArtifact:\n'
printf '  %s\n' "${REPORT}"
