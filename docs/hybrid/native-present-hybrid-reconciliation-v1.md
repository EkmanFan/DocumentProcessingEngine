# Native-present hybrid reconciliation v1

## Status

Phase 21C.2B.4A implements the production execution route for pages whose
native PDF text exists but is not deterministically trusted.

This increment is intentionally **not complete on unit tests alone**.
Phase 21C.2B.4B must validate the staged implementation against the pinned real
corpus before commit.

## Route

```text
native text present
        |
        v
Suspicious / Unverified page decision
        |
        v
full-page raster
        |
        v
layout analysis
        |
        v
deterministic region treatment
        |
        +-------------------+------------------------+
        |                   |                        |
 RecognizeText   PreserveVisualWithoutOcr         Deferred
        |                   |                        |
        v                   v                        v
native/layout pairing   preserve raster          no text
        |
        v
targeted OCR
        |
        v
native/OCR reconciliation
        |
        v
hybrid page
```

Healthy native pages remain on `NativeOnly`.

Missing native pages remain on the existing
`MissingNativeHybridPageExecutor` recovery route.

## Target-centric reconciliation

Phase 21C.2B.3 established:

```text
layout target
    -> 0..N per-source-block ComparableNativeTextExtent
    -> one ComparableNativeTextEvidence
```

The reconciliation boundary now consumes that aggregate without collapsing its
provenance.

For backward compatibility:

- `TextReconciliationInput.NativeBlock` remains populated with the first source
  block;
- `TextReconciliationResult.ComparableNativeExtent` remains populated when the
  aggregate contains exactly one extent;
- complete provenance remains available through
  `ComparableNativeEvidence.SourceBlocks` and `.Extents`.

No synthetic `DocumentTextBlock` is invented.

## Region behavior

### Comparable

```text
native aggregate + OCR
    -> deterministic dehyphenation
    -> conservative equivalence
```

Agreement:

```text
Agreement / NativePdf
```

Conflict on Suspicious or Unverified native evidence:

```text
Conflict / None
```

### NoNativeEvidence

A page may contain native text overall while one exact layout target has no
native projection.

At that target scope native evidence is treated as missing:

```text
NoNativeEvidence + OCR
    -> OcrOnly / Ocr
```

This does not promote unrelated page-level native blocks.

### AmbiguousWordOwnership

If the same projected native word is claimed by more than one OCR-authorized
layout target, execution fails closed before OCR authority selection.

No max-overlap winner is invented.

## Visual regions

`Figure -> PreserveVisualWithoutOcr` remains unchanged.

The native-present executor never sends Figure regions to the OCR recognizer.

## Explicit non-goals

This increment adds no:

- IoU or overlap threshold;
- fuzzy text similarity;
- OCR-confidence authority rule;
- LLM arbitration;
- second layout engine;
- generic plugin registry;
- new persistence backend;
- ApologiaStudio integration.

## Required real-corpus validation before commit

Phase 21C.2B.4B must validate the staged code against the pinned corpus:

```text
De Decretis
  Healthy -> NativeOnly
  no hybrid reconciliation route

Ehrman p405
  Unverified -> targeted OCR reconciliation
  -> Agreement / NativePdf

Ehrman p380
  Unverified -> targeted OCR reconciliation
  -> Conflict / None

Ehrman p233
  Missing -> existing recovery route
  -> Figure preserved without OCR
```

The commit must wait until those controls pass or a concrete failure is
reviewed.
