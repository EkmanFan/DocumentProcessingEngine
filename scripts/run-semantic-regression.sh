#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LAYOUT_MODE=""

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 2
}

usage() {
  cat <<'TXT'
Usage:
  scripts/run-semantic-regression.sh --layout-mode baseline
  scripts/run-semantic-regression.sh --layout-mode all-pass

baseline:
  Reproduce the Phase-15.1 current-baseline classifications, including the
  known red meaningful-visual controls. Use before semantic remediation.

all-pass:
  Require every live layout semantic control to satisfy ground truth.
  Intended after remediation.
TXT
}

while (($# > 0)); do
  case "$1" in
    --layout-mode)
      (($# >= 2)) ||
        fail "--layout-mode requires a value."

      LAYOUT_MODE="$2"
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

case "$LAYOUT_MODE" in
  baseline|all-pass)
    ;;

  *)
    fail "--layout-mode must be 'baseline' or 'all-pass'."
    ;;
esac

NATIVE_RUNNER="$REPO/scripts/run-semantic-native-regression.sh"
LAYOUT_RUNNER="$REPO/scripts/run-semantic-layout-regression.sh"
OCR_RUNNER="$REPO/scripts/run-semantic-ocr-regression.sh"

for runner in \
  "$NATIVE_RUNNER" \
  "$LAYOUT_RUNNER" \
  "$OCR_RUNNER"; do
  [[ -x "$runner" ]] ||
    fail "Required semantic regression runner is missing or not executable: $runner"
done

printf 'DPEngine full semantic regression suite\n'
printf 'Layout mode: %s\n\n' "$LAYOUT_MODE"

printf '[1/3] Native/provenance regression...\n'
"$NATIVE_RUNNER"

printf '\n[2/3] Live layout semantic regression...\n'
"$LAYOUT_RUNNER" \
  --mode "$LAYOUT_MODE"

printf '\n[3/3] Real PP + PaddleOCR semantic regression...\n'
"$OCR_RUNNER"

printf '\nFULL SEMANTIC REGRESSION SUITE: PASS\n'
printf '  native/provenance: PASS\n'
printf '  layout mode:       %s PASS\n' "$LAYOUT_MODE"
printf '  real OCR:          PASS\n'
