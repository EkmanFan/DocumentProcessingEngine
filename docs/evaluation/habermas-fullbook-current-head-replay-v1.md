# Habermas full-book current-HEAD replay V1

## Status

**PASS — strict traversal and retained-result comparison**

This replay validates the complete 170-page Habermas PDF on commit
`dc5e8848114be8fed23e53cb8cff0ee47de6c3cc`, after the Host/Engine architecture
refactoring and the local corpus consolidation.

It complements rather than replaces the historical
`habermas-fullbook-strict-traversal-reference-v1` evidence frozen on
`f456e59cf527349736f459bf4c16b20b9efd7111`.

## Execution contract

- public `DocumentProcessingHost.ProcessDocumentAsync` path;
- current `DocumentProcessingResult` consumer contract;
- live PP-StructureV3 and PaddleOCR;
- both model containers resident concurrently;
- exact pinned 170-page source;
- no diagnostic source patch;
- no semantic bypass;
- fail-closed processing unchanged.

## Current result

| Metric | Current HEAD | Historical reference |
|---|---:|---:|
| Physical pages | 170 | 170 |
| Native-only pages | 159 | 159 |
| OCR-backed planned pages | 11 | 11 |
| PP requests | 28 | 28 |
| OCR requests | 17 | 17 |
| OCR-executed pages | 4 | 4 |
| Figure OCR | 0 | 0 |
| Preserved visuals | 22 | 22 |
| Result elements | 1,217 | 1,217 |

The current run completed without interruption. Both containers reported
`OOMKilled=false`.

## Visual comparison

The current run preserved the same 22 page/observation identifiers as the
historical run.

For every visual:

- dimensions are identical;
- decoded RGB pixels are identical;
- ImageMagick absolute pixel difference is `0`.

The current PNG files total `24,265,773` encoded bytes rather than the historical
`23,984,612`. Their file hashes therefore differ. This is an encoding-level
difference, not a visual-content difference: the current host uses
`pdftoppm 26.05.0`, and all 22 decoded pixel comparisons are exact.

## Measurement clarification

The adapted Host harness initially reported `26` as `layoutCalls`. That value is
the number of physical pages retaining layout evidence in the portable result,
not the number of HTTP calls. The captured PP service log contains exactly 28
successful `POST /layout-parsing` requests, matching the historical reference.
The Paddle log contains exactly 17 successful `POST /ocr` requests.

Service logs are authoritative for request counts in this replay.

## Claim boundary

This replay establishes that the current public Host:

- traverses all 170 pages without a fail-closed interruption;
- retains the same route accounting and result-element count;
- makes the same number of PP and OCR requests;
- never sends a Figure to OCR;
- preserves the same 22 visual regions with pixel-exact content.

The historical run did not retain a complete serialized result or a global hash
of all 1,217 elements. Exact whole-book equality of every textual element cannot
therefore be reconstructed retrospectively. Permanent page-level text,
provenance, layout and OCR regressions remain the detailed correctness oracles.

Timing and memory values are observations only. The current public-Host replay
took `239.874 s` with a `626.8 MiB` peak working set; this one-shot comparison is
not a performance regression gate.

The known p28 `figure_title` classification weakness remains outside this
strict-traversal acceptance, as in the historical reference.

## Evidence

Machine-readable evidence is retained in
`habermas-fullbook-current-head-replay-v1.json`. Copyrighted PDF and visual bytes
remain local under `scripts/tmp/habermas-fullbook-current-head-dc5e884/`.
