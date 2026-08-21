#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIRECTORY}"

PROJECT="tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj"

EHRMAN_SHA="f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe"
EHRMAN_BYTES="233369762"
DE_DECRETIS_SHA="de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75"
DE_DECRETIS_BYTES="11963985"

EHRMAN_PATH="${DOCUMENT_PROCESSING_EHRMAN_PDF:-}"
DE_DECRETIS_PATH="${DOCUMENT_PROCESSING_DE_DECRETIS_PDF:-}"

EHRMAN_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-heading-boundary-diagnostics.json"
DE_REPORT="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-heading-boundary-diagnostics.json"

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
      "${ROOT_DIRECTORY}/tests/document_corpus/pdf/full" \
      "${HOME}/Downloads" \
      "${HOME}/Documents"
  )" ||
    fail "Could not locate pinned Ehrman PDF."
fi

if [[ -z "${DE_DECRETIS_PATH}" ]]; then
  DE_DECRETIS_PATH="$(
    discover_by_sha \
      "${DE_DECRETIS_SHA}" \
      "${ROOT_DIRECTORY}/tests/document_corpus/pdf/full" \
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
rm -f "${EHRMAN_REPORT}" "${DE_REPORT}"

printf '\n== Heading-boundary diagnostics: Ehrman ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-heading-boundaries-pdf \
  --source "${EHRMAN_PATH}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617

printf '\n== Heading-boundary diagnostics: De Decretis ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-heading-boundaries-pdf \
  --source "${DE_DECRETIS_PATH}" \
  --report "${DE_REPORT}" \
  --pages 512-561

python3 - \
  "${EHRMAN_REPORT}" \
  "${DE_REPORT}" \
  "${EHRMAN_SHA}" \
  "${EHRMAN_BYTES}" \
  "${DE_DECRETIS_SHA}" \
  "${DE_DECRETIS_BYTES}" <<'PY'
import json
import sys
from pathlib import Path

ehrman = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
de = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

ehrman_sha = sys.argv[3]
ehrman_bytes = int(sys.argv[4])
de_sha = sys.argv[5]
de_bytes = int(sys.argv[6])

SCHEMA = "document-processing-heading-boundary-analysis-v1"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
SEGMENTATION = "strict-typography-optional-hints-cross-page-fallback-v4"

def require(condition, message):
    if not condition:
        raise SystemExit(f"HEADING-BOUNDARY DIAGNOSTIC FAILED: {message}")

def validate_common(report, sha, byte_length):
    require(report["schemaVersion"] == SCHEMA, "unexpected report schema")
    require(report["sourceSha256"] == sha, "source SHA changed")
    require(report["sourceByteLength"] == byte_length, "source byte length changed")
    require(report["normalizationProfileId"] == NORMALIZATION, "normalization profile changed")
    require(report["segmentationProfileId"] == SEGMENTATION, "segmentation profile changed")
    parity = report["parity"]
    require(parity["productionOnlyCount"] == 0, "diagnostic missed production headings")
    require(parity["diagnosticOnlyCount"] == 0, "diagnostic invented headings")
    require(
        parity["productionHeadingCount"] == parity["diagnosticHeadingCount"],
        "diagnostic/production heading counts disagree",
    )

validate_common(ehrman, ehrman_sha, ehrman_bytes)
validate_common(de, de_sha, de_bytes)

# 8.4f strict-typography production baseline.
require(ehrman["totalPdfPages"] == 617, "Ehrman PDF page count changed")
require(ehrman["pageSelection"]["pageCount"] == 617, "Ehrman selected page count changed")
require(ehrman["segmentation"]["segmentCount"] == 267, "Ehrman segment count changed")
require(ehrman["segmentation"]["headingSegmentCount"] == 267, "Ehrman heading count changed")
require(ehrman["segmentation"]["fallbackSegmentCount"] == 0, "Ehrman fallback count changed")
require(ehrman["segmentation"]["crossPageSegmentCount"] == 166, "Ehrman cross-page count changed")
require(ehrman["flags"]["smallSegments"] == 50, "Ehrman small-segment count changed")

require(de["totalPdfPages"] == 1479, "De Decretis PDF page count changed")
require(de["pageSelection"]["pageCount"] == 50, "De Decretis selected page count changed")
require(de["segmentation"]["segmentCount"] == 50, "De Decretis segment count changed")
require(de["segmentation"]["headingSegmentCount"] == 0, "De Decretis false heading returned")
require(de["segmentation"]["fallbackSegmentCount"] == 50, "De Decretis fallback count changed")
require(de["segmentation"]["crossPageSegmentCount"] == 0, "De Decretis cross-page count changed")

def origin_counts(report):
    return {
        item["origin"]: item["count"]
        for item in report["originCounts"]
    }

def print_summary(label, report, historical):
    seg = report["segmentation"]
    flags = report["flags"]
    origins = origin_counts(report)

    print()
    print(f"{label}:")
    print(
        f"  segments={seg['segmentCount']} "
        f"historical={historical} "
        f"delta={seg['segmentCount'] - historical:+d}"
    )
    print(
        "  decision origins: "
        + ", ".join(
            f"{name}={count}"
            for name, count in sorted(origins.items())
        )
    )
    print(
        "  review flags: "
        f"numbered={flags['numberedStructural']} "
        f"weak_font={flags['weakTypography']} "
        f"repeated_instances={flags['repeatedHeadingInstances']} "
        f"small={flags['smallSegments']} "
        f"large={flags['largeSegments']} "
        f"very_large_cross_page={flags['veryLargeCrossPageSegments']}"
    )
    print(
        "  repeated heading groups: "
        f"{len(report['repeatedHeadingGroups'])}"
    )

print()
print("RESULT: HEADING-BOUNDARY DIAGNOSTICS COMPLETE")
print("Diagnostic mirror matched production heading boundaries; this command does not modify production.")
print_summary("Ehrman", ehrman, 277)
print_summary("De Decretis", de, 50)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_REPORT}"
