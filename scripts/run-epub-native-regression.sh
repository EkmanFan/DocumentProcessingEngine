#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFERENCE="$REPO/docs/evaluation/habermas-epub-native-reference-v1.json"
EPUB_ZERO_REFERENCE="$REPO/docs/evaluation/habermas-epub-reference-v1.json"
EPUB_FILE="${HABERMAS_EPUB_FILE:-$REPO/tests/document_corpus/epub/habermas-case-for-resurrection.epub}"
EPUBCHECK_ZIP="${EPUBCHECK_ZIP:-$REPO/scripts/tmp/tool-cache/epubcheck-5.3.0.zip}"
OUT="$REPO/scripts/tmp/epub-native-regression"
REPORT="$OUT/report.json"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

for command in dotnet jq sha256sum unzip; do
  command -v "$command" >/dev/null 2>&1 || fail "$command was not found."
done

[[ -f "$REFERENCE" ]] || fail "Native EPUB reference is missing: $REFERENCE"
[[ -f "$EPUB_ZERO_REFERENCE" ]] || fail "EPUB-0 reference is missing: $EPUB_ZERO_REFERENCE"
[[ -f "$EPUB_FILE" ]] || fail "Habermas EPUB is missing: $EPUB_FILE"
[[ -f "$EPUBCHECK_ZIP" ]] || fail "EPUBCheck distribution is missing: $EPUBCHECK_ZIP"

expected_source_sha="$(jq -r '.source.sha256' "$REFERENCE")"
expected_source_bytes="$(jq -r '.source.byteLength' "$REFERENCE")"
expected_checker_version="$(jq -r '.conformance.epubCheckVersion' "$EPUB_ZERO_REFERENCE")"
expected_checker_sha="$(jq -r '.conformance.epubCheckDistributionSha256' "$EPUB_ZERO_REFERENCE")"

observed_source_sha="$(sha256sum "$EPUB_FILE" | awk '{print $1}')"
observed_source_bytes="$(stat -c '%s' "$EPUB_FILE")"
observed_checker_sha="$(sha256sum "$EPUBCHECK_ZIP" | awk '{print $1}')"

[[ "$observed_source_sha" == "$expected_source_sha" ]] || fail "Habermas EPUB SHA-256 differs from the frozen reference."
[[ "$observed_source_bytes" == "$expected_source_bytes" ]] || fail "Habermas EPUB byte length differs from the frozen reference."
[[ "$observed_checker_sha" == "$expected_checker_sha" ]] || fail "EPUBCheck distribution SHA-256 differs from the frozen reference."

mkdir -p "$OUT"
rm -f "$REPORT"

WORK="$(mktemp -d "$OUT/work.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

unzip -q "$EPUBCHECK_ZIP" -d "$WORK"
EPUBCHECK_DIRECTORY="$WORK/epubcheck-$expected_checker_version"

[[ -f "$EPUBCHECK_DIRECTORY/epubcheck.jar" ]] || fail "Pinned EPUBCheck JAR is missing after extraction."

printf 'DPEngine EPUB-1 native regression\n'
printf 'Reference: %s\n' "$REFERENCE"
printf 'EPUB: %s\n' "$EPUB_FILE"
printf 'EPUBCheck: %s\n\n' "$expected_checker_version"

dotnet build \
  "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  --warnaserror

dotnet run \
  --project "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  --no-build \
  -- \
  analyze-epub \
  --source "$EPUB_FILE" \
  --epubcheck-distribution "$EPUBCHECK_DIRECTORY" \
  --report "$REPORT"

jq -e \
  --slurpfile expected "$REFERENCE" \
  '. == $expected[0]' \
  "$REPORT" >/dev/null || fail "Native EPUB processing report differs from the frozen reference."

printf '\nEPUB-1 NATIVE REGRESSION: PASS\n'
printf 'Report: %s\n' "$REPORT"
