# Source-backed multi-visual preservation V1

## Purpose

Healthy native PDF pages may contain several embedded images whose source
evidence already requests meaningful preservation. PP-StructureV3 may classify
one image as one region, split it into several regions, omit it, or label native
text as `formula`.

The Engine must preserve the source images selected by planning without
allowing PP-only regions to change their number.

## Retained evidence

The authoritative visual-planning pass already measures, in normalized page
coordinates:

- each source visual's declared placement;
- its effective foreground bounds when foreground analysis succeeds;
- the stable source visual index used by evidence and execution plans.

These exact raster observations now remain attached to the authoritative
planning result and reach Healthy native visual execution. The execution plan
continues to contain decisions only; source geometry remains separate evidence.

## Preservation rule

For every source visual planned as `PreserveMeaningfulVisual`:

1. use effective foreground bounds when present, otherwise declared bounds;
2. create exactly one source-backed Figure with those bounds;
3. insert that Figure into page order using unambiguous vertical geometry
   without changing the relative order of PP observations;
4. preserve exactly that source-backed Figure.

PP Figure regions may help describe the page, but they are not preservation
units. Several PP regions intersecting one source image still produce one
asset. A PP Figure with no planned source visual produces no additional asset.
Missing source evidence or ambiguous insertion geometry continues to fail
closed.

When layout text orders appear to straddle a preserved visual, a whole native
block may use its own geometry only if that geometry places the complete block
unambiguously before, between, or after all preserved visuals. Overlap still
fails closed and native blocks are never split.

## Formula regions

PP-StructureV3 `formula` is mapped to the neutral Core `Figure` role while the
backend label remains in `RawLabel`. That label means that rendered pixels look
like a formula; it does not establish that the PDF contains an image.

The product rule is:

- source image corresponding to PP `formula`: preserve the source image without
  OCR;
- PP `formula` without a corresponding source image: discard the region as
  visual evidence and retain native text.

The source image, not the PP region, remains the unit written through
`UserVisualAssetWriter`.

## Validation

Deterministic tests cover:

- one source Figure per planned source visual;
- several PP `formula` and `image` fragments intersecting one source image;
- a PP-only `formula` that creates no additional asset;
- missing planned source evidence, which remains rejected;
- geometry fallback only when a complete native block has one unambiguous
  visual band.

Live public-Host validation covers these Brenner physical pages:

| Page | Source images preserved | Relevant PP behavior |
|---:|---:|---|
| 17 | 3 | three `formula` regions |
| 20 | 2 | no Figure; PP grouped the area as `footnote` |
| 25 | 1 | one source image fragmented into `formula`, `image`, caption, and `formula` |
| 47 | 2 | PP also emitted an unrelated `formula` over native text |
| 65 | 4 | PP emitted six `formula` regions; two were native text |
| 241 | 3 | all three source images were labelled as text |

The resulting asset counts are respectively 3, 2, 1, 2, 4, and 3. No visual OCR
is required. On page 65, the native strings `~C & ~D` and `~p & ~q` remain in
the textual result and do not create visual assets.
