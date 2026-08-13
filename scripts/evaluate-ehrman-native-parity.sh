#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIRECTORY}"

PROJECT="tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj"

EXPECTED_SHA="f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe"
EXPECTED_BYTES="233369762"

SOURCE_PATH="${DOCUMENT_PROCESSING_EHRMAN_PDF:-}"

FULL_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-native-parity-full.json"
TEXT_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-native-parity-pages-398-405.json"
RASTER_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-native-parity-pages-14-20.json"

fail() {
  printf '\nERROR: %s\n' "$*" >&2
  exit 1
}

usage() {
  cat <<'USAGE'
Usage:
  bash scripts/evaluate-ehrman-native-parity.sh \
    [--ehrman /absolute/path/ehrman.pdf]

The evaluation validates native extraction parity only.

The historical post-normalization targets:
- 531 recurring headers excluded;
- 0 recurring footers excluded;
- 229 multi-column candidate pages;
- 144 interleaved-column pages;
- 10 vertical reading-order reversal pages;
- normalized block-probe parity;

are deliberately deferred until Document Processing Engine has a normalization
stage equivalent to the historical ApologiaStudio normalizer.
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
    --ehrman)
      SOURCE_PATH="$(read_value "$1" "${2:-}")"
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

for command in dotnet sha256sum python3 realpath find awk stat; do
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

      if [[ "${name}" != *"9780197754023"* &&
            ! ( "${name}" == *"new-testament"* &&
                "${name}" == *"historical-introduction"* ) ]]; then
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
    fail "Could not locate the pinned Ehrman source. Use --ehrman."
fi

SOURCE_PATH="$(realpath "${SOURCE_PATH}" 2>/dev/null)" ||
  fail "Cannot resolve Ehrman source path."

[[ -f "${SOURCE_PATH}" ]] ||
  fail "Source file not found: ${SOURCE_PATH}"

ACTUAL_SHA="$(sha256sum "${SOURCE_PATH}" | awk '{print $1}')"
[[ "${ACTUAL_SHA}" == "${EXPECTED_SHA}" ]] ||
  fail "Source SHA-256 mismatch."

ACTUAL_BYTES="$(stat -c '%s' "${SOURCE_PATH}")"
[[ "${ACTUAL_BYTES}" == "${EXPECTED_BYTES}" ]] ||
  fail "Source byte length mismatch."

mkdir -p "${ROOT_DIRECTORY}/scripts/tmp"
rm -f "${FULL_REPORT}" "${TEXT_REPORT}" "${RASTER_REPORT}"

printf '\n== Ehrman native PDF parity ==\n'
printf 'Source: %s\n' "${SOURCE_PATH}"
printf 'SHA-256: %s\n\n' "${ACTUAL_SHA}"

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-pdf \
  --source "${SOURCE_PATH}" \
  --report "${FULL_REPORT}" \
  --pages 1-617 \
  --probe "TAKE A STAND" \
  --probe "WHAT DO YOU THINK?" \
  --probe "SUGGESTIONS FOR FURTHER READING"

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-pdf \
  --source "${SOURCE_PATH}" \
  --report "${TEXT_REPORT}" \
  --pages 398-405 \
  --probe "TAKE A STAND" \
  --probe "WHAT DO YOU THINK?" \
  --probe "SUGGESTIONS FOR FURTHER READING"

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-pdf \
  --source "${SOURCE_PATH}" \
  --report "${RASTER_REPORT}" \
  --pages 14-20

python3 - \
  "${FULL_REPORT}" \
  "${TEXT_REPORT}" \
  "${RASTER_REPORT}" \
  "${EXPECTED_SHA}" \
  "${EXPECTED_BYTES}" <<'PY'
import json
import sys
from pathlib import Path

full = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
text = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
raster = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
expected_sha = sys.argv[4]
expected_bytes = int(sys.argv[5])

def require(condition, message):
    if not condition:
        raise SystemExit(f"NATIVE PARITY FAILED: {message}")

def get_probe(report, value):
    for item in report.get("probes", []):
        if item.get("probe") == value:
            return item
    raise SystemExit(f"NATIVE PARITY FAILED: missing probe {value!r}")

for report in (full, text, raster):
    require(
        report.get("schemaVersion") ==
        "document-processing-native-pdf-analysis-v1",
        "unexpected report schema")
    require(
        report.get("sourceSha256") == expected_sha,
        "source SHA-256 changed")
    require(
        report.get("sourceByteLength") == expected_bytes,
        "source byte length changed")
    require(
        report.get("totalPdfPages") == 617,
        "PDF page count changed")

selection = full["pageSelection"]
extraction = full["extraction"]

