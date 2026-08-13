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

EHRMAN_REPORT="${ROOT_DIRECTORY}/scripts/tmp/ehrman-normalization-parity.json"
DE_DECRETIS_REPORT="${ROOT_DIRECTORY}/scripts/tmp/de-decretis-normalization-parity.json"

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
  local path="$2"
  local expected_sha="$3"
  local expected_bytes="$4"

  path="$(realpath "${path}" 2>/dev/null)" ||
    fail "Cannot resolve ${label} path."

  [[ -f "${path}" ]] ||
    fail "${label} PDF not found: ${path}"

  local actual_sha
  actual_sha="$(sha256sum "${path}" | awk '{print $1}')"

  [[ "${actual_sha}" == "${expected_sha}" ]] ||
    fail "${label} SHA-256 mismatch."

  local actual_bytes
  actual_bytes="$(stat -c '%s' "${path}")"

  [[ "${actual_bytes}" == "${expected_bytes}" ]] ||
    fail "${label} byte length mismatch."

  printf '%s' "${path}"
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

printf '\n== Recurring-margin parity: Ehrman ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-normalized-pdf \
  --source "${EHRMAN_PATH}" \
  --report "${EHRMAN_REPORT}" \
  --pages 1-617 \
  --probe "TAKE A STAND" \
  --probe "WHAT DO YOU THINK?" \
  --probe "SUGGESTIONS FOR FURTHER READING"

printf '\n== Recurring-margin parity: De Decretis ==\n'

dotnet run \
  --project "${PROJECT}" \
  --no-build -- \
  analyze-normalized-pdf \
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
  "${DE_DECRETIS_BYTES}" <<'PY'
import json
import sys
from pathlib import Path

ehrman = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
de_decretis = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

ehrman_sha = sys.argv[3]
ehrman_bytes = int(sys.argv[4])
de_sha = sys.argv[5]
de_bytes = int(sys.argv[6])

EXPECTED_SCHEMA = "document-processing-normalized-pdf-analysis-v1"
EXPECTED_PROFILE = "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1"

def require(condition, message):
    if not condition:
        raise SystemExit(f"NORMALIZATION PARITY FAILED: {message}")

def probe(report, value):
    for item in report.get("probes", []):
        if item.get("probe") == value:
            return item
    raise SystemExit(f"NORMALIZATION PARITY FAILED: missing probe {value!r}")

for report, sha, size in (
    (ehrman, ehrman_sha, ehrman_bytes),
    (de_decretis, de_sha, de_bytes),
):
    require(report["schemaVersion"] == EXPECTED_SCHEMA, "unexpected report schema")
    require(report["sourceSha256"] == sha, "source SHA-256 changed")
    require(report["sourceByteLength"] == size, "source byte length changed")
    require(report["normalizationProfileId"] == EXPECTED_PROFILE, "normalization profile changed")

# Ehrman native + normalization parity
require(ehrman["totalPdfPages"] == 617, "Ehrman PDF page count changed")
require(ehrman["pageSelection"]["pageCount"] == 617, "Ehrman selected page count changed")
require(ehrman["extraction"]["wordCount"] == 233595, "Ehrman native word count regressed")
require(ehrman["extraction"]["blockCount"] == 3179, "Ehrman raw block count regressed")
require(ehrman["extraction"]["pagesWithWords"] == 331, "Ehrman text-page count regressed")
require(ehrman["extraction"]["pagesWithoutWords"] == 286, "Ehrman textless-page count regressed")
require(ehrman["extraction"]["textLayerCoveragePercent"] == 53.6, "Ehrman text coverage regressed")

norm = ehrman["normalization"]
require(norm["blockCount"] == 3179, "Ehrman normalization block count changed")
require(norm["includedBlocks"] == 2648, "Ehrman included block count is not at parity")
require(norm["excludedHeaderBlocks"] == 531, "Ehrman recurring header count is not at parity")
require(norm["excludedFooterBlocks"] == 0, "Ehrman recurring footer count is not at parity")

layout = ehrman["layout"]
require(layout["multiColumnCandidatePages"] == 229, "Ehrman multi-column diagnostic is not at parity")
require(layout["interleavedColumnPages"] == 144, "Ehrman interleaved-column diagnostic is not at parity")
require(layout["verticalReversalPages"] == 10, "Ehrman vertical-reversal diagnostic is not at parity")

for value, word_matches, block_matches in (
    ("TAKE A STAND", 6, 6),
    ("WHAT DO YOU THINK?", 20, 7),
    ("SUGGESTIONS FOR FURTHER READING", 21, 21),
):
    item = probe(ehrman, value)
    require(item["wordStreamMatches"] == word_matches, f"Ehrman {value!r} word-stream count changed")
    require(item["blockMatches"] == block_matches, f"Ehrman {value!r} normalized block count is not at parity")

# De Decretis native + normalization parity
require(de_decretis["totalPdfPages"] == 1479, "De Decretis PDF page count changed")
require(de_decretis["pageSelection"]["firstPage"] == 512, "De Decretis first page changed")
require(de_decretis["pageSelection"]["lastPage"] == 561, "De Decretis last page changed")
require(de_decretis["pageSelection"]["pageCount"] == 50, "De Decretis page selection changed")
require(de_decretis["extraction"]["wordCount"] == 29044, "De Decretis word count regressed")
require(de_decretis["extraction"]["blockCount"] == 269, "De Decretis raw block count regressed")

de_norm = de_decretis["normalization"]
require(de_norm["blockCount"] == 269, "De Decretis normalization block count changed")
require(de_norm["includedBlocks"] == 269, "De Decretis included block count changed")
require(de_norm["excludedHeaderBlocks"] == 0, "De Decretis false-positive header exclusion")
require(de_norm["excludedFooterBlocks"] == 0, "De Decretis false-positive footer exclusion")

de_layout = de_decretis["layout"]
require(de_layout["multiColumnCandidatePages"] == 4, "De Decretis multi-column diagnostic changed")
require(de_layout["interleavedColumnPages"] == 2, "De Decretis interleaved diagnostic changed")
require(de_layout["verticalReversalPages"] == 3, "De Decretis vertical-reversal diagnostic changed")

amen = probe(de_decretis, "endless ages of ages. Amen.")
require(amen["wordStreamMatches"] == 1, "De Decretis sentinel word-stream count changed")
require(amen["blockMatches"] == 1, "De Decretis sentinel normalized block count changed")

print()
print("RESULT: RECURRING MARGIN PARITY PASS")
print(
    "Ehrman: "
    f"headers={norm['excludedHeaderBlocks']} "
    f"footers={norm['excludedFooterBlocks']} "
    f"included={norm['includedBlocks']} "
    f"multicolumn={layout['multiColumnCandidatePages']} "
    f"interleaved={layout['interleavedColumnPages']} "
    f"vertical_reversal={layout['verticalReversalPages']}"
)
print(
    "De Decretis: "
    f"headers={de_norm['excludedHeaderBlocks']} "
    f"footers={de_norm['excludedFooterBlocks']} "
    f"multicolumn={de_layout['multiColumnCandidatePages']} "
    f"interleaved={de_layout['interleavedColumnPages']} "
    f"vertical_reversal={de_layout['verticalReversalPages']}"
)
PY

printf '\nReports:\n'
printf '  %s\n' "${EHRMAN_REPORT}"
printf '  %s\n' "${DE_DECRETIS_REPORT}"
