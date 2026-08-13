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
  bash scripts/evaluate-pdf-outline-alignment-diagnostics.sh \
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

EHRMAN_REPORT="${REPO}/scripts/tmp/ehrman-pdf-outline-alignment-diagnostics.json"
DE_REPORT="${REPO}/scripts/tmp/de-decretis-pdf-outline-alignment-diagnostics.json"

cd "${REPO}"

printf '\n== PDF outline/content alignment diagnostics: Ehrman ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  analyze-outline-alignment-pdf \
  --source "${EHRMAN}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617

printf '\n== PDF outline/content alignment diagnostics: De Decretis ==\n'

dotnet run \
  --no-build \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  analyze-outline-alignment-pdf \
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

SCHEMA = "document-processing-pdf-outline-alignment-analysis-v1"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
SEGMENTATION = "strict-typography-optional-hints-cross-page-fallback-v4"

def require(condition, message):
    if not condition:
        raise SystemExit(f"VALIDATION ERROR: {message}")

def count_sum(mapping):
    return sum(mapping.values())

def validate_common(
    report,
    expected_sha,
    expected_bytes,
    pages,
    first,
    last,
    roots,
    global_entries,
    selected_entries,
    production_headings,
):
    require(report["schemaVersion"] == SCHEMA, "alignment schema changed")
    require(report["sourceSha256"] == expected_sha, "source SHA changed")
    require(report["sourceByteLength"] == int(expected_bytes), "source bytes changed")
    require(report["totalPdfPages"] == pages, "PDF page count changed")
    require(report["pageSelection"]["firstPage"] == first, "first page changed")
    require(report["pageSelection"]["lastPage"] == last, "last page changed")
    require(
        report["pageSelection"]["pageCount"] == last - first + 1,
        "selected page count changed",
    )
    require(
        report["normalizationProfileId"] == NORMALIZATION,
        "normalization profile changed",
    )
    require(
        report["segmentationProfileId"] == SEGMENTATION,
        "segmentation profile changed",
    )
    require(report["outlineRootCount"] == roots, "outline root count changed")
    require(report["outlineEntryCount"] == global_entries, "global outline entry count changed")
    require(
        report["productionHeadingCount"] == production_headings,
        "production heading count changed",
    )

    summary = report["summary"]

    require(
        summary["selectedInternalEntryCount"] == selected_entries,
        "selected internal outline count changed",
    )
    require(
        len(report["entries"]) == selected_entries,
        "entry detail count differs from selected outline count",
    )
    require(
        summary["bestCandidateOnTargetPage"]
        + summary["bestCandidateOnNearbyPage"]
        == summary["entriesWithBestCandidate"],
        "best-candidate page accounting is inconsistent",
    )
    require(
        summary["bestCandidateProductionHeading"]
        + summary["bestCandidateNonHeading"]
        == summary["entriesWithBestCandidate"],
        "best-candidate heading accounting is inconsistent",
    )
    require(
        summary["plausibleAlignmentCount"]
        + summary["exploratoryCandidateCount"]
        + summary["noCandidateCount"]
        == selected_entries,
        "plausible/exploratory/no-candidate accounting is inconsistent",
    )
    require(
        summary["plausibleAlignmentOnTargetPage"]
        + summary["plausibleAlignmentOnNearbyPage"]
        == summary["plausibleAlignmentCount"],
        "plausible-alignment page accounting is inconsistent",
    )
    require(
        summary["plausibleAlignmentProductionHeading"]
        + summary["plausibleAlignmentNonHeading"]
        == summary["plausibleAlignmentCount"],
        "plausible-alignment heading accounting is inconsistent",
    )
    require(
        count_sum(summary["bandCounts"]) == summary["entriesWithBestCandidate"],
        "alignment-band accounting is inconsistent",
    )
    require(
        summary["plausibleAlignmentCount"]
        == summary["bandCounts"]["ExactEquivalent"]
        + summary["bandCounts"]["Containment"]
        + summary["bandCounts"]["HighOverlap"],
        "plausible-alignment count differs from the documented bands",
    )
    require(
        summary["exploratoryCandidateCount"]
        == summary["bandCounts"]["ModerateOverlap"]
        + summary["bandCounts"]["WeakOverlap"]
        + summary["bandCounts"]["None"],
        "exploratory-candidate count differs from the documented bands",
    )
    require(
        count_sum(summary["numericLabelRelationCounts"])
        == summary["entriesWithBestCandidate"],
        "numeric-label accounting is inconsistent",
    )

validate_common(
    ehrman,
    ehrman_sha,
    ehrman_bytes,
    617,
    1,
    617,
    40,
    48,
    48,
    267,
)

validate_common(
    de,
    de_sha,
    de_bytes,
    1479,
    512,
    561,
    30,
    471,
    8,
    0,
)

def print_report(label, report):
    summary = report["summary"]

    print()
    print(label)
    print(
        "  target pages words / blocks / textless-raster: "
        f'{summary["targetPagesWithWords"]} / '
        f'{summary["targetPagesWithBlocks"]} / '
        f'{summary["targetPagesTextlessDominantRaster"]}'
    )
    print(
        "  destinations normalized left / top: "
        f'{summary["destinationsWithNormalizedLeft"]} / '
        f'{summary["destinationsWithNormalizedTop"]}'
    )
    print(
        "  best candidates total / target / nearby: "
        f'{summary["entriesWithBestCandidate"]} / '
        f'{summary["bestCandidateOnTargetPage"]} / '
        f'{summary["bestCandidateOnNearbyPage"]}'
    )
    print(
        "  plausible / exploratory / none: "
        f'{summary["plausibleAlignmentCount"]} / '
        f'{summary["exploratoryCandidateCount"]} / '
        f'{summary["noCandidateCount"]}'
    )
    print(
        "  plausible target / nearby: "
        f'{summary["plausibleAlignmentOnTargetPage"]} / '
        f'{summary["plausibleAlignmentOnNearbyPage"]}'
    )
    print(
        "  best candidate production-heading / non-heading: "
        f'{summary["bestCandidateProductionHeading"]} / '
        f'{summary["bestCandidateNonHeading"]}'
    )
    print(
        "  plausible production-heading / non-heading: "
        f'{summary["plausibleAlignmentProductionHeading"]} / '
        f'{summary["plausibleAlignmentNonHeading"]}'
    )
    print("  bands:")
    for key, value in summary["bandCounts"].items():
        print(f"    {key}: {value}")
    print("  numeric-label relation:")
    for key, value in summary["numericLabelRelationCounts"].items():
        print(f"    {key}: {value}")

print()
print("RESULT: PDF OUTLINE/CONTENT ALIGNMENT DIAGNOSTICS COMPLETE")
print("Alignment bands are transparent diagnostic categories, not production confidence scores.")
print("Nearby-page candidates do not imply an incorrect bookmark destination; they only locate native text when the target page itself may be raster/textless.")

print_report("Ehrman", ehrman)
print_report("De Decretis", de)

print()
print("Reports:")
print(f"  {ehrman_path}")
print(f"  {de_path}")
PY
