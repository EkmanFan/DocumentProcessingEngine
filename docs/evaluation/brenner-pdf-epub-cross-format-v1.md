# Brenner PDF/EPUB cross-format evaluation V1

## Scope

This evaluation processes the complete PDF and EPUB editions of William H.
Brenner's *Logic and Philosophy*. The local corpus files are intentionally
excluded from Git.

The PDF was produced from the EPUB through Calibre. The comparison therefore
tests whether the two independent format pipelines recover corresponding
content; the EPUB result is not used as processing input for the PDF.

## Source-backed formula rule

The validated product rule is:

- a source PDF image corresponding to a PP-StructureV3 `formula` region is a
  meaningful visual and is preserved without OCR;
- a PP `formula` region without a corresponding source image is discarded as
  visual evidence and native text remains authoritative;
- the source image is the preservation unit, so several PP regions intersecting
  one source image still produce one asset.

Human-reviewed physical-page controls are:

| Page | Expected visual assets | OCR |
|---:|---:|---|
| 25 | 1 | none |
| 47 | 2 | none |
| 65 | 4 | none |
| 241 | 3 | none |

All four public-Host executions pass with these exact counts. On page 65,
`~C & ~D` and `~p & ~q` remain native text and do not create assets.

## Complete PDF result

The complete PDF processing succeeds:

- 1,321 portable elements;
- 1,222 text elements;
- 99 visual assets;
- 73,484 normalized tokens;
- 83 PP-StructureV3 calls;
- 5 targeted OCR calls.

A separate page-isolated sweep covers all 68 Healthy Native pages whose
full-document source plan requests meaningful preservation. Every isolated
execution completes successfully. The sweep is a robustness control, not an
asset-count oracle, because extracting one physical page removes the recurring
and document-wide evidence used by full-document planning.

## Text comparison

The EPUB result contains 72,807 normalized tokens. A deterministic ordered
line-diff aligns 72,634 tokens:

- PDF token coverage: 98.8433%;
- EPUB token coverage: 99.7624%;
- symmetric token overlap: 99.3007%;
- 850 PDF-only aligned deletions and 173 EPUB-only aligned insertions.

The normalized sequences are not byte-identical. Reviewed differences include
PDF table-of-contents duplication, front-matter order, joined/split compounds,
hyphenation, and isolated PDF extraction errors.

## Visual comparison

The EPUB result contains 129 visual assets; the PDF result contains 99.

An order-constrained normalized-image comparison aligns every PDF asset with
one EPUB asset in the same sequence, leaving 30 EPUB assets without a PDF
result counterpart. Manual review of the lowest-scoring aligned pairs confirms
that they depict the same logical formulas or diagrams despite resolution,
margin, and rasterization differences.

The full PDF contains 130 embedded image occurrences:

- 106 are planned as `PreserveMeaningfulVisual`;
- 24 remain `AnalyzeVisual`;
- mixed pages containing both actions currently skip 7 otherwise-preservable
  source images because Healthy Native visual execution requires every visual
  action on the page to be resolved.

The 30-result visual gap is therefore established and remains unresolved. This
increment does not broaden visual qualification beyond the approved
source-image/formula rule.

## Performance

One complete PDF-then-EPUB run measured:

- elapsed time: 391.63 seconds;
- Engine maximum resident memory: 505,076 KiB (about 493 MiB);
- PP-StructureV3 container peak: 9,908,191,232 bytes (about 9.3 GiB);
- PaddleOCR container peak: 3,558,096,896 bytes (about 3.4 GiB).

The 68-page isolated sweep measured 382.25 seconds cumulatively and a maximum
Engine resident memory of 131,992 KiB (about 129 MiB). PP-StructureV3 peaked at
approximately 9.0 GiB during that sweep.

## Outcome

Established:

- the source-image/formula rule produces the four human-approved page results;
- the complete PDF and EPUB text sequences have 99.3007% symmetric ordered
  token overlap;
- every emitted PDF visual has an ordered EPUB counterpart.

Unresolved:

- the PDF result emits 30 fewer meaningful visuals than the EPUB result;
- qualification of the remaining `AnalyzeVisual` source images and execution
  of mixed visual-action pages require a separate, explicit increment.
