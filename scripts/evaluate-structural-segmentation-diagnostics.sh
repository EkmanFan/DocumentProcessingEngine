#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIRECTORY}"

PROJECT="tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj"

EHRMAN_SHA="f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe"
EHRMAN_BYTES="233369762"
DE_DECRETIS_SHA="de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75"
DE_DECRETIS_BYTES="11963985"

EHRMAN_HISTORICAL_SEGMENTS=277
DE_DECRETIS_HISTORICAL_SEGMENTS=50

EHRMAN_PATH="${DOCUMENT_PROCESSING_EHRMAN_PDF:-}"
DE_DECRETIS_PATH="${DOCUMENT_PROCESSING_DE_DECRETIS_PDF:-}"

EHRMAN_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-segmentation-diagnostics.json"
DE_DECRETIS_REPORT="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-segmentation-diagnostics.json"

fail() {
  printf '\nERROR: %s\n' "$*" >&2
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
    --ehrman)
      EHRMAN_PATH="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --de-decretis)
      DE_DECRETIS_PATH="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

for command in dotnet sha256sum python3 realpath find awk stat; do
  command -v "${command}" >/dev/null 2>&1 ||
    fail "${command} is required."
done

discover_by_sha() {
  local expected_sha="$1"
  shift

  local root
  local candidate
  local actual_sha

  for root in "$@"; do
    [[ -d "${root}" ]] || continue

    while IFS= read -r -d '' candidate; do
      actual_sha="$(sha256sum "${candidate}" | awk '{print $1}')"

      if [[ "${actual_sha}" == "${expected_sha}" ]]; then
        printf '%s' "${candidate}"
        return 0
      fi
    done < <(
      find "${root}" -maxdepth 6 -type f -iname '*.pdf' -print0 2>/dev/null
    )
  done

  return 1
}

verify_source() {
  local label="$1"
  local source_path="$2"
  local expected_sha="$3"
  local expected_bytes="$4"

  source_path="$(realpath "${source_path}" 2>/dev/null)" ||
    fail "Cannot resolve ${label} path."

  [[ -f "${source_path}" ]] ||
    fail "${label} PDF not found: ${source_path}"

  local actual_sha
  actual_sha="$(sha256sum "${source_path}" | awk '{print $1}')"

  [[ "${actual_sha}" == "${expected_sha}" ]] ||
    fail "${label} SHA-256 mismatch."

  local actual_bytes
  actual_bytes="$(stat -c '%s' "${source_path}")"

  [[ "${actual_bytes}" == "${expected_bytes}" ]] ||
    fail "${label} byte length mismatch."

  printf '%s' "${source_path}"
}

if [[ -z "${EHRMAN_PATH}" ]]; then
  EHRMAN_PATH="$(
    discover_by_sha \
      "${EHRMAN_SHA}" \
      "${HOME}/Downloads" \
      "${HOME}/Documents"
  )" ||
    fail "Could not locate pinned Ehrman PDF."
fi

if [[ -z "${DE_DECRETIS_PATH}" ]]; then
  DE_DECRETIS_PATH="$(
    discover_by_sha \
      "${DE_DECRETIS_SHA}" \
      "${HOME}/Documents" \
      "${HOME}/Downloads"
  )" ||
    fail "Could not locate pinned De Decretis PDF."
fi

EHRMAN_PATH="$(
  verify_source \
    "Ehrman" \
    "${EHRMAN_PATH}" \
    "${EHRMAN_SHA}" \
    "${EHRMAN_BYTES}"
)"

DE_DECRETIS_PATH="$(
  verify_source \
    "De Decretis" \
    "${DE_DECRETIS_PATH}" \
    "${DE_DECRETIS_SHA}" \
    "${DE_DECRETIS_BYTES}"
)"

mkdir -p "${ROOT_DIRECTORY}/scripts/tmp"
rm -f "${EHRMAN_REPORT}" "${DE_DECRETIS_REPORT}"

printf '\n== Structural segmentation diagnostics: Ehrman ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-segmented-pdf \
  --source "${EHRMAN_PATH}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617 \
  --probe "TAKE A STAND" \
  --probe "WHAT DO YOU THINK?" \
  --probe "SUGGESTIONS FOR FURTHER READING"

printf '\n== Structural segmentation diagnostics: De Decretis ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-segmented-pdf \
  --source "${DE_DECRETIS_PATH}" \
  --report "${DE_DECRETIS_REPORT}" \
  --pages 512-561 \
  --probe "endless ages of ages. Amen."

python3 - \
  "${EHRMAN_REPORT}" \
  "${DE_DECRETIS_REPORT}" \
  "${EHRMAN_SHA}" \
  "${EHRMAN_BYTES}" \
  "${DE_DECRETIS_SHA}" \
  "${DE_DECRETIS_BYTES}" \
  "${EHRMAN_HISTORICAL_SEGMENTS}" \
  "${DE_DECRETIS_HISTORICAL_SEGMENTS}" <<'PY'
