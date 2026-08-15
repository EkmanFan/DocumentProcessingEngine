# Missing-native hybrid page execution V1

## Status

Phase 21C.2A production composition increment.

## Purpose

Phase 21B can now decide:

```text
Missing
    -> LayoutWithTargetedOcrRecovery
```

Phase 21C.1 added the production raster execution boundary.

This increment composes the already-proven capabilities into the first real
hybrid page executor:

```text
missing-native page
    -> full-page raster
    -> layout
    -> deterministic region treatment
       -> Text/Heading/Caption/Table: targeted OCR
       -> Figure: preserve visual, never OCR
       -> Unknown: Deferred
    -> Missing/OCR reconciliation
    -> HybridDocumentElement
    -> HybridDocumentPage
```

No document-level orchestration is added yet.

## Why Phase 21C.2 is split

The missing-native route does not require native/layout spatial pairing.

The native-present reconciliation route does.

The current reconciliation boundary explicitly requires pairing to be supplied
by its caller. It does not perform automatic spatial matching.

Therefore:

```text
21C.2A
    Missing -> recovery
    no native pairing problem

21C.2B
    Suspicious/Unverified -> reconciliation
    deterministic native/layout pairing required and tested separately
```

This is deliberate evidence-driven sequencing. A spatial matcher is not being
invented merely to make one large "hybrid executor" commit possible.

## External service ports

Two narrow interfaces are introduced:

```text
IPageLayoutAnalyzer
IRegionTextRecognizer
```

They isolate actual external model-service volatility and make orchestration
testable.

Concrete V1 adapters remain:

```text
PpStructureV3PageLayoutAnalyzer
    -> PpStructureV3ServingClient

PaddleOcrRegionTextRecognizer
    -> PaddleOcrServingClient
```

This is not a plugin registry or multi-backend framework.

## Visual destination rule

Phase 16 deliberately established a caller-owned binary destination boundary.

`MissingNativeHybridPageExecutor` therefore accepts a caller-supplied function:

```text
LayoutObservation
    -> destination Stream
```

only when a Figure is actually present.

The executor:

- does not choose filesystem/database/object storage;
- does not dispose the caller-owned destination;
- does not create storage keys;
- does not introduce a generic storage-provider abstraction.

If layout identifies Figure evidence and no destination function was supplied,
execution fails closed before any region OCR or visual crop work begins.

## Deterministic region behavior

The executor does not reinterpret model labels.

It reuses:

```text
LayoutTreatmentPolicy
TargetedOcrPlanner
VisualPreservationPlanner
RasterCropGeometry
VisualAssetPreserver
NativeOcrTextReconciler
HybridDocumentElementFactory
HybridDocumentAssembler
```

Current policy remains:

```text
Text     -> RecognizeText
Heading  -> RecognizeText
Caption  -> RecognizeText
Table    -> RecognizeText
Figure   -> PreserveVisualWithoutOcr
Unknown  -> Deferred
```

Table OCR remains neutral text while its source
`LayoutObservationKind.Table` stays attached as provenance.

## Missing-native authority rule

Each OCR-authorized region is reconciled as:

```text
NativeTextStatus.Missing
nativeBlock = null
ocrRegion = recognized region
```

Therefore:

```text
OCR text exists
    -> OcrOnly
    -> TextSelectionOrigin.Ocr

OCR text absent
    -> NoTextRecovered
    -> unresolved
```

No hidden native fallback exists in this route.

## Failure behavior

The executor rejects:

- a decision for another physical page;
- a non-Missing native status;
- a route other than `LayoutWithTargetedOcrRecovery`;
- a non-full-page raster as layout input;
- layout evidence for another page;
- an OCR result that does not retain the exact source layout observation;
- a region raster whose crop or source dimensions differ from the deterministic
  plan;
- Figure evidence when the caller provides no visual destination.

Cancellation is propagated through raster, layout, OCR and preservation calls.

## Test coverage

The focused contract proves synthetically that one mixed page containing:

```text
Heading
Text
Table
Figure
Unknown
Caption
```

produces:

```text
4 targeted OCR calls
1 visual crop/preservation
0 OCR calls for Figure
0 OCR calls for Unknown

Heading -> Heading / Ocr
Text    -> Text    / Ocr
Table   -> Text    / Ocr + Table layout provenance
Figure  -> Visual  / preserved evidence
Unknown -> Deferred
Caption -> Caption / Ocr
```

It also proves:

- no region side effects occur when a Figure exists but no destination is
  available;
- empty OCR remains unresolved;
- the executor refuses the reconciliation route.

## Non-goals

Phase 21C.2A does not:

- implement automatic native/layout pairing;
- execute Suspicious/Unverified reconciliation;
- modify `DocumentProcessor`;
- start/stop Docker or model services;
- add retries/concurrency;
- change storage/persistence;
- change RAG concerns;
- claim end-to-end real-corpus driver completion.

The next increment is Phase 21C.2B: define and prove deterministic native/layout
pairing, then execute the native-present reconciliation route without violating
the route-semantic parity contract locked in Phase 21C.0.
