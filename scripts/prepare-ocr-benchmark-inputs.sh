#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_MANIFEST="${REPO}/docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json"

SOURCE=""
MANIFEST="${DEFAULT_MANIFEST}"
OUTPUT_DIR="${REPO}/scripts/tmp/ocr-benchmark-inputs"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

read_value() {
  local option="$1"
  local value="${2:-}"

  [[ -n "${value}" && "${value}" != --* ]] ||
    fail "Missing value for ${option}."

  printf '%s' "${value}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      SOURCE="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --manifest)
      MANIFEST="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --output-dir)
      OUTPUT_DIR="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --help|-h)
      cat <<'HELP'
Usage:
  bash scripts/prepare-ocr-benchmark-inputs.sh \
    --source /absolute/path/document.pdf \
    [--manifest docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json] \
    [--output-dir scripts/tmp/ocr-benchmark-inputs]

The renderer is fixed by the benchmark manifest. OCR-0A uses pdftoppm to
produce the same PNG inputs for every OCR backend. Rendering is evaluation
infrastructure, not the production IPdfRasterizer implementation.
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in pdftoppm python3 sha256sum stat realpath; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

[[ -n "${SOURCE}" ]] ||
  fail "--source is required."

SOURCE="$(realpath "${SOURCE}")"
MANIFEST="$(realpath "${MANIFEST}")"
OUTPUT_DIR="$(realpath -m "${OUTPUT_DIR}")"

[[ -f "${SOURCE}" ]] ||
  fail "Source PDF not found: ${SOURCE}"

[[ -f "${MANIFEST}" ]] ||
  fail "Manifest not found: ${MANIFEST}"

mkdir -p "${OUTPUT_DIR}"
rm -f "${OUTPUT_DIR}"/page-*.png
rm -f "${OUTPUT_DIR}/input-index.json"

python3 - \
  "${MANIFEST}" \
  "${SOURCE}" \
  "${OUTPUT_DIR}" <<'PY'
import hashlib
import json
import os
import struct
import subprocess
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
source_path = Path(sys.argv[2])
output_dir = Path(sys.argv[3])

manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

if manifest.get("schemaVersion") != "document-processing-ocr-benchmark-manifest-v1":
    raise SystemExit("Unsupported OCR benchmark manifest schema.")

source = manifest["source"]
rendering = manifest["rendering"]

source_sha = hashlib.sha256(source_path.read_bytes()).hexdigest()
source_bytes = source_path.stat().st_size

if source_sha != source["sha256"]:
    raise SystemExit(
        f"Source SHA mismatch: expected {source['sha256']}, found {source_sha}"
    )

if source_bytes != source["byteLength"]:
    raise SystemExit(
        f"Source byte length mismatch: expected {source['byteLength']}, found {source_bytes}"
    )

if rendering["format"].lower() != "png":
    raise SystemExit("OCR-0A renderer currently supports only PNG.")

if rendering["colorMode"].lower() != "rgb":
    raise SystemExit("OCR-0A renderer currently expects RGB rendering.")

if rendering["preprocessing"].lower() != "none":
    raise SystemExit("OCR-0A renderer currently expects no preprocessing.")

dpi = int(rendering["dpi"])
if dpi <= 0:
    raise SystemExit("Invalid render DPI.")

version = subprocess.run(
    ["pdftoppm", "-v"],
    stdout=subprocess.PIPE,
    stderr=subprocess.STDOUT,
    text=True,
    check=True,
).stdout.splitlines()[0].strip()

def png_size(path: Path):
    header = path.read_bytes()[:24]
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"Not a PNG file: {path}")
    if header[12:16] != b"IHDR":
        raise SystemExit(f"PNG missing IHDR at expected location: {path}")
    return struct.unpack(">II", header[16:24])

pages = []
seen = set()

for page in sorted(manifest["pages"], key=lambda item: item["pageNumber"]):
    number = int(page["pageNumber"])
    if number in seen:
        raise SystemExit(f"Duplicate manifest page: {number}")
    seen.add(number)

    prefix = output_dir / f"page-{number:04d}"
    output = Path(str(prefix) + ".png")

    subprocess.run(
        [
            "pdftoppm",
            "-f",
            str(number),
            "-l",
            str(number),
            "-r",
            str(dpi),
            "-png",
            "-singlefile",
            str(source_path),
            str(prefix),
        ],
        check=True,
    )

    if not output.is_file():
        raise SystemExit(f"Renderer did not create expected file: {output}")

    data = output.read_bytes()
    width, height = png_size(output)

    pages.append(
        {
            "pageNumber": number,
            "fileName": output.name,
            "sha256": hashlib.sha256(data).hexdigest(),
            "byteLength": len(data),
            "width": width,
            "height": height,
        }
    )

index = {
    "schemaVersion": "document-processing-ocr-benchmark-input-index-v1",
    "benchmarkId": manifest["benchmarkId"],
    "sourceSha256": source_sha,
    "rasterizer": {
        "id": "pdftoppm",
        "version": version,
        "dpi": dpi,
        "format": "png",
        "colorMode": rendering["colorMode"],
        "preprocessing": rendering["preprocessing"],
    },
    "pages": pages,
}

index_path = output_dir / "input-index.json"
temporary = output_dir / f".input-index.{os.getpid()}.tmp"
temporary.write_text(
    json.dumps(index, indent=2, ensure_ascii=False) + "\n",
    encoding="utf-8",
)
temporary.replace(index_path)

print("RESULT: OCR BENCHMARK INPUTS PREPARED")
print(f"Benchmark: {manifest['benchmarkId']}")
print(f"Source SHA-256: {source_sha}")
print(f"Rasterizer: {version}")
print(f"DPI: {dpi}")
print(f"Pages rendered: {len(pages)}")
print(f"Output directory: {output_dir}")
print(f"Input index: {index_path}")
PY