import json
import sys
from pathlib import Path

ehrman = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
de = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

ehrman_sha = sys.argv[3]
ehrman_bytes = int(sys.argv[4])
de_sha = sys.argv[5]
de_bytes = int(sys.argv[6])
ehrman_historical = int(sys.argv[7])
de_historical = int(sys.argv[8])

EXPECTED_SCHEMA = "document-processing-segmented-pdf-analysis-v1"
EXPECTED_NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
EXPECTED_SEGMENTATION = "page-bounded-obvious-headings-v1"

def require(condition, message):
    if not condition:
        raise SystemExit(f"SEGMENTATION DIAGNOSTIC FAILED: {message}")

def validate_common(report, sha, size):
    require(report["schemaVersion"] == EXPECTED_SCHEMA, "unexpected report schema")
    require(report["sourceSha256"] == sha, "source SHA-256 changed")
    require(report["sourceByteLength"] == size, "source byte length changed")
    require(report["normalizationProfileId"] == EXPECTED_NORMALIZATION, "normalization profile changed")
    require(report["segmentationProfileId"] == EXPECTED_SEGMENTATION, "segmentation profile changed")
    require(report["segmentation"]["crossPageSegmentCount"] == 0, "v1 unexpectedly produced cross-page segment")

validate_common(ehrman, ehrman_sha, ehrman_bytes)
validate_common(de, de_sha, de_bytes)

# Freeze already-established upstream behavior. Segment counts themselves are
# observations in 8.2 and deliberately are not asserted.
require(ehrman["totalPdfPages"] == 617, "Ehrman PDF page count changed")
require(ehrman["pageSelection"]["pageCount"] == 617, "Ehrman selected page count changed")
require(ehrman["preprocessing"]["wordCount"] == 233595, "Ehrman native word count regressed")
require(ehrman["preprocessing"]["rawBlockCount"] == 3179, "Ehrman raw block count regressed")
require(ehrman["preprocessing"]["includedBlockCount"] == 2648, "Ehrman included block count regressed")
require(ehrman["preprocessing"]["excludedHeaderBlocks"] == 531, "Ehrman recurring header count regressed")
require(ehrman["preprocessing"]["excludedFooterBlocks"] == 0, "Ehrman recurring footer count regressed")

require(de["totalPdfPages"] == 1479, "De Decretis PDF page count changed")
require(de["pageSelection"]["firstPage"] == 512, "De Decretis first selected page changed")
require(de["pageSelection"]["lastPage"] == 561, "De Decretis last selected page changed")
require(de["pageSelection"]["pageCount"] == 50, "De Decretis page selection changed")
require(de["preprocessing"]["wordCount"] == 29044, "De Decretis native word count regressed")
require(de["preprocessing"]["rawBlockCount"] == 269, "De Decretis raw block count regressed")
require(de["preprocessing"]["includedBlockCount"] == 269, "De Decretis included block count regressed")
require(de["preprocessing"]["excludedHeaderBlocks"] == 0, "De Decretis false-positive header exclusion")
require(de["preprocessing"]["excludedFooterBlocks"] == 0, "De Decretis false-positive footer exclusion")

def print_summary(label, report, historical):
    s = report["segmentation"]
    observed = s["segmentCount"]
    delta = observed - historical
    ratio = observed / historical if historical else 0

    print()
    print(f"{label}:")
    print(f"  current segments:    {observed}")
    print(f"  historical baseline: {historical}")
    print(f"  delta:               {delta:+d}")
    print(f"  ratio:               {ratio:.2f}x")
    print(f"  heading/fallback:    {s['headingSegmentCount']} / {s['fallbackSegmentCount']}")
    print(f"  pages without seg:   {s['pagesWithoutSegments']}")
    print(f"  multi-segment pages: {s['pagesWithMultipleSegments']}")
    print(f"  max segments/page:   {s['maximumSegmentsOnPage']}")
    print(
        "  chars min/med/avg/max: "
        f"{s['minimumCharacterCount']} / "
        f"{s['medianCharacterCount']:.1f} / "
        f"{s['averageCharacterCount']:.1f} / "
        f"{s['maximumCharacterCount']}"
    )
    print(
        "  small/large segments: "
        f"{s['smallSegmentCount']} / {s['largeSegmentCount']}"
    )

print()
print("RESULT: STRUCTURAL SEGMENTATION DIAGNOSTICS COMPLETE")
print("Historical segment counts are comparison references only; no parity assertion was applied.")
print_summary("Ehrman", ehrman, ehrman_historical)
print_summary("De Decretis", de, de_historical)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_DECRETIS_REPORT}"
