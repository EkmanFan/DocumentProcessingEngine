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

EHRMAN_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-typography-diagnostics.json"
DE_REPORT="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-typography-diagnostics.json"

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

printf '\n== Typography diagnostics: Ehrman ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-typography-pdf \
  --source "${EHRMAN_PATH}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617

printf '\n== Typography diagnostics: De Decretis ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-typography-pdf \
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

SCHEMA = "document-processing-typography-pdf-analysis-v2"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
SEGMENTATION = "strict-typography-optional-hints-cross-page-fallback-v4"

def require(condition, message):
    if not condition:
        raise SystemExit(f"TYPOGRAPHY DIAGNOSTIC FAILED: {message}")

def validate(report, sha, size):
    require(report["schemaVersion"] == SCHEMA, "unexpected report schema")
    require(report["sourceSha256"] == sha, "source SHA changed")
    require(report["sourceByteLength"] == size, "source size changed")
    require(report["normalizationProfileId"] == NORMALIZATION, "normalization profile changed")
    require(report["segmentationProfileId"] == SEGMENTATION, "segmentation profile changed")

validate(ehrman, ehrman_sha, ehrman_bytes)
validate(de, de_sha, de_bytes)

# Guard established upstream baselines. Typography values remain observational.
require(ehrman["totalPdfPages"] == 617, "Ehrman page count changed")
require(ehrman["pageSelection"]["pageCount"] == 617, "Ehrman selection changed")
require(ehrman["coverage"]["wordCount"] == 233595, "Ehrman word count regressed")
require(ehrman["coverage"]["rawBlockCount"] == 3179, "Ehrman raw block count regressed")
require(ehrman["coverage"]["includedBlockCount"] == 2648, "Ehrman included block count regressed")

require(de["totalPdfPages"] == 1479, "De Decretis page count changed")
require(de["pageSelection"]["pageCount"] == 50, "De Decretis selection changed")
require(de["coverage"]["wordCount"] == 29044, "De Decretis word count regressed")
require(de["coverage"]["rawBlockCount"] == 269, "De Decretis raw block count regressed")
require(de["coverage"]["includedBlockCount"] == 269, "De Decretis included block count regressed")

def pct(a, b):
    return 0 if not b else a * 100.0 / b

def summary(label, report):
    c = report["coverage"]
    h = report["headingComparison"]
    f = report["fontCandidates"]

    print()
    print(f"{label}:")
    print(f"  body font weighted median: {report['weightedMedianBodyFontSize']}")
    print(
        "  word typography coverage: "
        f"font={pct(c['wordsWithFontName'], c['wordCount']):.1f}% "
        f"size={pct(c['wordsWithPointSize'], c['wordCount']):.1f}%"
    )
    print(
        "  included-block typography: "
        f"font={pct(c['includedBlocksWithFontName'], c['includedBlockCount']):.1f}% "
        f"size={pct(c['includedBlocksWithPointSize'], c['includedBlockCount']):.1f}%"
    )
    print(
        "  historical font candidates: "
        f"{f['total']} "
        f"(sub={f['subsection']}, sec={f['section']}, chap={f['chapter']})"
    )
    print(
        "  current segmenter headings / overlap / segmenter-only / font-only: "
        f"{h['currentSegmenterHeadings']} / {h['overlap']} / {h['segmenterOnly']} / {h['fontOnly']}"
    )

print()
print("RESULT: TYPOGRAPHY DIAGNOSTICS COMPLETE")
print("Typography values are observations; no font-threshold parity assertion was applied.")
summary("Ehrman", ehrman)
summary("De Decretis", de)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_REPORT}"
