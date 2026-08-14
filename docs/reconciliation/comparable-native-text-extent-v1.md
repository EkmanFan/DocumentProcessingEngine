# Comparable native text extent V1

## Status

Phase B / 17C production-boundary increment.

```text
17 Native/OCR reconciliation
   17A deterministic reconciliation boundary      DONE
   17B real evidence + human diagnosis            DONE
   17C comparable native text extent              THIS INCREMENT
   17D deterministic dehyphenation                NEXT
   17E real reconciliation regression             TODO
```

## Evidence that triggered 17C

Phase 17B established three different cases on the pinned Ehrman corpus:

- physical page 233: native text missing; OCR recovery is appropriate;
- physical page 405: the OCR region contains the first portion of a larger
  healthy native block, not contradictory text;
- physical page 380: OCR and native text substantially agree over a common
  portion, while each source contains a small number of character-level errors
  and the native block extends beyond the OCR region.

Human review therefore established that a positive block/region intersection is
not enough to prove equivalent textual extent.

## Coordinate-space defect found during diagnosis

A deeper upstream cause was found before changing reconciliation policy.

PdfPig 0.1.15 exposes `Page.Width` and `Page.Height` from the effective CropBox
visible bounds. Its page content coordinates are translated so the CropBox
origin becomes `(0,0)`.

The current DPE OCR/layout benchmark rasterizer invokes `pdftoppm` without
`-cropbox`, so the generated raster uses the MediaBox viewport.

Before 17C, `PdfPigDocumentExtractor` normalized crop-relative word/block
coordinates by `Page.Width`/`Page.Height`, while PP-StructureV3 observations were
normalized against a MediaBox raster. The rectangles could therefore overlap
substantially while still representing different physical positions.

17C keeps the existing raster evidence profile and maps PdfPig native evidence
into the same displayed MediaBox coordinate space before normalization.

The transform supports rotations 0, 90, 180, and 270 degrees.

PdfPig word/glyph rectangles on rotated pages may remain oriented rectangles.
Before converting them into DPE's axis-aligned `NormalizedRectangle`, 17C uses
PdfPig's geometry `Normalise()` operation to compute the axis-aligned envelope
of all four rectangle corners. This is geometry normalization only; it does not
alter text or reading order.

## Comparable native extent

`NativeTextExtentProjector` receives:

```text
DocumentTextBlock
LayoutObservation
```

It does **not** receive OCR text.

For OCR-authorized layout kinds only, it:

1. requires positive block/layout intersection;
2. finds the first and last native words having positive spatial intersection
   with the layout region;
3. retains the complete contiguous source-block word span between those
   boundaries;
4. preserves `DocumentTextBlock.Words` order;
5. returns `ComparableNativeTextExtent`.

This deliberately avoids:

- sorting by global `DocumentWord.SourceSequence`;
- fuzzy matching;
- edit-distance thresholds;
- OCR-confidence thresholds;
- LLM arbitration;
- dehyphenation;
- reconciliation/authority decisions.

The contiguous-span rule is intentional: minor geometry differences may cause a
middle word not to intersect a region even though it is textually between two
words that do. Punching holes in the native reading-order span would create a
new artifact.

## Provenance

`ComparableNativeTextExtent` retains:

- the original `DocumentTextBlock`;
- the source `LayoutObservation`;
- first and last source-block word indexes;
- count of words that intersected spatially;
- the complete contiguous native word span;
- union bounds of the selected words;
- raw word-joined text.

It is evidence projection, not final document text.

## Phase 17C acceptance

The live Phase 17C validation reuses the real Phase 17B PP-StructureV3 plans and
OCR evidence. It does not call Paddle services again.

For pages 405 and 380 it verifies that:

- native page dimensions align with the already-rendered 300-DPI MediaBox
  raster;
- a comparable native extent is produced;
- the extent is closer in length to OCR than the whole native block;
- conservative diagnostic edit similarity improves relative to the Phase 17B
  whole-block comparison.

These evaluation comparisons do not change production reconciliation policy.

## Deliberate non-decisions

17C does not:

- modify `NativeOcrTextReconciler`;
- define an Agreement similarity threshold;
- correct native/OCR character errors;
- merge the two evidence sources;
- dehyphenate line-break artifacts;
- implement automatic cross-block matching;
- implement cross-page continuity.

## Next

Phase 17D should address only deterministic dehyphenation, using the now
comparable extents. Phase 17E will then rerun real reconciliation before any
decision about additional divergence states or fuzzy comparison.
