# Targeted OCR production boundary V1

## Status

Phase B / 15 targeted OCR is complete.

The production boundary establishes the neutral OCR evidence model,
deterministic OCR target planning, crop geometry, and the concrete PaddleOCR
General OCR serving client. Phase 15B then validated that boundary against real
self-hosted PP-StructureV3 and PaddleOCR services on the pinned Ehrman
mixed-content page.

```text
15 Targeted OCR
   15A neutral model + planner + serving client  DONE
   15B real targeted OCR integration             DONE

16 Figure preservation                           NEXT
```

## Architectural rule

OCR is permitted only after neutral layout evidence has been translated by
deterministic application policy.

```text
LayoutObservation
        ↓
LayoutTreatmentPolicy
        ↓
RecognizeText
        ↓
TargetedOcrPlanner
        ↓
RasterCropRectangle
        ↓
PaddleOcrServingClient
        ↓
OcrRegionResult
```

The selected V1 policy remains:

```text
Text     -> RecognizeText
Heading  -> RecognizeText
Caption  -> RecognizeText
Figure   -> PreserveVisualWithoutOcr
Table    -> Deferred
Unknown  -> Deferred
```

`PaddleOcrServingClient` independently re-checks this policy and refuses a
non-`RecognizeText` source region before issuing HTTP. This is deliberate
defense in depth: a caller cannot accidentally OCR a Figure merely by invoking
the OCR client directly.

## Neutral OCR evidence

The new Core model distinguishes OCR evidence from final document text.

`OcrRegionResult` records:

- OCR backend ID;
- versioned processing profile ID supplied by application configuration;
- the exact source `LayoutObservation`;
- zero or more `OcrTextObservation` items.

Each `OcrTextObservation` records:

- physical page number;
- source layout observation sequence;
- OCR-local observation sequence;
- recognized text;
- recognition confidence;
- bounds normalized to the full source page.

This evidence is not automatically authoritative text.

Native/OCR reconciliation remains Phase 17.

## Crop geometry

`NormalizedRectangle` remains unclamped evidence.

`RasterCropRectangle.FromNormalized(...)` converts that evidence into the only
pixel rectangle that can be physically addressed on a page raster:

- left/top use floor;
- right/bottom use ceiling;
- clamping occurs only at the raster boundary;
- a region with no non-empty intersection fails closed.

This preserves the earlier decision that source evidence itself is not silently
rewritten merely because a physical raster cannot contain out-of-page pixels.

## Serving boundary

The concrete V1 client targets the PaddleOCR General OCR basic-serving contract:

```text
POST /ocr
```

The request sends one already-cropped image and explicitly disables:

- document orientation classification;
- document unwarping;
- text-line orientation classification;
- visualization output.

The request sets `textRecScoreThresh` to `0` so low-confidence recognizer
evidence is not silently discarded by an application-chosen threshold at this
stage.

The response parser requires one image result and validates the parallel
`rec_texts`, `rec_scores`, and `rec_boxes` arrays. OCR boxes are mapped from
crop-local pixels back to normalized full-page coordinates.

The client also retains the same operational safeguards used by the layout
serving boundary:

- caller cancellation;
- finite request timeout;
- bounded raster input;
- bounded HTTP response;
- HTTP status validation;
- service `errorCode` validation;
- response-schema validation;
- seekable input-position restoration.

## Deliberate non-decisions

This increment does **not** add:

- a generic `IOcrEngine` plugin abstraction;
- a second OCR backend;
- figure persistence;
- table OCR;
- `Unknown` OCR;
- native/OCR reconciliation;
- cross-page hybrid continuity;
- final document-text authority rules;
- an image-decoding/cropping NuGet dependency.

The engine now owns the exact crop geometry. The next live integration can
materialize those crops in the evaluation harness without forcing a general
image library into the production engine before the rasterization boundary is
designed.

## Live targeted OCR validation

Phase B / 15B validated the production path on 2026-08-14 against the pinned
Ehrman physical page 233 using real self-hosted PP-StructureV3 and PaddleOCR
services.

The live layout produced 10 neutral observations. The deterministic planner
authorized exactly seven OCR regions:

```text
2, 3, 5, 6, 7, 8, 9
```

The papyrus remained observation sequence 4:

```text
Figure -> PreserveVisualWithoutOcr
```

and therefore produced no OCR crop, no OCR HTTP request, and no OCR evidence.

Exactly seven real PaddleOCR `/ocr` requests were issued. Every authorized
region produced OCR evidence, and the curated modern-text sentinels for the
heading, left body, caption, and right opening were recovered.

Detailed evidence is recorded in:

```text
docs/evaluation/targeted-ocr-live-integration-v1.md
docs/evaluation/targeted-ocr-live-integration-v1.json
```

The raw OCR text and raster crops remain local evaluation artifacts and are not
committed.

Phase B / 15 is therefore complete.

## Next step — Phase 16

Implement neutral figure preservation.

The first increment should preserve enough evidence to identify and audit a
visual asset:

```text
source document identity
physical page number
normalized bbox
pixel crop dimensions
content hash
extraction/raster profile
source layout observation association
```

Do not add ApologiaStudio semantics, generic visual taxonomies, native/OCR
reconciliation, or end-to-end hybrid orchestration in the same increment.
