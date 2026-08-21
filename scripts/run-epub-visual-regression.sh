#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFERENCE="$REPO/docs/evaluation/habermas-epub-visual-reference-v1.json"
NATIVE_REFERENCE="$REPO/docs/evaluation/habermas-epub-native-reference-v1.json"
EPUB_ZERO_REFERENCE="$REPO/docs/evaluation/habermas-epub-reference-v1.json"
EPUB_FILE="${HABERMAS_EPUB_FILE:-$REPO/tests/document_corpus/epub/habermas-case-for-resurrection.epub}"
EPUBCHECK_ZIP="${EPUBCHECK_ZIP:-$REPO/scripts/tmp/tool-cache/epubcheck-5.3.0.zip}"
OUT="$REPO/scripts/tmp/epub-visual-regression"
NATIVE_REPORT="$OUT/native-report.json"
VISUAL_REPORT="$OUT/visual-report.json"
NATIVE_OPT_IN_REPORT="$OUT/native-opt-in-report.json"
VISUAL_OPT_IN_REPORT="$OUT/visual-opt-in-report.json"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

for command in dotnet jq sha256sum unzip; do
  command -v "$command" >/dev/null 2>&1 || fail "$command was not found."
done

[[ -f "$REFERENCE" ]] || fail "EPUB visual reference is missing: $REFERENCE"
[[ -f "$NATIVE_REFERENCE" ]] || fail "Native EPUB reference is missing: $NATIVE_REFERENCE"
[[ -f "$EPUB_ZERO_REFERENCE" ]] || fail "EPUB-0 reference is missing: $EPUB_ZERO_REFERENCE"
[[ -f "$EPUB_FILE" ]] || fail "Habermas EPUB is missing: $EPUB_FILE"
[[ -f "$EPUBCHECK_ZIP" ]] || fail "EPUBCheck distribution is missing: $EPUBCHECK_ZIP"

expected_source_sha="$(jq -r '.sourceSha256' "$REFERENCE")"
expected_checker_version="$(jq -r '.conformance.epubCheckVersion' "$EPUB_ZERO_REFERENCE")"
expected_checker_sha="$(jq -r '.conformance.epubCheckDistributionSha256' "$EPUB_ZERO_REFERENCE")"

observed_source_sha="$(sha256sum "$EPUB_FILE" | awk '{print $1}')"
observed_checker_sha="$(sha256sum "$EPUBCHECK_ZIP" | awk '{print $1}')"

[[ "$observed_source_sha" == "$expected_source_sha" ]] || fail "Habermas EPUB SHA-256 differs from the frozen reference."
[[ "$observed_checker_sha" == "$expected_checker_sha" ]] || fail "EPUBCheck distribution SHA-256 differs from the frozen reference."

mkdir -p "$OUT"
rm -f \
  "$NATIVE_REPORT" \
  "$VISUAL_REPORT" \
  "$NATIVE_OPT_IN_REPORT" \
  "$VISUAL_OPT_IN_REPORT"

WORK="$(mktemp -d "$OUT/work.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

unzip -q "$EPUBCHECK_ZIP" -d "$WORK"
EPUBCHECK_DIRECTORY="$WORK/epubcheck-$expected_checker_version"

[[ -f "$EPUBCHECK_DIRECTORY/epubcheck.jar" ]] || fail "Pinned EPUBCheck JAR is missing after extraction."

printf 'DPEngine EPUB-3 visual qualification regression\n'
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
  --report "$NATIVE_REPORT" \
  --visual-report "$VISUAL_REPORT"

dotnet run \
  --project "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  --no-build \
  -- \
  analyze-epub \
  --source "$EPUB_FILE" \
  --epubcheck-distribution "$EPUBCHECK_DIRECTORY" \
  --report "$NATIVE_OPT_IN_REPORT" \
  --visual-report "$VISUAL_OPT_IN_REPORT" \
  --analyze-unresolved-visuals-with-paddle

jq -e \
  --slurpfile expected "$NATIVE_REFERENCE" \
  '. == $expected[0]' \
  "$NATIVE_REPORT" >/dev/null || fail "Native EPUB processing report differs from the EPUB-1 reference."

jq -e \
  --slurpfile expected "$NATIVE_REFERENCE" \
  '. == $expected[0]' \
  "$NATIVE_OPT_IN_REPORT" >/dev/null || fail "Opt-in native EPUB report differs from the EPUB-1 reference."

jq -e \
  --slurpfile expected "$REFERENCE" \
  '. == $expected[0]' \
  "$VISUAL_REPORT" >/dev/null || fail "EPUB visual report differs from the frozen reference."

jq -e \
  --slurpfile expected "$REFERENCE" \
  '. == $expected[0]' \
  "$VISUAL_OPT_IN_REPORT" >/dev/null || fail "Opt-in EPUB visual report differs from the frozen reference."

printf '\nEPUB-3 VISUAL QUALIFICATION REGRESSION: PASS\n'
printf 'Report: %s\n' "$VISUAL_REPORT"
