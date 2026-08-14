# Figure preservation live integration V1

## Status

**PASS — Phase B / 16B completed on 2026-08-14.**

This evaluation proves that the committed production layout client,
deterministic visual-preservation planner, shared raster crop geometry, and
`VisualAssetPreserver` can preserve the pinned Ehrman page 233 Figure #4 as
reproducible PNG bytes with neutral provenance and integrity evidence while
the same Figure remains excluded from targeted OCR.

It is an integration proof. It is **not** a general visual-semantic,
storage-backend, performance-SLA, native/OCR-reconciliation, or end-to-end
ingestion claim.

## Reproducible baseline

```text
Repository baseline:
b99c429fde1d8f26e069137183f6515a80b02ab4

Source:
Bart D. Ehrman, The New Testament, 8th edition
physical PDF page: 233
source SHA-256:
f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe

Raster:
300 DPI
2556 x 3305
SHA-256:
654dd8186552c2727808c48b2e4376815693e1d845f489a66dbca8305e61d484

Serving/runtime profile:
PaddlePaddle 3.2.2 CPU
PaddleOCR 3.7.0 / PP-StructureV3
pdftoppm-26.01.0-300dpi-rgb-png-pillow-10.1.0-crop-v1
```

## Live execution path

```text
Ehrman page 233
      -> real PP-StructureV3
      -> PpStructureV3ServingClient
      -> 10 neutral LayoutObservations
      -> VisualPreservationPlanner
      -> exactly Figure #4
      -> RasterCropGeometry
      -> exact papyrus PNG crop
      -> VisualAssetPreserver
      -> preserved PNG bytes
      + PreservedVisualEvidence
```

## Figure target

- Source sequence: **4**
- Kind: `Figure`
- Raw PP label: `image`
- Treatment: `PreserveVisualWithoutOcr`

Normalized bounds:

```text
left   = 0.24256651017214398
top    = 0.43630862329803327
right  = 0.5715962441314554
bottom = 0.859304084720121
```

Deterministic pixel crop:

```text
left   = 620
top    = 1442
right  = 1461
bottom = 2840
width  = 841
height = 1398
```

## Preserved content

- Media type: `image/png`
- Preserved bytes: **1534766**
- SHA-256: `aaf62775d525104c737d2d238df1840894b42572e52de3874705fa10c926009d`
- Independent crop materializations: **2**
- Independent preservation runs: **2**

Two independent PNG crops produced byte-identical content. Each crop was then
copied through the production `VisualAssetPreserver`, and the preserved outputs
retained the same content length and SHA-256.

The content hash identifies bytes produced by the pinned raster/crop/encoding
profile. It is not claimed to be an encoding-independent identity of the visual.

## OCR negative control

The same live `LayoutAnalysisResult` was evaluated by the targeted OCR planner.

- Figure #4 included in targeted OCR: **false**
- OCR service started by this validation: **false**
- OCR requests issued by this validation: **0**

Therefore the visual path and OCR path remain mutually consistent:

```text
Figure -> PreserveVisualWithoutOcr -> preserve visual
Figure -> targeted OCR              -> excluded
```

## Acceptance gates

- `layoutBackendIsPpStructureV3`: **true**
- `pageIdentityIsPinnedEhrman233`: **true**
- `tenNeutralLayoutObservationsProduced`: **true**
- `exactlyOneVisualTargetProduced`: **true**
- `visualTargetIsFigureSequence4`: **true**
- `visualTreatmentIsPreserveWithoutOcr`: **true**
- `figureBoundsMatchCommittedLiveEvidence`: **true**
- `deterministicPixelCropMatchesBaseline`: **true**
- `figureExcludedFromTargetedOcrPlan`: **true**
- `preservedContentIsNonEmpty`: **true**
- `independentCropEncodingIsByteReproducible`: **true**
- `visualAssetPreserverCopiedExactBytes`: **true**
- `preservedSha256IsReproducible`: **true**
- `sourceDocumentIdentityRetained`: **true**
- `sourcePageAndLayoutSequenceRetained`: **true**
- `normalizedBoundsRetained`: **true**
- `processingProfileRetained`: **true**
- `mediaTypeRetained`: **true**

All gates passed.

## Evidence-retention decision

The repository records derived integration evidence only.

The source crop PNGs and preserved PNGs remain local under
`scripts/tmp/phase16b-live-figure-preservation/` and are intentionally not
committed.

This preserves auditability without storing copyrighted/binary source-derived
visual assets in the repository.

## Architectural conclusion

Phase B / 16 figure preservation is now evidenced end to end for the
representative mixed-content page.

Phase B / 16 is therefore **DONE**.

## Next step

Phase B / 17 — **Native/OCR reconciliation**.

The next increment should unify native text evidence and OCR text evidence into
one neutral page/document evidence stream while retaining origin/provenance and
without yet forcing semantic segmentation or cross-page hybrid regression into
the same change.
