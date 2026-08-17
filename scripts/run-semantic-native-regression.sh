#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GROUND_TRUTH="$REPO/docs/evaluation/semantic-regression-ground-truth-v1.json"
FIXTURES="$REPO/tests/pdf_pages_test"
MANIFEST="$FIXTURES/fixtures-manifest.tsv"
OUT="$REPO/scripts/tmp/semantic-native-regression"
REPORT="$OUT/native-report.json"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 2
}

command -v dotnet >/dev/null 2>&1 ||
  fail "dotnet is required."

[[ -f "$GROUND_TRUTH" ]] ||
  fail "Ground-truth manifest is missing: $GROUND_TRUTH"

[[ -d "$FIXTURES" ]] ||
  fail "Fixture directory is missing: $FIXTURES"

[[ -f "$MANIFEST" ]] ||
  fail "Fixture manifest is missing: $MANIFEST"

mkdir -p "$OUT"
rm -f "$REPORT"

printf 'DPEngine semantic native/provenance regression\n'
printf 'Ground truth: %s\n' "$GROUND_TRUTH"
printf 'Fixtures: %s\n' "$FIXTURES"
printf 'Manifest: %s\n\n' "$MANIFEST"

printf '[1/2] Building EvaluationCli...\n'
dotnet build \
  "$REPO/tools/DocumentProcessing.EvaluationCli/DocumentProcessing.EvaluationCli.csproj" \
  -c Release \
  -warnaserror \
  --nologo

printf '\n[2/2] Running no-ML native/provenance evaluation...\n'
set +e

dotnet \
  "$REPO/tools/DocumentProcessing.EvaluationCli/bin/Release/net10.0/DocumentProcessing.EvaluationCli.dll" \
  evaluate-semantic-native-regression \
  --ground-truth "$GROUND_TRUTH" \
  --fixtures "$FIXTURES" \
  --manifest "$MANIFEST" \
  --report "$REPORT"

evaluation_status=$?

set -e

[[ -f "$REPORT" ]] ||
  fail "Semantic native evaluator produced no report."

printf '\nReport: %s\n' "$REPORT"
printf 'Exit code: %s\n' "$evaluation_status"

exit "$evaluation_status"
