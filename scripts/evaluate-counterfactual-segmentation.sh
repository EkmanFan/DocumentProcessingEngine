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

EHRMAN_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-counterfactual-segmentation.json"
DE_REPORT="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-counterfactual-segmentation.json"

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
rm -f "${EHRMAN_REPORT}" "${DE_REPORT}"

printf '\n== Counterfactual segmentation: Ehrman ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-counterfactual-segmentation-pdf \
  --source "${EHRMAN_PATH}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617 \
  --historical-segments 277 \
  --hint "TAKE A STAND" \
  --hint "WHAT DO YOU THINK?" \
  --hint "SUGGESTIONS FOR FURTHER READING" \
  --probe "TAKE A STAND" \
  --probe "WHAT DO YOU THINK?" \
  --probe "SUGGESTIONS FOR FURTHER READING"

printf '\n== Counterfactual segmentation: De Decretis ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-counterfactual-segmentation-pdf \
  --source "${DE_DECRETIS_PATH}" \
  --report "${DE_REPORT}" \
  --pages 512-561 \
  --historical-segments 50 \
  --hint "TAKE A STAND" \
  --hint "WHAT DO YOU THINK?" \
  --hint "SUGGESTIONS FOR FURTHER READING" \
  --probe "endless ages of ages. Amen."

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

SCHEMA = "document-processing-counterfactual-segmentation-analysis-v1"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
PRODUCTION_SEGMENTATION = "typography-aware-cross-page-fallback-v2"

EXPECTED_POLICIES = [
    "A-ProductionV2",
    "B-TypographyOnly",
    "C-TypographyPlusStrongExplicit",
    "D-TypographyPlusHints",
]

def require(condition, message):
    if not condition:
        raise SystemExit(f"COUNTERFACTUAL EVALUATION FAILED: {message}")

def by_name(report):
    return {item["name"]: item for item in report["policies"]}

def validate_common(report, sha, byte_length):
    require(report["schemaVersion"] == SCHEMA, "unexpected schema")
    require(report["sourceSha256"] == sha, "source SHA changed")
    require(report["sourceByteLength"] == byte_length, "source byte length changed")
    require(report["normalizationProfileId"] == NORMALIZATION, "normalization profile changed")
    require(
        report["productionSegmentationProfileId"] == PRODUCTION_SEGMENTATION,
        "production segmentation profile changed",
    )
    policies = by_name(report)
    require(list(policies) == EXPECTED_POLICIES, "policy set/order changed")
    return policies

ehrman_policies = validate_common(ehrman, ehrman_sha, ehrman_bytes)
de_policies = validate_common(de, de_sha, de_bytes)

# Freeze the production baseline. Counterfactual outcomes remain observations.
production = ehrman_policies["A-ProductionV2"]["metrics"]
require(production["segmentCount"] == 380, "Ehrman production segment count changed")
require(production["headingSegmentCount"] == 380, "Ehrman production heading count changed")
require(production["fallbackSegmentCount"] == 0, "Ehrman production fallback count changed")
require(production["crossPageSegmentCount"] == 204, "Ehrman production cross-page count changed")

production_de = de_policies["A-ProductionV2"]["metrics"]
require(production_de["segmentCount"] == 50, "De Decretis production segment count changed")
require(production_de["headingSegmentCount"] == 0, "De Decretis production false heading returned")
require(production_de["fallbackSegmentCount"] == 50, "De Decretis production fallback count changed")
require(production_de["crossPageSegmentCount"] == 0, "De Decretis production cross-page count changed")

# All counterfactuals must preserve source-block coverage. The CLI enforces this
# before serializing; here we only ensure the expected normalized corpora remain.
require(ehrman["includedBlockCount"] == 2648, "Ehrman included-block baseline changed")
require(de["includedBlockCount"] == 269, "De Decretis included-block baseline changed")

def print_report(label, report, policies):
    print()
    print(label)
    print(
        f"  body_font={report['weightedMedianBodyFontSize']} "
        f"historical={report['historicalSegmentCount']} "
        f"included_blocks={report['includedBlockCount']}"
    )
    for name in EXPECTED_POLICIES:
        policy = policies[name]
        m = policy["metrics"]
        print(
            f"  {name}: "
            f"segments={m['segmentCount']} "
            f"delta={policy['deltaFromHistorical']:+d} "
            f"heading={m['headingSegmentCount']} "
            f"fallback={m['fallbackSegmentCount']} "
            f"cross_page={m['crossPageSegmentCount']} "
            f"small={m['smallSegmentCount']} "
            f"large={m['largeSegmentCount']} "
            f"removed={policy['removedProductionBoundaryCount']} "
            f"added={policy['addedBoundaryCount']}"
        )
        for probe in policy["probes"]:
            print(
                f"    probe={probe['probe']!r} "
                f"heading={probe['headingMatches']} "
                f"text={probe['segmentTextMatches']}"
            )

print()
print("RESULT: COUNTERFACTUAL SEGMENTATION EVALUATION COMPLETE")
print("Only production A is a regression gate; B/C/D remain observational.")
print_report("Ehrman", ehrman, ehrman_policies)
print_report("De Decretis", de, de_policies)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_REPORT}"
