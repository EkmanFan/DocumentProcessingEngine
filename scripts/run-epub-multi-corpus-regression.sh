#!/usr/bin/env bash
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFERENCE="$REPO/docs/evaluation/epub-multi-corpus-reference-v1.json"
CALVIN_EPUB_FILE="${CALVIN_EPUB_FILE:-$REPO/tests/epub_test/Institution de la Religion Chretienne.epub}"
BAUCKHAM_EPUB_FILE="${BAUCKHAM_EPUB_FILE:-$REPO/tests/epub_test/Jesus and the Eyewitnesses - The Gospels as Eyewitness Testimony - Richard Bauckham.epub}"
EPUBCHECK_ZIP="${EPUBCHECK_ZIP:-$REPO/scripts/tmp/tool-cache/epubcheck-5.3.0.zip}"
OUT="$REPO/scripts/tmp/epub-multi-corpus-regression"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

for command in dotnet jq unzip; do
  command -v "$command" >/dev/null 2>&1 || fail "$command was not found."
done

[[ -f "$REFERENCE" ]] || fail "Multi-corpus EPUB reference is missing: $REFERENCE"
[[ -f "$CALVIN_EPUB_FILE" ]] || fail "Calvin EPUB is missing: $CALVIN_EPUB_FILE"
[[ -f "$BAUCKHAM_EPUB_FILE" ]] || fail "Bauckham EPUB is missing: $BAUCKHAM_EPUB_FILE"
[[ -f "$EPUBCHECK_ZIP" ]] || fail "EPUBCheck distribution is missing: $EPUBCHECK_ZIP"

mkdir -p "$OUT"

WORK="$(mktemp -d "$OUT/work.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

unzip -q "$EPUBCHECK_ZIP" -d "$WORK"
EPUBCHECK_DIRECTORY="$WORK/epubcheck-5.3.0"

[[ -f "$EPUBCHECK_DIRECTORY/epubcheck.jar" ]] || fail "Pinned EPUBCheck JAR is missing after extraction."

dotnet build \
  "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  --warnaserror

write_summary() {
  local native_report="$1"
  local visual_report="$2"
  local summary="$3"

  jq -n \
    --slurpfile native "$native_report" \
    --slurpfile visual "$visual_report" \
    '{
      source: $native[0].source,
      processing: {
        elementCount: $native[0].processing.elementCount,
        textElementCount: $native[0].processing.textElementCount,
        headingElementCount: $native[0].processing.headingElementCount,
        captionElementCount: $native[0].processing.captionElementCount,
        segmentCount: $native[0].processing.segmentCount,
        authoritativeTextSha256: $native[0].processing.authoritativeTextSha256,
        nativeExtractionProfileId: $native[0].processing.nativeExtractionProfileId
      },
      visuals: {
        bodyMatterStartSpineIndex: $visual[0].bodyMatterStartSpineIndex,
        selectedVisualCount: $visual[0].selectedVisualCount,
        visualAssetCount: $visual[0].visualAssetCount,
        assets: ($visual[0].assets | map({
          sourceResourceId,
          qualification,
          contentLength,
          contentSha256
        }))
      }
    }' >"$summary"
}

run_corpus() {
  local corpus_key="$1"
  local source_path="$2"
  local native_report="$OUT/$corpus_key-native.json"
  local visual_report="$OUT/$corpus_key-visual.json"
  local observed_summary="$OUT/$corpus_key-summary.json"
  local opt_in_native_report="$OUT/$corpus_key-opt-in-native.json"
  local opt_in_visual_report="$OUT/$corpus_key-opt-in-visual.json"
  local opt_in_summary="$OUT/$corpus_key-opt-in-summary.json"

  dotnet run \
    --project "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
    -c Release \
    --no-build \
    -- \
    analyze-epub \
    --source "$source_path" \
    --epubcheck-distribution "$EPUBCHECK_DIRECTORY" \
    --report "$native_report" \
    --visual-report "$visual_report"

  dotnet run \
    --project "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
    -c Release \
    --no-build \
    -- \
    analyze-epub \
    --source "$source_path" \
    --epubcheck-distribution "$EPUBCHECK_DIRECTORY" \
    --report "$opt_in_native_report" \
    --visual-report "$opt_in_visual_report" \
    --analyze-unresolved-visuals-with-paddle

  write_summary \
    "$native_report" \
    "$visual_report" \
    "$observed_summary"

  write_summary \
    "$opt_in_native_report" \
    "$opt_in_visual_report" \
    "$opt_in_summary"

  jq -e \
    --arg corpus "$corpus_key" \
    --slurpfile expected "$REFERENCE" \
    '. == $expected[0].corpora[$corpus]' \
    "$observed_summary" >/dev/null || fail "$corpus_key EPUB report differs from the frozen reference."

  jq -e \
    --arg corpus "$corpus_key" \
    --slurpfile expected "$REFERENCE" \
    '. == $expected[0].corpora[$corpus]' \
    "$opt_in_summary" >/dev/null || fail "$corpus_key opt-in EPUB report differs from the frozen reference."
}

printf 'DPEngine EPUB multi-corpus regression\n'
printf 'Reference: %s\n\n' "$REFERENCE"

run_corpus "calvin" "$CALVIN_EPUB_FILE"
run_corpus "bauckham" "$BAUCKHAM_EPUB_FILE"

printf '\nEPUB MULTI-CORPUS REGRESSION: PASS\n'
