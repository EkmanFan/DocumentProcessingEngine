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
  one source image still produce one asset;
- a source image confidently overlapping a PP `formula` or `table` region is
  reported as meaningful;
- a source image that cannot be qualified confidently is still preserved and
  reported as unqualified;
- a full-page first-page image associated with the document title is treated as
  publication presentation and excluded.

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

- 1,351 portable elements;
- 1,222 text elements;
- 129 visual assets;
- 73,484 normalized tokens;
- 84 PP-StructureV3 calls;
- 5 targeted OCR calls.

The authoritative, normalized, and tokenized PDF text artifacts are
byte-identical to the previous complete-run reference. The visual-policy
increment therefore changes visual output without changing recovered text.

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

## Human-reviewed unresolved source visuals

The PDF contains 130 embedded image occurrences. Before this increment, 106
were already planned as meaningful and 24 required visual analysis.

The 24 reviewed cases now resolve as follows:

- 1 first-page cover is excluded;
- 5 source images receive strong `formula` or `table` evidence and are reported
  as meaningful;
- 18 source images are preserved and explicitly reported as unqualified.

The complete PDF result therefore contains 111 meaningful and 18 unqualified
visual assets. This includes the optional portraits: conservative preservation
keeps them available to the user without claiming that they are meaningful.

Mixed pages no longer suppress already-qualified source images merely because
another source image on the same page remains unresolved. Unqualified source
images are appended after the existing layout observations when inserting them
geometrically would split an indivisible native text block. This keeps native
text order unchanged while preserving the visual.

## Visual comparison

The EPUB and PDF results both contain 129 visual assets.

An order-constrained normalized-image comparison aligns all 129 PDF assets with
the 129 EPUB assets in the same sequence. Manual review covers the 18 pairs with
the largest normalized pixel differences. Every reviewed pair depicts the same
formula, table, diagram, portrait, or supporting illustration despite
resolution, margin, JPEG, and rasterization differences.

The previous 30-asset result gap is closed without treating every retained
source image as meaningful.

## Performance

One complete PDF-then-EPUB run measured:

- elapsed time: 522.98 seconds;
- Engine maximum resident memory: 607,344 KiB (about 593 MiB);
- PP-StructureV3 container peak: 9,691,389,952 bytes (about 9.03 GiB);
- PaddleOCR container peak: 3,348,385,792 bytes (about 3.12 GiB).

These are single-pass observations, not a benchmark. Compared with the earlier
run, elapsed time increased while PP and OCR peak memory remained in the same
operational range. The run includes one additional PP request and preservation
of 30 additional PDF assets.

The 68-page isolated sweep measured 382.25 seconds cumulatively and a maximum
Engine resident memory of 131,992 KiB (about 129 MiB). PP-StructureV3 peaked at
approximately 9.0 GiB during that sweep.

## Outcome

Established:

- the source-image/formula rule produces the four human-approved page results;
- the complete PDF and EPUB text sequences have 99.3007% symmetric ordered
  token overlap;
- the PDF text result is exactly unchanged by the visual-policy increment;
- all 129 PDF visuals have an ordered EPUB counterpart;
- uncertain source visuals remain available with an explicit unqualified
  status, while the cover is excluded.

Unresolved:

- visual qualification remains deliberately best-effort; an unqualified asset
  requires the user to make the final content decision;
- the performance figures require repeated runs before they can support a
  stable performance claim.
