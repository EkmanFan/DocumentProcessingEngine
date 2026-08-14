# Targeted OCR production boundary V1

## Status

Phase B / 15A production-boundary increment.

This increment establishes the neutral OCR evidence model, deterministic OCR
target planning, crop geometry, and the concrete PaddleOCR General OCR serving
client.

It intentionally stops before claiming live targeted OCR on the real corpus.

```text
15 Targeted OCR
   15A neutral model + planner + serving client  THIS INCREMENT
   15B real targeted OCR integration             NEXT
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

## Next step — Phase 15B

Validate the production path against the pinned Ehrman physical page 233 and a
real self-hosted PaddleOCR 3.7 General OCR service.

Acceptance must include:

```text
Heading -> OCR
Text    -> OCR
Figure  -> NO OCR
Caption -> OCR
Text    -> OCR
```

The papyrus Figure is the critical negative control: no OCR request may be
issued for layout observation sequence 4.

Representative modern-text sentinels should be recovered from the authorized
regions, while OCR evidence retains page/region/profile/confidence provenance.

Only after this real integration proof should Phase 15 be marked DONE.
