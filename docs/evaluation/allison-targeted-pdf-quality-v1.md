# Allison targeted PDF quality validation V1

## Status

**PASS — machine evidence and human sign-off complete**

This validation deliberately samples four qualified physical pages from the
689-page Allison PDF. It is not a full-book traversal or an exhaustive editorial
comparison.

## Sources

| Source | SHA-256 |
|---|---|
| Allison PDF | `c1e7abb683540db65dd9a4d494fdd8afbe234737eef1aaf251441caa1987a5e7` |
| Allison EPUB | `7de262376d385a569ca77209553c86ae8483ea7497102d165c6e79b3e7cebd37` |

The EPUB is a control edition, not an assumption of perfect editorial equality.
Its corresponding XHTML and image resources were inspected only around the
selected PDF windows.

## Classification

Allison is **hybrid simple** for the current product contract:

- the selected narrative text has a complete native text layer;
- no selected page has a raw multi-column candidate, interleaved column, or
  vertical reading-order reversal;
- localized diagrams and image-backed glyphs need layout evidence and may
  benefit from targeted OCR;
- no selected observation requires whole-page OCR or a complex competing-text
  reconciliation.

This classification is intentionally narrower than a claim about every page in
the book.

## Qualified windows

| PDF page | Risk selected before review | Native evidence | Live PP-StructureV3 | EPUB control | Disposition |
|---:|---|---|---|---|---|
| 24 | Chapter boundary and heading hierarchy | 418 words, 8 blocks, stable order | 3 headings and 4 text regions | Chapter 2 in `c7S.xhtml` | PASS |
| 333 | Large mid-book diagram | 387 words, 8 blocks; diagram text absent from native flow | diagram classified `table` between its title and following paragraph | same 411×296 diagram in `c3PA.xhtml` | PASS |
| 380 | Semi-blind image-bearing holdout | 581 words, 9 blocks; narrative remains native | comparison content read as one `text` region | same 452×99 image in `c4AF.xhtml` | PASS, semantic table structure not claimed |
| 524 | Chapter boundary, inline glyph images, small hierarchy | 291 words, 10 blocks, stable order | headings retained; hierarchy classified `table` | matching Chapter 27 structure and image-backed Greek glyphs in `c5X7.xhtml` | PASS |

The PDF image streams are re-encoded relative to the EPUB resources. Dimensions
are identical for the compared images. Decoded RMSE ratios are approximately
`1.05%` (p333), `1.44%` (p380), `1.14%` and `2.11%` (p524 glyphs), supporting
same-content comparison without requiring byte-identical encodings.

## Semi-blind control

Page 380 was selected from the PDF image inventory before its page text, render,
PP labels, or corresponding EPUB XHTML were inspected. The pre-reveal risk was
limited to an unknown image-bearing narrative page. After native and PP evidence
was collected, visual and EPUB reveal confirmed a two-column comparison embedded
between ordinary paragraphs.

The current result is acceptable because the narrative remains native and the
comparison remains recoverable and auditable. No claim is made that V1 preserves
its row/column structure as a portable table.

## Human oracle

Human sign-off was completed on 2026-08-29. The reviewer confirmed that the
visuals are correct on all four pages after inspecting these questions:

1. Are the chapter/title/body boundaries readable on p24 and p524?
2. Is the p333 mutually-exclusive-attributes diagram meaningful and correctly
   located between its title and following paragraph?
3. Is the p380 prophets/apostles comparison meaningful and correctly located?
4. Is any visible narrative text missing, duplicated, or moved across these
   windows?

No missing, duplicated, displaced, or visibly corrupted content was reported.
Any later contradictory observation reopens this ticket as FAIL with the exact
page and defect.

## Scope boundary

- no full Allison processing run was executed;
- no full PDF corpus was executed;
- PP-StructureV3 was called only for the four qualified Allison pages and the
  separate Habermas p28 control;
- PaddleOCR was not required to establish the classification;
- no Engine correction is justified by the observed Allison evidence.
