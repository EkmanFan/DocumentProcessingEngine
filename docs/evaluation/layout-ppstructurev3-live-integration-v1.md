# PP-StructureV3 live integration validation V1

## Status

**PASS — Phase B / 14B completed on 2026-08-14.**

This evaluation proves that the production .NET serving boundary can call a real
self-hosted PP-StructureV3 service on the pinned Ehrman mixed-content page and
produce neutral layout observations that satisfy the existing spatial oracle and
deterministic treatment policy.

It is an integration proof. It is **not** a general production-quality, throughput,
latency-SLA, OCR-quality, or multi-backend claim.

## Reproducible baseline

```text
Repository baseline:
f6ca5307dc37ae77eed31e0b57288d4adafe1e99

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
endpoint: POST /layout-parsing
```

## Execution result

- Backend: `pp-structurev3`
- Neutral observations: **10**
- One-request observed elapsed time: **8661.8 ms**
- Overall decision: **PASS**

The elapsed time above is recorded only as an observation from this CPU integration
run. No performance target or SLA is inferred from it.

## Representative neutral sequence

| Order | Raw PP label | Neutral kind | Deterministic treatment | IoU |
|---:|---|---|---|---:|
| 2 | `paragraph_title` | `Heading` | `RecognizeText` | 0.820 |
| 3 | `text` | `Text` | `RecognizeText` | 0.860 |
| 4 | `image` | `Figure` | `PreserveVisualWithoutOcr` | 0.923 |
| 5 | `figure_title` | `Caption` | `RecognizeText` | 0.766 |
| 6 | `text` | `Text` | `RecognizeText` | 0.901 |

The critical safety invariant is therefore demonstrated:

```text
papyrus facsimile
    -> Figure
    -> PreserveVisualWithoutOcr
```

The neutral `LayoutObservation` still carries no recognized text/content, so
backend `block_content` cannot leak into document narrative through the layout
boundary.

## Acceptance gates

- `backendIsPpStructureV3`: **true**
- `physicalPageIs233`: **true**
- `observationsProduced`: **true**
- `figureDetectedAsNonNarrative`: **true**
- `noNarrativeTextBlockCenteredInsideFacsimile`: **true**
- `captionSeparated`: **true**
- `sectionTitleSeparated`: **true**
- `leftModernTextDetected`: **true**
- `rightModernTextDetected`: **true**
- `modernTextReadingOrderUsable`: **true**
- `figureCaptionSpatialRelationPlausible`: **true**
- `representativeOrderHeadingTextFigureCaptionText`: **true**
- `facsimileTreatmentPreservesVisualWithoutOcr`: **true**
- `textualTreatmentsRecognizeText`: **true**
- `deterministicPolicyConsistentForAllObservations`: **true**
- `neutralObservationCarriesNoTextOrContent`: **true**

All gates passed.

## Architectural conclusion

The following production path is now evidenced against the real backend:

```text
raster image stream
        -> PpStructureV3ServingClient
        -> real HTTP POST /layout-parsing
        -> PP-StructureV3
        -> prunedResult
        -> PpStructureV3LayoutAdapter
        -> LayoutAnalysisResult
        -> LayoutTreatmentPolicy
```

Phase B / 14B is therefore **DONE**.

## Next step

Phase B / 15 — **Targeted OCR**.

Only regions whose deterministic treatment is `RecognizeText` should be sent to
the OCR recognizer:

```text
Text     -> OCR
Heading  -> OCR
Caption  -> OCR
Figure   -> NO OCR
Table    -> Deferred
Unknown  -> Deferred
```

Figure persistence, native/OCR reconciliation, cross-page hybrid continuity and
end-to-end hybrid regression remain later increments.
