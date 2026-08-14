# Targeted OCR live integration V1

## Status

**PASS — Phase B / 15B completed on 2026-08-14.**

This evaluation proves the committed production layout client, deterministic
targeted OCR planner, crop geometry, and PaddleOCR serving client can execute
the pinned Ehrman mixed-content page through real self-hosted services while
excluding the papyrus Figure from OCR.

It is an integration proof. It is **not** a general OCR-quality, corpus-wide
accuracy, performance-SLA, hybrid-reconciliation, or end-to-end ingestion claim.

## Reproducible baseline

```text
Repository baseline:
bb47f1dc67059d9b032d66395bac4763ae855f56

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

Serving runtime:
PaddlePaddle 3.2.2 CPU
PaddleOCR 3.7.0
PP-StructureV3 basic serving
PaddleOCR General OCR basic serving
```

## Live execution path

```text
Ehrman page 233
      -> real PP-StructureV3
      -> PpStructureV3ServingClient
      -> 10 neutral LayoutObservations
      -> TargetedOcrPlanner
      -> 7 authorized raster crops
      -> real PaddleOCR /ocr
      -> PaddleOcrServingClient
      -> OcrRegionResult / OcrTextObservation
```

## Targeting result

The live layout produced 10 neutral observations.

Exactly these source sequences were authorized for OCR:

```text
2, 3, 5, 6, 7, 8, 9
```

The following were deliberately excluded:

```text
0, 1 -> Unknown -> Deferred
4    -> Figure  -> PreserveVisualWithoutOcr
```

Therefore the papyrus Figure produced:

```text
crop files:   0
OCR requests: 0
OCR evidence: 0
```

This is the critical negative-control result for Phase 15.

## OCR execution result

- Layout backend: `pp-structurev3`
- Layout observations: **10**
- OCR backend: `paddleocr-general-ocr`
- OCR profile: `paddleocr-3.7.0-ppocrv6-medium-cpu-v1`
- OCR targets: **7**
- Real OCR HTTP requests: **7**

| Source seq. | Kind | OCR observations | Mean confidence | Request ms |
|---:|---|---:|---:|---:|
| 2 | `Heading` | 3 | 0.999 | 201.8 |
| 3 | `Text` | 7 | 0.995 | 315.7 |
| 5 | `Caption` | 6 | 0.995 | 274.0 |
| 6 | `Text` | 12 | 0.995 | 492.8 |
| 7 | `Text` | 16 | 0.997 | 677.9 |
| 8 | `Text` | 16 | 0.996 | 610.5 |
| 9 | `Text` | 5 | 0.998 | 258.1 |

All seven authorized regions produced OCR evidence. The curated modern-text
sentinels for the heading, left body, caption, and right opening were recovered.

The confidence values above are recorded as backend evidence only. No
application threshold is inferred from this run.

## Acceptance gates

- `layoutBackendIsPpStructureV3`: **true**
- `pageIdentityIsPinnedEhrman233`: **true**
- `targetPlanIsExactlyExpected`: **true**
- `figureSequence4ExcludedFromPlan`: **true**
- `unknownSequences0And1ExcludedFromPlan`: **true**
- `exactlySevenOcrHttpRequestsIssued`: **true**
- `everyTargetProducedOcrEvidence`: **true**
- `noOcrEvidenceAssociatedWithFigureSequence4`: **true**
- `headingSentinelRecovered`: **true**
- `leftBodyImagineRecovered`: **true**
- `captionSentinelsRecovered`: **true**
- `rightOpeningForExampleRecovered`: **true**
- `remainingRightColumnRegionsProducedEvidence`: **true**
- `allEvidenceRetainsPage233`: **true**

All gates passed.

## Evidence-retention decision

The repository records derived integration evidence only.

The raw OCR text and raster crops remain local evaluation artifacts under
`scripts/tmp/phase15b-live-targeted-ocr/` and are intentionally not committed.

This keeps production evidence reproducible without turning the repository into
a copy of source-document text or binary raster artifacts.

## Architectural conclusion

Phase B / 15 targeted OCR is now evidenced end to end for the representative
mixed-content page:

```text
Text / Heading / Caption -> targeted OCR
Figure                   -> NO OCR
Unknown                  -> Deferred
```

Phase B / 15 is therefore **DONE**.

## Next step

Phase B / 16 — **Figure preservation**.

The next increment should preserve a visual asset with enough neutral evidence
to prove source document, physical page, normalized bbox, raster/crop dimensions,
content hash, and processing profile, without yet introducing ApologiaStudio
semantics or native/OCR reconciliation.
