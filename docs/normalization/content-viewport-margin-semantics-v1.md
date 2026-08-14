# Content viewport margin semantics v1

## Status

Regression repair discovered while closing Phase 18E.

## Problem

Phase 17C correctly changed PdfPig evidence from its effective CropBox-local
coordinates into the canonical MediaBox coordinate space used by the selected
`pdftoppm` raster profile.

Recurring header/footer detection still interpreted its top/bottom zones as if
canonical MediaBox bounds were relative to the effective source viewport.

The diagnosis compared exact commits `0ee43d1` and `9506f24` against the same
pinned 617-page Ehrman PDF and matched all 531 historical recurring-header
blocks by page, source sequence and text hash.

```text
pre-17C Top min / median / max
0.020457 / 0.034873 / 0.084563

current MediaBox Top min / median / max
0.200125 / 0.211261 / 0.251278

median shift
+0.176461
```

The blocks did not disappear. Their canonical position moved because MediaBox
contains area above the effective CropBox.

That caused:

```text
recurring headers excluded: 531 -> 0
Ehrman segments:            267 -> 300
cross-page segments:        166 -> 193
```

## Correct model

Words, native blocks, layout observations and hybrid element bounds remain in
canonical MediaBox coordinates. This is the correct space for native/OCR
comparison, layout, visual crops and reconciliation.

`DocumentExtractionPage.ContentViewport` separately records the effective source
viewport inside canonical page coordinates. For PDF this is the CropBox expressed
in normalized MediaBox display coordinates. Other producers default to the full
page.

`HybridDocumentPage` carries the same page-level viewport evidence.

## Margin rule

The deterministic policy remains:

```text
header zone: first 12% of effective content viewport
footer zone: last 12% of effective content viewport
maximum candidate height: 20% of effective content viewport
```

Canonical evidence is not rewritten.

## Why not increase the global threshold

The diagnosis showed:

```text
<= 0.20:   0 / 531
<= 0.22: 490 / 531
<= 0.25: 527 / 531
```

A global 0.22-0.26 threshold would encode one document's CropBox offset into a
generic rule. The missing evidence is the effective content viewport.

## Raster diagnostic refresh

The Ehrman source still has exactly 286 pages without native words. Under the
MediaBox page-area denominator, the diagnostic "textless page whose largest
raster covers at least 60% of canonical page area" is 285 rather than the
pre-17C value 286.

Physical pages 14-20 are raster-only front-matter/table-of-contents pages. They
remain 7/7 natively textless and dominant-raster. They are OCR/layout/structure
regression cases, not representative narrative-body segmentation cases.

## Acceptance

The repair is accepted only if Ehrman native parity remains exact, recurring
headers return to 531, segmentation returns to 267 segments / 166 cross-page,
De Decretis remains unchanged, legacy and hybrid normalization share the same
content-viewport-relative geometry, canonical MediaBox evidence remains
unchanged, and all tests pass.

## Next

This repair does not close Phase 18E. The broader real hybrid corpus validation
remains next, including a real hybrid path over pages 1-10 and suitable narrative
cross-page cases.