require(
    selection["firstPage"] == 1 and
    selection["lastPage"] == 617 and
    selection["pageCount"] == 617,
    "full page selection changed")

require(
    extraction["wordCount"] == 233595,
    "full word count is not at ApologiaStudio native parity")
require(
    extraction["blockCount"] == 3179,
    "full block count is not at ApologiaStudio native parity")
require(
    extraction["pagesWithWords"] == 331,
    "pages-with-native-text count is not at parity")
require(
    extraction["pagesWithoutWords"] == 286,
    "textless-page count is not at parity")
require(
    extraction["textLayerCoveragePercent"] == 53.6,
    "text-layer coverage is not at parity")
require(
    extraction["textlessPagesWithDominantRasterImage"] == 286,
    "dominant-raster textless-page count is not at parity")

for value, expected in (
    ("TAKE A STAND", 6),
    ("WHAT DO YOU THINK?", 20),
    ("SUGGESTIONS FOR FURTHER READING", 21),
):
    observed = get_probe(full, value)
    require(
        observed["wordStreamMatches"] == expected,
        f"{value!r} native word-stream count is not at parity")

text_selection = text["pageSelection"]
text_extraction = text["extraction"]

require(
    text_selection["firstPage"] == 398 and
    text_selection["lastPage"] == 405 and
    text_selection["pageCount"] == 8,
    "born-digital reference range changed")
require(
    text_extraction["pagesWithWords"] == 7,
    "born-digital 398-405 native page count is not at parity")
require(
    text_extraction["pagesWithoutWords"] == 1,
    "born-digital 398-405 textless page count is not at parity")
require(
    text_extraction["wordCount"] == 4728,
    "born-digital 398-405 word count is not at parity")
require(
    text_extraction["blockCount"] == 87,
    "born-digital 398-405 block count is not at parity")
require(
    text_extraction["textlessPagesWithDominantRasterImage"] == 1,
    "born-digital 398-405 raster diagnostic is not at parity")

raster_selection = raster["pageSelection"]
raster_extraction = raster["extraction"]

require(
    raster_selection["firstPage"] == 14 and
    raster_selection["lastPage"] == 20 and
    raster_selection["pageCount"] == 7,
    "raster reference range changed")
require(
    raster_extraction["pagesWithWords"] == 0,
    "raster 14-20 unexpectedly contains native words")
require(
    raster_extraction["pagesWithoutWords"] == 7,
    "raster 14-20 textless-page count is not at parity")
require(
    raster_extraction["wordCount"] == 0,
    "raster 14-20 word count is not zero")
require(
    raster_extraction["blockCount"] == 0,
    "raster 14-20 block count is not zero")
require(
    raster_extraction["textlessPagesWithDominantRasterImage"] == 7,
    "raster 14-20 dominant-raster count is not at parity")

layout = full["rawLayout"]
take = get_probe(full, "TAKE A STAND")
think = get_probe(full, "WHAT DO YOU THINK?")
reading = get_probe(full, "SUGGESTIONS FOR FURTHER READING")

print()
print("RESULT: EHRMAN NATIVE PARITY PASS")
print(
    "Full native extraction: "
    f"pages={selection['pageCount']} "
    f"words={extraction['wordCount']} "
    f"blocks={extraction['blockCount']} "
    f"coverage={extraction['textLayerCoveragePercent']:.1f}% "
    f"textless={extraction['pagesWithoutWords']} "
    f"dominant_raster={extraction['textlessPagesWithDominantRasterImage']}"
)
print(
    "Native word-stream probes: "
    f"TAKE_A_STAND={take['wordStreamMatches']} "
    f"WHAT_DO_YOU_THINK={think['wordStreamMatches']} "
    f"FURTHER_READING={reading['wordStreamMatches']}"
)
print(
    "Raw layout observations (not post-normalization assertions): "
    f"multicolumn={layout['multiColumnCandidatePages']} "
    f"interleaved={layout['interleavedColumnPages']} "
    f"vertical_reversal={layout['verticalReversalPages']}"
)
print(
    "Raw block probe observations (not post-normalization assertions): "
    f"TAKE_A_STAND={take['blockMatches']} "
    f"WHAT_DO_YOU_THINK={think['blockMatches']} "
    f"FURTHER_READING={reading['blockMatches']}"
)
print()
print(
    "Deferred to normalization: "
    "531 recurring headers, 0 recurring footers, "
    "229 multicolumn, 144 interleaved, 10 vertical reversals, "
    "and normalized block-probe parity."
)
PY

printf '\nReports:\n'
printf '  %s\n' "${FULL_REPORT}"
printf '  %s\n' "${TEXT_REPORT}"
printf '  %s\n' "${RASTER_REPORT}"
