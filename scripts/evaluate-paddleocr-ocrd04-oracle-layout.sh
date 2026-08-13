#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${REPO}/docs/evaluation/corpora/ocr-d-diversification-v1.json"
WORK_DIR="${REPO}/scripts/tmp/ocr-0g-oracle-layout"
DATASET_DIR="${WORK_DIR}/gt_structure_text"
REPORT="${WORK_DIR}/paddleocr-ocrd04-oracle-layout-report.json"
IMAGE="document-processing-paddleocr:3.7.0-paddle3.2.2-cpu"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

for command in git docker python3; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

[[ -f "${MANIFEST}" ]] ||
  fail "OCR-0F diversification manifest is missing."

readarray -t VALUES < <(
  python3 - "${MANIFEST}" <<'PY'
import json
import sys
from pathlib import Path

manifest = json.loads(
    Path(sys.argv[1]).read_text(encoding="utf-8")
)

specimen = next(
    item
    for item in manifest["specimens"]
    if item["id"] == "ocrd-04"
)

print(manifest["upstream"]["repository"])
print(manifest["upstream"]["commit"])
print(Path(specimen["pageXmlPath"]).parent.as_posix())
PY
)

UPSTREAM_REPO="${VALUES[0]}"
UPSTREAM_COMMIT="${VALUES[1]}"
SPARSE_DIR="${VALUES[2]}"

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"

printf '\n== Fetching exact pinned OCR-D ocrd-04 source ==\n'

git init --quiet "${DATASET_DIR}"
git -C "${DATASET_DIR}" remote add origin "${UPSTREAM_REPO}"
git -C "${DATASET_DIR}" config core.sparseCheckout true

{
  printf '/LICENSE.md\n'
  printf '/%s/\n' "${SPARSE_DIR}"
} > "${DATASET_DIR}/.git/info/sparse-checkout"

git -C "${DATASET_DIR}" fetch \
  --quiet \
  --depth 1 \
  origin "${UPSTREAM_COMMIT}"

git -C "${DATASET_DIR}" checkout \
  --quiet \
  --detach FETCH_HEAD

ACTUAL_COMMIT="$(git -C "${DATASET_DIR}" rev-parse HEAD)"

[[ "${ACTUAL_COMMIT}" == "${UPSTREAM_COMMIT}" ]] ||
  fail "Upstream OCR-D commit mismatch."

printf 'Corpus commit: %s\n' "${ACTUAL_COMMIT}"

printf '\n== Building pinned PaddleOCR CPU image ==\n'

docker build \
  --pull \
  --tag "${IMAGE}" \
  "${REPO}/tools/ocr-benchmarks/paddleocr"

printf '\n== Running full-page and PAGE-XML oracle-layout comparison ==\n'

docker run \
  --rm \
  --entrypoint python \
  --volume "${REPO}:/workspace:Z" \
  --volume "${DATASET_DIR}:/dataset:ro,Z" \
  "${IMAGE}" \
  /workspace/tools/ocr-benchmarks/diversified/run_ocrd_oracle_layout.py \
  --dataset-root /dataset \
  --manifest /workspace/docs/evaluation/corpora/ocr-d-diversification-v1.json \
  --specimen-id ocrd-04 \
  --output /workspace/scripts/tmp/ocr-0g-oracle-layout/paddleocr-ocrd04-oracle-layout-report.json

UID_NOW="$(id -u)"
GID_NOW="$(id -g)"

docker run \
  --rm \
  --entrypoint /bin/bash \
  --volume "${REPO}:/workspace:Z" \
  "${IMAGE}" \
  -lc "chown -R ${UID_NOW}:${GID_NOW} /workspace/scripts/tmp/ocr-0g-oracle-layout"

printf '\n== OCR-0G report summary ==\n'

python3 - "${REPORT}" <<'PY'
import json
import sys
from pathlib import Path

report = json.loads(
    Path(sys.argv[1]).read_text(encoding="utf-8")
)

full = report["fullPage"]
oracle = report["oracleRegionLayout"]

def pct(value):
    return f"{value * 100:.3f}%"

print(
    "Full page:      "
    f'CER={pct(full["characterErrorRate"])} '
    f'WER={pct(full["wordErrorRate"])}'
)
print(
    "Oracle regions: "
    f'CER={pct(oracle["characterErrorRate"])} '
    f'WER={pct(oracle["wordErrorRate"])}'
)
print(
    "CER improvement: "
    f'{(full["characterErrorRate"] - oracle["characterErrorRate"]) * 100:+.3f} pp'
)
print(
    "WER improvement: "
    f'{(full["wordErrorRate"] - oracle["wordErrorRate"]) * 100:+.3f} pp'
)
print(
    "Empty recognized regions: "
    f'{report["oracle"]["emptyRecognizedRegionCount"]}/'
    f'{report["oracle"]["regionCount"]}'
)
print()
print("This is diagnostic evidence only.")
print("Oracle layout uses PAGE-XML TextRegion bounds and explicit reading order.")
PY

printf '\nArtifact:\n'
printf '  %s\n' "${REPORT}"
