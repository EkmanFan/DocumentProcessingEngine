# Phase 21E.1H.4C — True shadow document planning V1

## Status

Production true-shadow integration.

The candidate two-axis planning chain now runs automatically inside
`DocumentProcessor` when explicitly configured, but its output remains
non-authoritative.

The execution law is:

```text
authoritative legacy planner
        ↓
PageProcessingDecision[]
        ↓
requiresHybridExecution
ResolveHybridExecution
        ↓
legacy execution
```

alongside:

```text
same extraction
        ↓
native normalization
        ↓
H.4A visual raster observations
        ↓
H.4B structural enrichment
        ↓
GuardedDocumentPageExecutionPlanner
        ↓
DocumentShadowPlanningReport
        ↓
observer / evaluation only
```

The candidate report is **not** read back into runtime execution.

---

## 1. Explicit opt-in

Existing `DocumentProcessor` construction remains valid and has shadow planning
disabled by default.

Shadow planning is enabled only when the caller supplies:

```text
DocumentShadowPlanningDependencies
```

containing:

```text
IVisualRasterObservationSource
IDocumentShadowPlanningObserver
DocumentTextNormalizer
DefaultVisualStructuralEvidenceEnricher
GuardedDocumentPageExecutionPlanner
```

The deterministic Engine components default to their production
implementations. The format-specific visual source stays behind the Core
interface, so `DocumentProcessing.Engine` does not take a dependency on
`DocumentProcessing.Pdf`.

For PDF composition the caller supplies:

```text
PdfPigVisualRasterObservationSource
```

---

## 2. Authoritative execution remains legacy

`DocumentProcessor` first creates and validates the existing legacy decisions:

```text
_pageProcessingPlanner.Plan(extraction)
```

It then computes:

```text
requiresHybridExecution
```

and calls the existing:

```text
ResolveHybridExecution(...)
```

**before** shadow work.

This preserves legacy failure ordering. A document that needs hybrid execution
but was constructed without hybrid dependencies still fails for that reason
before optional shadow work begins.

When shadow is enabled, the processor calls `DocumentShadowPlanningRunner`
after legacy route/dependency resolution and before page execution.

After the shadow call, all execution variables still come exclusively from:

```text
decisions
requiresHybridExecution
hybridExecution
```

The candidate plan is never substituted into any of them.

---

## 3. Complete production shadow chain

`DocumentShadowPlanningRunner` executes:

```text
capability check
    ↓
DocumentTextNormalizer
    ↓
IVisualRasterObservationSource
    ↓
DefaultVisualStructuralEvidenceEnricher
    ↓
GuardedDocumentPageExecutionPlanner
    ↓
DocumentShadowPageComparison[]
```

For each page the comparison retains:

```text
AuthoritativeLegacy
Shadow.Legacy
Shadow.Candidate
```

This matters because the guarded planner recomputes its legacy branch.

The report therefore exposes:

```text
LegacyPlanningAgreement
```

which requires equality of:

```text
NativeTextStatus
PageProcessingRoute
```

between the already-authoritative processor decision and the guarded planner's
legacy branch.

A custom legacy planner can therefore be detected rather than silently assumed
equivalent.

---

## 4. Shadow failure isolation

True shadow work must not become a new availability dependency.

The runner converts ordinary non-cancellation failures into:

```text
DocumentShadowPlanningStatus.Failed
DocumentShadowPlanningFailure
```

with a deterministic failure stage:

```text
Capability
NativeNormalization
RasterObservation
StructuralEnrichment
CandidatePlanning
```

The report is delivered best-effort.

Failures in `IDocumentShadowPlanningObserver` are also isolated.

Two exceptions are deliberate:

```text
caller-requested cancellation propagates
OutOfMemoryException is not swallowed
```

A memory exhaustion event is not treated as a harmless diagnostics failure.

`DocumentProcessor` resets its prepared seekable source around shadow work, so a
misbehaving or failed shadow source cannot leave the authoritative execution
path at an arbitrary stream position.

---

## 5. Report semantics

`DocumentShadowPlanningReport` exposes:

```text
Status
Failure
Pages
LegacyPlanningAgreementExact

CandidateRemovesLegacyTextMlCount
CandidateAddsIndependentVisualWorkToLegacyNativePageCount
CandidateNativeTextPageCount
CandidateTargetedOcrPageCount
CandidateVisualAnalysisPageCount
CandidateMeaningfulVisualPreservationPageCount
```

`CandidateRemovesLegacyTextMl` remains an evaluation signal only.

It never authorizes cutover.

---

## 6. Real-corpus gate

The delivery script executes the production shadow runner over the pinned:

```text
Ehrman
De Decretis
Habermas
```

corpora with:

```text
PdfPigDocumentExtractor
PdfPigVisualRasterObservationSource
DocumentTextNormalizer
DefaultVisualStructuralEvidenceEnricher
GuardedDocumentPageExecutionPlanner
```

It validates the frozen native-status counts:

```text
Ehrman
  Missing       286
  Suspicious    119
  Unverified    212
  Healthy         0

De Decretis
  Healthy      1477
  Missing         1
  Suspicious      1
  Unverified      0

Habermas
  Healthy       159
  Missing        11
  Suspicious      0
  Unverified      0
```

For every page it rechecks the guarded text-safety invariant:

```text
Healthy
    -> NativeText

Missing
    -> TargetedOcrRecovery

Suspicious
    -> TargetedOcrReconciliation

Unverified
    -> NativeText OR TargetedOcrVerification
```

It also requires exact legacy-planner agreement on every page.

The frozen 21E.1F/H.3 policy predicts for Ehrman's 212 Unverified pages:

```text
211 -> NativeText
  1 -> TargetedOcrVerification
```

so the real-corpus gate requires:

```text
Ehrman CandidateRemovesLegacyTextMlCount == 211
```

This is a shadow optimization measurement, not a cutover.

---

## 7. No ML execution in H.4C shadow planning

The candidate can *plan*:

```text
TargetedOcr...
AnalyzeVisual
PreserveMeaningfulVisual
```

but H.4C does not execute those candidate actions.

Therefore the shadow branch invokes neither:

```text
PP-StructureV3
PaddleOCR
candidate raster execution
candidate visual preservation
```

H.4A's embedded-source visual decoding is evidence acquisition, not page ML
layout/OCR execution.

The legacy runtime continues to invoke its existing services according to the
legacy route only.

---

## 8. Cutover remains later

H.4C answers:

```text
What would the candidate planner do on the same real document?
```

It does **not** answer:

```text
Can the new execution plan replace legacy runtime execution?
```

Before cutover we still need:

```text
real-corpus shadow report review
resource/performance measurement of the added observation path
execution integration for independent visual actions
end-to-end output parity/safety regression
explicit cutover decision
```

The candidate remains evidence, not authority.
