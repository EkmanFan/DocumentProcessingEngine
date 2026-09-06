#!/usr/bin/env bash
set -Eeuo pipefail

# Characterizes native descriptive metadata across the qualified local corpus.
# Evaluation only: no source is modified and no processing route runs.

REPO="${HOME}/RiderProjects/DocumentProcessingEngine"
CORPUS="${REPO}/tests/document_corpus"
REPORT="${REPO}/docs/evaluation/document-native-metadata-characterization-v1.md"
JSON="${REPO}/docs/evaluation/document-native-metadata-characterization-v1.json"

dotnet run \
  --project "${REPO}/tools/DocumentProcessing.EvaluationCli" \
  -c Release \
  -- characterize-native-metadata \
  --corpus "${CORPUS}" \
  --report "${REPORT}" \
  --json "${JSON}"

echo "Report: ${REPORT}"
echo "JSON:   ${JSON}"
