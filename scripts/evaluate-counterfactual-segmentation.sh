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

SCHEMA = "document-processing-counterfactual-segmentation-analysis-v3"
NORMALIZATION = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"
PRODUCTION_SEGMENTATION = "strict-typography-optional-hints-cross-page-fallback-v4"

EXPECTED_POLICIES = [
    "A-ProductionStrictTypographyV4",
    "B-TypographyOnly",
    "C-TypographyPlusStrongExplicit",
    "D-TypographyPlusHints",
    "E-StrictTypographyOnly",
    "F-StrictTypographyPlusHints",
    "G-ProductionStrictTypographyPlusHintsV4",
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

# Freeze production v3 plus the established counterfactual baselines.
expected_ehrman = {
    "A-ProductionStrictTypographyV4": {
        "segmentCount": 267,
        "headingSegmentCount": 267,
        "fallbackSegmentCount": 0,
        "crossPageSegmentCount": 166,
        "smallSegmentCount": 50,
        "largeSegmentCount": 125,
    },
    "B-TypographyOnly": {
        "segmentCount": 278,
        "headingSegmentCount": 278,
        "fallbackSegmentCount": 0,
        "crossPageSegmentCount": 166,
        "smallSegmentCount": 60,
        "largeSegmentCount": 125,
    },
    "C-TypographyPlusStrongExplicit": {
        "segmentCount": 315,
        "headingSegmentCount": 315,
        "fallbackSegmentCount": 0,
        "crossPageSegmentCount": 184,
        "smallSegmentCount": 61,
        "largeSegmentCount": 136,
    },
    "D-TypographyPlusHints": {
        "segmentCount": 285,
        "headingSegmentCount": 285,
        "fallbackSegmentCount": 0,
        "crossPageSegmentCount": 168,
        "smallSegmentCount": 63,
        "largeSegmentCount": 126,
    },
    "G-ProductionStrictTypographyPlusHintsV4": {
        "segmentCount": 274,
        "headingSegmentCount": 274,
        "fallbackSegmentCount": 0,
        "crossPageSegmentCount": 168,
        "smallSegmentCount": 53,
        "largeSegmentCount": 126,
    },
}

for name, expected in expected_ehrman.items():
    actual = ehrman_policies[name]["metrics"]
    for key, value in expected.items():
        require(
            actual[key] == value,
            f"Ehrman 8.4d baseline changed for {name}.{key}: "
            f"expected {value}, got {actual[key]}",
        )

expected_probe_counts = {
    "A-ProductionStrictTypographyV4": {
        "TAKE A STAND": (5, 6),
        "WHAT DO YOU THINK?": (5, 7),
        "SUGGESTIONS FOR FURTHER READING": (18, 21),
    },
    "B-TypographyOnly": {
        "TAKE A STAND": (5, 6),
        "WHAT DO YOU THINK?": (5, 7),
        "SUGGESTIONS FOR FURTHER READING": (18, 21),
    },
    "C-TypographyPlusStrongExplicit": {
        "TAKE A STAND": (5, 6),
        "WHAT DO YOU THINK?": (5, 7),
        "SUGGESTIONS FOR FURTHER READING": (18, 21),
    },
    "D-TypographyPlusHints": {
        "TAKE A STAND": (6, 6),
        "WHAT DO YOU THINK?": (6, 7),
        "SUGGESTIONS FOR FURTHER READING": (18, 21),
    },
    "G-ProductionStrictTypographyPlusHintsV4": {
        "TAKE A STAND": (6, 6),
        "WHAT DO YOU THINK?": (6, 7),
        "SUGGESTIONS FOR FURTHER READING": (18, 21),
    },
}

for policy_name, probes in expected_probe_counts.items():
    actual_probes = {
        item["probe"]: (item["headingMatches"], item["segmentTextMatches"])
        for item in ehrman_policies[policy_name]["probes"]
    }
    require(
        actual_probes == probes,
        f"Ehrman 8.4d probe baseline changed for {policy_name}",
    )

# De Decretis must remain insensitive to every experimental policy.
for name in EXPECTED_POLICIES:
    metrics = de_policies[name]["metrics"]
    require(metrics["segmentCount"] == 50, f"De Decretis {name} segment count changed")
    require(metrics["headingSegmentCount"] == 0, f"De Decretis {name} false heading returned")
    require(metrics["fallbackSegmentCount"] == 50, f"De Decretis {name} fallback count changed")
    require(metrics["crossPageSegmentCount"] == 0, f"De Decretis {name} cross-page count changed")

# All counterfactuals must preserve source-block coverage. The CLI enforces this
# before serializing; here we ensure the normalized corpus baselines remain.
require(ehrman["includedBlockCount"] == 2648, "Ehrman included-block baseline changed")
require(de["includedBlockCount"] == 269, "De Decretis included-block baseline changed")

# The strict gate must only remove automatic boundaries; it cannot invent any.
require(ehrman["strictMinimumHeadingLetterCount"] == 4, "strict minimum letter count changed")
require(abs(ehrman["strictMinimumAlphaNumericRatio"] - 0.55) < 1e-12,
        "strict alphanumeric ratio changed")

comparisons = {
    (item["fromPolicy"], item["toPolicy"]): item
    for item in ehrman["strictGateComparisons"]
}

expected_comparison_keys = {
    ("B-TypographyOnly", "E-StrictTypographyOnly"),
    ("D-TypographyPlusHints", "F-StrictTypographyPlusHints"),
}

require(set(comparisons) == expected_comparison_keys,
        "strict-gate comparison set changed")

for key, comparison in comparisons.items():
    require(
        comparison["addedBoundaryCount"] == 0,
        f"strict gate unexpectedly added boundaries for {key}",
    )

require(
    ehrman_policies["E-StrictTypographyOnly"]["metrics"]["headingSegmentCount"]
    <= ehrman_policies["B-TypographyOnly"]["metrics"]["headingSegmentCount"],
    "strict typography has more headings than typography-only",
)

require(
    ehrman_policies["F-StrictTypographyPlusHints"]["metrics"]["headingSegmentCount"]
    <= ehrman_policies["D-TypographyPlusHints"]["metrics"]["headingSegmentCount"],
    "strict typography+hints has more headings than typography+hints",
)

# Production default v4 must equal the independently reconstructed strict policy E.
production_metrics = ehrman_policies["A-ProductionStrictTypographyV4"]["metrics"]
strict_metrics = ehrman_policies["E-StrictTypographyOnly"]["metrics"]

for key in (
    "segmentCount",
    "headingSegmentCount",
    "fallbackSegmentCount",
    "crossPageSegmentCount",
    "smallSegmentCount",
    "largeSegmentCount",
):
    require(
        production_metrics[key] == strict_metrics[key],
        f"production default v4 diverges from strict counterfactual E for {key}",
    )

production_probes = {
    item["probe"]: (item["headingMatches"], item["segmentTextMatches"])
    for item in ehrman_policies["A-ProductionStrictTypographyV4"]["probes"]
}
strict_probes = {
    item["probe"]: (item["headingMatches"], item["segmentTextMatches"])
    for item in ehrman_policies["E-StrictTypographyOnly"]["probes"]
}
require(
    production_probes == strict_probes,
    "production v3 probes diverge from strict counterfactual E",
)

# Real production hints must equal the independently reconstructed F policy.
hinted_metrics = ehrman_policies["G-ProductionStrictTypographyPlusHintsV4"]["metrics"]
strict_hinted_metrics = ehrman_policies["F-StrictTypographyPlusHints"]["metrics"]

for key in (
    "segmentCount",
    "headingSegmentCount",
    "fallbackSegmentCount",
    "crossPageSegmentCount",
    "smallSegmentCount",
    "largeSegmentCount",
):
    require(
        hinted_metrics[key] == strict_hinted_metrics[key],
        f"production hinted v4 diverges from strict+hints counterfactual F for {key}",
    )

hinted_probes = {
    item["probe"]: (item["headingMatches"], item["segmentTextMatches"])
    for item in ehrman_policies["G-ProductionStrictTypographyPlusHintsV4"]["probes"]
}

strict_hinted_probes = {
    item["probe"]: (item["headingMatches"], item["segmentTextMatches"])
    for item in ehrman_policies["F-StrictTypographyPlusHints"]["probes"]
}

require(
    hinted_probes == strict_hinted_probes,
    "production hinted v4 probes diverge from strict+hints counterfactual F",
)

require(
    ehrman_policies["G-ProductionStrictTypographyPlusHintsV4"]["addedBoundaryCount"] == 7,
    "production hinted v4 did not add exactly the seven validated Ehrman hint boundaries",
)

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
print("RESULT: STRICT HEADING QUALITY-GATE EVALUATION COMPLETE")
print("Production default A must match E; production hinted G must match F.")
print_report("Ehrman", ehrman, ehrman_policies)

print()
print("Ehrman strict-gate deltas")
for item in ehrman["strictGateComparisons"]:
    print(
        f"  {item['fromPolicy']} -> {item['toPolicy']}: "
        f"removed={item['removedBoundaryCount']} "
        f"added={item['addedBoundaryCount']}"
    )
    for sample in item["removedBoundarySamples"][:12]:
        print(
            f"    p{sample['physicalPageNumber']} "
            f"font_ratio={sample['fontRatio']} "
            f"letters={sample['letterCount']} "
            f"alnum_ratio={sample['alphaNumericRatio']:.3f} "
            f"{sample['text']}"
        )

print_report("De Decretis", de, de_policies)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_REPORT}"
