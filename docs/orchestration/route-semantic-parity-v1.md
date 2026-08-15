# Route semantic parity contract V1

## Status

Phase 21C.0 regression-contract increment.

This increment does not add hybrid execution. It freezes the semantic property
that must remain true when Phase 21C connects the Phase 21B planner to the
already-proven reconciliation and hybrid-processing capabilities.

## Why this contract exists

Phase 21B corrected the page-assessment model:

```text
Healthy
Missing
Suspicious
Unverified
```

For image-backed native text, `Unverified` means that current native evidence
cannot establish fidelity to the visible page. It is deliberately not evidence
of corruption.

Both pinned Ehrman reconciliation controls are therefore `Unverified` before
secondary evidence:

```text
physical page 380 -> Unverified
physical page 405 -> Unverified
```

The route is the same:

```text
LayoutWithTargetedOcrReconciliation
```

but the later reconciliation result is not:

```text
page 380 -> Conflict
page 405 -> Agreement -> NativePdf
```

The distinction therefore belongs to secondary native/OCR evidence.

## Architectural invariant

Changing execution route must not silently change authoritative document
semantics when the two routes should converge.

For a native text block whose OCR verification agrees:

```text
native-only reference
        ↓
NativePdf authoritative text

Unverified
        ↓
OCR verification
        ↓
Agreement
        ↓
NativePdf authoritative text
```

The following must remain equal:

```text
selected authoritative text
normalized authoritative text
structural segment text
```

The following is expected to differ:

```text
processing history
OCR evidence
reconciliation evidence
provenance richness
```

Therefore full `DocumentIngestionResult` JSON byte equality is not a valid
parity requirement.

## Conflict invariant

For `Unverified` native text whose OCR evidence disagrees:

```text
Unverified
   ↓
reconciliation
   ↓
Conflict
   ↓
TextSelectionOrigin.None
   ↓
no authoritative narrative text
```

The engine must not silently fall back to native text merely because a native
layer exists.

If OCR yields no usable text for `Unverified` native evidence, the candidate
also remains unresolved.

## Scope of Phase 21C.0

This increment adds regression tests against the already-existing production
boundaries:

```text
NativeOcrTextReconciler
HybridDocumentElementFactory
HybridDocumentAssembler
HybridDocumentNormalizer
HybridDocumentSegmenter
```

It proves the semantic contract synthetically and checks that committed
real-corpus Phase 19C evidence still contains:

```text
p405 -> Agreement -> NativePdf
p380 -> Conflict  -> None
```

It intentionally does not claim automatic end-to-end route execution.

## Phase 21C / 21D acceptance refinement

Phase 21C must connect:

```text
automatic assessment
    -> automatic policy
    -> automatic route execution
```

without changing the semantics frozen here.

Phase 21D must prove the complete route on real corpus controls:

```text
De Decretis
  Healthy -> NativeOnly
  no native regression

Ehrman p405
  Unverified -> Reconciliation -> Agreement -> NativePdf
  authoritative/normalized/structural text parity with native reference

Ehrman p380
  Unverified -> Reconciliation -> Conflict
  no silent authoritative selection

Ehrman p233
  Missing -> Recovery -> OCR authoritative
  Figure preserved and never promoted to narrative OCR
```

## Non-goals

No:

- raster implementation;
- layout service invocation;
- OCR service invocation;
- automatic native/layout pairing;
- visual storage implementation;
- `DocumentProcessor` modification;
- Docker/model lifecycle;
- generic pipeline/DAG/plugin registry;
- persistence/RAG change.

The next implementation increment should address the concrete execution I/O
boundaries required by the hybrid route before `DocumentProcessor` is expanded.
