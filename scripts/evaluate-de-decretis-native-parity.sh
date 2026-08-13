#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIRECTORY}"

PROJECT="tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj"
EXPECTED_SHA="de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75"
EXPECTED_BYTES="11963985"
SOURCE_PATH="${DOCUMENT_PROCESSING_DE_DECRETIS_PDF:-}"
REPORT_PATH="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-native-parity.json"

fail() {
  printf '\nERROR: %s\n' "$*" >&2
  exit 1
}

usage() {
  cat <<'USAGE'
Usage:
  bash scripts/evaluate-de-decretis-native-parity.sh \
    [--de-decretis /absolute/path/npnf204.pdf] \
    [--report /absolute/path/report.json]
USAGE
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
    --de-decretis)
      SOURCE_PATH="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --report)
      REPORT_PATH="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in dotnet sha256sum python3 realpath find awk; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

discover_source() {
  local root
  local candidate
  local name
  local actual_sha
  local -a matches=()

  for root in "${HOME}/Documents" "${HOME}/Downloads"; do
    [[ -d "${root}" ]] || continue

    while IFS= read -r -d '' candidate; do
      name="$(basename "${candidate}" | tr '[:upper:]' '[:lower:]')"

      if [[ "${name}" != *"npnf204"* &&
            "${name}" != *"npnf2-04"* &&
            "${name}" != *"npnf2_04"* ]]; then
        continue
      fi

      actual_sha="$(sha256sum "${candidate}" | awk '{print $1}')"

      if [[ "${actual_sha}" == "${EXPECTED_SHA}" ]]; then
        matches+=("${candidate}")
      fi
    done < <(
      find "${root}" -maxdepth 5 -type f -iname '*.pdf' -print0 2>/dev/null
    )
  done

  [[ ${#matches[@]} -gt 0 ]] || return 1

  printf '%s' "${matches[0]}"
}

if [[ -z "${SOURCE_PATH}" ]]; then
  SOURCE_PATH="$(discover_source)" ||
    fail "Could not locate the pinned De Decretis source. Use --de-decretis."
fi

SOURCE_PATH="$(realpath "${SOURCE_PATH}" 2>/dev/null)" ||
  fail "Cannot resolve source path."

REPORT_PATH="$(realpath -m "${REPORT_PATH}" 2>/dev/null)" ||
  fail "Cannot resolve report path."

[[ -f "${SOURCE_PATH}" ]] ||
  fail "Source file not found: ${SOURCE_PATH}"

ACTUAL_SHA="$(sha256sum "${SOURCE_PATH}" | awk '{print $1}')"
[[ "${ACTUAL_SHA}" == "${EXPECTED_SHA}" ]] ||
  fail "Source SHA-256 mismatch."

ACTUAL_BYTES="$(stat -c '%s' "${SOURCE_PATH}")"
[[ "${ACTUAL_BYTES}" == "${EXPECTED_BYTES}" ]] ||
  fail "Source byte length mismatch."

mkdir -p "$(dirname "${REPORT_PATH}")"

printf '\n== De Decretis native PDF parity ==\n'
printf 'Source: %s\n' "${SOURCE_PATH}"
printf 'Report: %s\n\n' "${REPORT_PATH}"

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-pdf \
  --source "${SOURCE_PATH}" \
  --report "${REPORT_PATH}" \
  --pages 512-561 \
  --probe "endless ages of ages. Amen."

python3 - "${REPORT_PATH}" "${EXPECTED_SHA}" "${EXPECTED_BYTES}" <<'PY'
import json
import sys
from pathlib import Path

report = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
expected_sha = sys.argv[2]
expected_bytes = int(sys.argv[3])

def require(condition, message):
    if not condition:
        raise SystemExit(f"REGRESSION FAILED: {message}")

def probe(value):
    for item in report.get("probes", []):
        if item.get("probe") == value:
            return item
    raise SystemExit(f"REGRESSION FAILED: missing probe {value!r}")

require(
    report.get("schemaVersion") ==
    "document-processing-native-pdf-analysis-v1",
    "unexpected report schema")

require(
    report.get("sourceSha256") == expected_sha,
    "source identity changed")

require(
    report.get("sourceByteLength") == expected_bytes,
    "source byte length changed")

require(
    report.get("totalPdfPages") == 1479,
    "total PDF page count changed")

selection = report["pageSelection"]
require(selection["firstPage"] == 512, "first page changed")
require(selection["lastPage"] == 561, "last page changed")
require(selection["pageCount"] == 50, "selected page count changed")

extraction = report["extraction"]
require(extraction["wordCount"] == 29044, "word count is not at ApologiaStudio parity")
require(extraction["blockCount"] == 269, "block count is not at ApologiaStudio parity")
require(extraction["pagesWithWords"] == 50, "not all selected pages contain native words")
require(extraction["pagesWithoutWords"] == 0, "native text coverage regressed")
require(extraction["textLayerCoveragePercent"] == 100.0, "text-layer coverage is not 100%")
require(
    extraction["textlessPagesWithDominantRasterImage"] == 0,
    "unexpected textless dominant-raster page")

layout = report["rawLayout"]
require(
    layout["multiColumnCandidatePages"] == 4,
    "multi-column diagnostic is not at ApologiaStudio parity")
require(
    layout["interleavedColumnPages"] == 2,
    "interleaved-column diagnostic is not at ApologiaStudio parity")
require(
    layout["verticalReversalPages"] == 3,
    "vertical-reading-order diagnostic is not at ApologiaStudio parity")

sentinel = probe("endless ages of ages. Amen.")
require(
    sentinel["wordStreamMatches"] == 1,
    "sentinel was not recovered exactly once in the page word stream")
require(
    sentinel["blockMatches"] == 1,
    "sentinel was not recovered exactly once in the block stream")

print()
print("RESULT: PARITY PASS")
print(
    "De Decretis: "
    f"pages={selection['pageCount']} "
    f"words={extraction['wordCount']} "
    f"blocks={extraction['blockCount']} "
    f"coverage={extraction['textLayerCoveragePercent']:.1f}%"
)
print(
    "Layout: "
    f"multicolumn={layout['multiColumnCandidatePages']} "
    f"interleaved={layout['interleavedColumnPages']} "
    f"vertical_reversal={layout['verticalReversalPages']}"
)
print(
    "Sentinel: "
    f"word_stream={sentinel['wordStreamMatches']} "
    f"blocks={sentinel['blockMatches']}"
)
PY
