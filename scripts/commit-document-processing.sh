#!/usr/bin/env bash
set -Eeuo pipefail

REPO="${HOME}/RiderProjects/DocumentProcessingEngine"
SOLUTION="${REPO}/DocumentProcessingEngine.sln"

usage() {
  cat <<'EOF'
Usage:
  ./scripts/commit-document-processing.sh "commit message"
  ./scripts/commit-document-processing.sh --push "commit message"

Behavior:
  1. Verifies the repository and solution.
  2. Runs whitespace validation.
  3. Builds the solution.
  4. Runs all tests.
  5. Stages all changes.
  6. Re-runs staged whitespace validation.
  7. Creates the commit.
  8. Pushes only when --push is specified.
EOF
}

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

PUSH=false

if [[ "${1:-}" == "--push" ]]; then
  PUSH=true
  shift
fi

COMMIT_MESSAGE="${1:-}"

if [[ -z "${COMMIT_MESSAGE}" ]]; then
  usage
  exit 2
fi

command -v git >/dev/null 2>&1 || fail "git was not found."
command -v dotnet >/dev/null 2>&1 || fail "dotnet CLI was not found."

[[ -d "${REPO}/.git" ]] || fail "Git repository not found: ${REPO}"
[[ -f "${SOLUTION}" ]] || fail "Solution not found: ${SOLUTION}"

cd "${REPO}"

printf '\n== Document Processing Engine commit helper ==\n'

printf '\n1/7 Repository status before validation...\n'
git status --short

if [[ -z "$(git status --porcelain)" ]]; then
  fail "There are no changes to commit."
fi

printf '\n2/7 Checking working-tree whitespace...\n'
git diff --check

printf '\n3/7 Building solution...\n'
dotnet build "${SOLUTION}"

printf '\n4/7 Running tests...\n'
dotnet test "${SOLUTION}" --no-build

printf '\n5/7 Staging changes...\n'
git add -A

printf '\nStaged changes:\n'
git status --short

if git diff --cached --quiet; then
  fail "There are no staged changes to commit."
fi

printf '\n6/7 Checking staged whitespace...\n'
git diff --cached --check

printf '\nStaged diff summary:\n'
git diff --cached --stat

printf '\n7/7 Creating commit...\n'
git commit -m "${COMMIT_MESSAGE}"

if [[ "${PUSH}" == true ]]; then
  printf '\nPushing current branch...\n'
  git push
fi

printf '\nDone.\n'
git log -1 --oneline
