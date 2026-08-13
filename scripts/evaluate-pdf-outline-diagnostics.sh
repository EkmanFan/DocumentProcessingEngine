#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

EHRMAN_SHA="f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe"
EHRMAN_BYTES="233369762"
DE_SHA="de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75"
DE_BYTES="11963985"

EHRMAN="${DOCUMENT_PROCESSING_EHRMAN_PDF:-}"
DE="${DOCUMENT_PROCESSING_DE_DECRETIS_PDF:-}"

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
    --ehrman)
      EHRMAN="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --de-decretis)
      DE="$(read_value "$1" "${2:-}")"
      shift 2
      ;;
    --help|-h)
      cat <<'HELP'
Usage:
  bash scripts/evaluate-pdf-outline-diagnostics.sh \
    --ehrman /absolute/path/ehrman.pdf \
    --de-decretis /absolute/path/npnf204.pdf
HELP
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

[[ -f "${EHRMAN}" ]] ||
  fail "Ehrman PDF not found: ${EHRMAN}"

[[ -f "${DE}" ]] ||
  fail "De Decretis PDF not found: ${DE}"

mkdir -p "${REPO}/scripts/tmp"

EHRMAN_REPORT="${REPO}/scripts/tmp/ehrman-pdf-outline-diagnostics.json"
DE_REPORT="${REPO}/scripts/tmp/de-decretis-pdf-outline-diagnostics.json"

cd "${REPO}"

printf '\n== PDF outline diagnostics: Ehrman ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  analyze-outline-pdf \
  --source "${EHRMAN}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617

printf '\n== PDF outline diagnostics: De Decretis ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  analyze-outline-pdf \
  --source "${DE}" \
  --report "${DE_REPORT}" \
  --pages 512-561

python3 - \
  "${EHRMAN_REPORT}" \
  "${DE_REPORT}" \
  "${EHRMAN_SHA}" \
  "${EHRMAN_BYTES}" \
  "${DE_SHA}" \
  "${DE_BYTES}" <<'PY'
import json
import sys
from pathlib import Path

(
    ehrman_path,
    de_path,
    ehrman_sha,
    ehrman_bytes,
    de_sha,
    de_bytes,
) = sys.argv[1:]

ehrman = json.loads(Path(ehrman_path).read_text(encoding="utf-8"))
de = json.loads(Path(de_path).read_text(encoding="utf-8"))

SCHEMA = "document-processing-pdf-outline-analysis-v1"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
SEGMENTATION = "strict-typography-optional-hints-cross-page-fallback-v4"

def require(condition, message):
    if not condition:
        raise SystemExit(f"VALIDATION ERROR: {message}")

def validate_common(report, expected_sha, expected_bytes, pages, first, last, headings):
    require(report["schemaVersion"] == SCHEMA, "outline schema changed")
    require(report["sourceSha256"] == expected_sha, "source SHA changed")
    require(report["sourceByteLength"] == int(expected_bytes), "source bytes changed")
    require(report["totalPdfPages"] == pages, "PDF page count changed")
    require(report["pageSelection"]["firstPage"] == first, "first comparison page changed")
    require(report["pageSelection"]["lastPage"] == last, "last comparison page changed")
    require(
        report["pageSelection"]["pageCount"] == last - first + 1,
        "comparison page count changed",
    )
    require(
        report["normalizationProfileId"] == NORMALIZATION,
        "normalization profile changed",
    )
    require(
        report["segmentationProfileId"] == SEGMENTATION,
        "segmentation profile changed",
    )
    require(report["production"]["headingCount"] == headings, "production headings changed")

    outline = report["outline"]
    matches = report["matches"]

    require(
        outline["entryCount"]
        == outline["internalDocumentEntryCount"] + outline["nonInternalEntryCount"],
        "outline entry accounting is inconsistent",
    )
    require(
        matches["selectedInternalEntryCount"]
        == matches["exactTextMatchCount"]
        + matches["normalizedTextMatchCount"]
        + matches["compactTextMatchCount"]
        + matches["unmatchedCount"],
        "selected outline match accounting is inconsistent",
    )
    require(
        matches["matchedProductionHeadingEntryCount"]
        + matches["outlineOnlyMatchedEntryCount"]
        == matches["selectedInternalEntryCount"] - matches["unmatchedCount"],
        "matched outline classification accounting is inconsistent",
    )
    require(
        matches["supportedProductionHeadingCount"]
        + matches["unsupportedProductionHeadingCount"]
        == headings,
        "production heading support accounting is inconsistent",
    )
    require(
        outline["internalEntriesInSelectedRange"]
        == matches["selectedInternalEntryCount"],
        "selected internal outline count differs between report sections",
    )

validate_common(
    ehrman,
    ehrman_sha,
    ehrman_bytes,
    617,
    1,
    617,
    267,
)

validate_common(
    de,
    de_sha,
    de_bytes,
    1479,
    512,
    561,
    0,
)

def print_report(label, report):
    outline = report["outline"]
    matches = report["matches"]

    print()
    print(label)
    print(
        "  outline present / roots / entries / max level: "
        f'{outline["hasOutline"]} / {outline["rootCount"]} / '
        f'{outline["entryCount"]} / {outline["maximumLevel"]}'
    )
    print(
        "  internal / non-internal / coordinates / invalid page: "
        f'{outline["internalDocumentEntryCount"]} / '
        f'{outline["nonInternalEntryCount"]} / '
        f'{outline["internalEntriesWithCoordinates"]} / '
        f'{outline["internalEntriesWithInvalidPage"]}'
    )
    print(
        "  selected internal entries: "
        f'{matches["selectedInternalEntryCount"]}'
    )
    print(
        "  exact / normalized / compact / unmatched: "
        f'{matches["exactTextMatchCount"]} / '
        f'{matches["normalizedTextMatchCount"]} / '
        f'{matches["compactTextMatchCount"]} / '
        f'{matches["unmatchedCount"]}'
    )
    print(
        "  matched production-heading / outline-only entries: "
        f'{matches["matchedProductionHeadingEntryCount"]} / '
        f'{matches["outlineOnlyMatchedEntryCount"]}'
    )
    print(
        "  production headings supported / unsupported by outline: "
        f'{matches["supportedProductionHeadingCount"]} / '
        f'{matches["unsupportedProductionHeadingCount"]}'
    )

print()
print("RESULT: PDF OUTLINE DIAGNOSTICS COMPLETE")
print("Outline counts and match rates are observations, not production gates.")

print_report("Ehrman", ehrman)
print_report("De Decretis", de)

print()
print("Reports:")
print(f"  {ehrman_path}")
print(f"  {de_path}")
PY
