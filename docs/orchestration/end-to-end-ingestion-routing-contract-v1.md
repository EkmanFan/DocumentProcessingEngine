# Phase 21.0 — end-to-end ingestion routing contract V1

## Status

Architecture/contract freeze before Phase 21 execution code.

Phase 20 completed the neutral result boundary:

```text
completed hybrid document
        ↓
DocumentIngestionResultBuilder
        ↓
DocumentIngestionResult
        ↓
optional JSON V1 transport
```

Phase 21 now makes the engine itself responsible for selecting and composing the
already-proven processing capabilities.

This increment deliberately introduces only the page-routing contract. It does
not yet implement end-to-end ingestion execution.

---

## 1. Target Phase 21 flow

The intended concrete V1 flow remains:

```text
document source
        ↓
format / preflight
        ↓
native extraction
        ↓
deterministic page assessment
        ↓
IPageProcessingPolicy
        ↓
PageProcessingPlan
        ↓
known processing capabilities
        ↓
assembly
        ↓
normalization
        ↓
segmentation
        ↓
DocumentIngestionResultBuilder
        ↓
DocumentIngestionResult
```

The processing component will coordinate existing capabilities. It must not
duplicate their algorithms.

---

## 2. Open/closed boundary

Page-routing policy is an explicit axis of variation.

The stable consumer depends on:

```text
IPageProcessingPolicy
        ↓
Decide(PageProcessingAssessment)
        ↓
PageProcessingPlan
```

A future policy may select a different supported route for the same assessment
without changing the processing component that consumes the plan.

That is the intended Open/Closed Principle boundary.

V1 does **not** interpret OCP as a requirement to create one Strategy class per
native/layout/OCR/figure/table case.

The variation point is the **policy mapping**.

---

## 3. Assessment contract

V1 assessment is intentionally minimal:

```text
PageProcessingAssessment
  PhysicalPageNumber
  NativeTextStatus
```

`NativeTextStatus` remains the existing neutral vocabulary:

```text
Healthy
Missing
Suspicious
```

This increment does not invent a second competing status enum.

The later Phase 21 implementation must derive this page-level assessment from
deterministic preflight/native evidence.

The policy must not infer it from:

- an LLM;
- layout/OCR model recommendations;
- downstream consumer semantics;
- arbitrary mutable global state.

`DocumentPreflightResult` remains document-level evidence. It is not itself the
page-routing policy.

---

## 4. Plan contract: one atomic route

V1 does not use:

```text
bool UseNative
bool RunLayout
bool RunOcr
bool Reconcile
bool PreserveVisual
...
```

as independent mutable choices.

That representation would permit contradictory plans such as:

```text
RunOcr = true
RunLayout = false
```

Instead the plan contains one atomic route:

```text
PageProcessingRoute.NativeOnly

PageProcessingRoute.LayoutWithTargetedOcrRecovery

PageProcessingRoute.LayoutWithTargetedOcrReconciliation
```

`PageProcessingPlan` exposes derived convenience facts such as:

```text
RequiresRasterization
RequiresLayoutAnalysis
RequiresTargetedOcr
RequiresReconciliation
```

but callers cannot independently construct contradictory boolean combinations.

---

## 5. Expected default V1 mapping

The concrete default policy implemented later should initially preserve the
already-proven deterministic behavior:

```text
Healthy
    → NativeOnly

Missing
    → LayoutWithTargetedOcrRecovery

Suspicious
    → LayoutWithTargetedOcrReconciliation
```

Phase 21.0 documents this expected default mapping but intentionally does not
hard-code it into `PageProcessingPlan`.

That distinction matters:

```text
contract
  says which routes are executable

policy
  decides which route to choose
```

A different policy can therefore choose differently among supported routes
without changing the plan model or processing component.

---

## 6. Region policy remains separate

A page route that performs layout does **not** replace the existing
`LayoutTreatmentPolicy`.

After layout, region-level treatment remains:

```text
Text / Heading / Caption / Table
    → RecognizeText

Figure
    → PreserveVisualWithoutOcr

Unknown
    → Deferred
```

Therefore `PageProcessingPlan` intentionally has no independent
`PreserveVisuals` flag.

When layout identifies a figure, the existing deterministic region policy owns
that decision.

This avoids two competing policy sources.

---

## 7. What OCP covers — and what it does not

This boundary is closed against changes such as:

```text
"for this deterministic assessment, choose another already-supported route"
```

because a new `IPageProcessingPolicy` implementation can be supplied.

It is **not** designed to make the engine magically closed against the addition
of a fundamentally new processing capability.

For example, adding a future specialized formula-recognition stage may
legitimately require:

- a new supported route/capability;
- execution changes;
- provenance changes;
- tests.

Phase 21.0 does not introduce a generic capability registry, DAG or plugin
framework to speculate about such changes.

---

## 8. Policy purity

`IPageProcessingPolicy.Decide(...)` is synchronous because it is a pure
deterministic decision boundary.

A policy implementation must perform no:

- file/network I/O;
- PDF extraction;
- rasterization;
- layout analysis;
- OCR;
- visual persistence;
- reconciliation;
- model/service lifecycle;
- persistence.

Those are execution responsibilities.

The policy only returns intent.

---

## 9. Execution boundary to implement next

The future processing component should expose a narrow public operation
conceptually equivalent to:

```csharp
Task<DocumentIngestionResult> IngestAsync(
    DocumentSource source,
    CancellationToken cancellationToken = default);
```

Its configured dependencies belong to the processor instance, not to each
method call.

Exact naming is intentionally left to the first vertical implementation, where
constructor dependencies can be verified against real code rather than guessed
in this contract-only increment.

---

## 10. Infrastructure boundary

The ingestion processor may call configured layout/OCR/raster capabilities.

It must not own deployment/service lifecycle such as:

```text
docker run
docker stop
container image selection
model download/install
service deployment
```

That belongs to the host/operations boundary.

---

## 11. Provenance-context ownership

Phase 19 deliberately left run-level identities such as native extraction,
rasterization and layout identities in `DocumentProcessingProvenanceContext`.

Phase 21 execution will own creation of that context from the actual configured
run before calling:

```text
DocumentIngestionResultBuilder.Build(...)
```

The processor must not manufacture identities that were not used.

---

## 12. Non-goals of Phase 21.0

No production execution is introduced here.

Specifically no:

- source opening/extraction orchestration;
- raster execution;
- layout invocation;
- OCR invocation;
- visual-byte destination/storage decision;
- reconciliation execution;
- normalization/segmentation execution;
- `DocumentIngestionResultBuilder` invocation;
- JSON serialization;
- persistence;
- retrieval chunks;
- embeddings;
- vector database;
- ApologiaStudio dependency;
- generic `IStep<T>` pipeline;
- middleware chain;
- DAG engine;
- plugin/capability registry;
- Docker lifecycle.

---

## 13. Phase 21 implementation sequence

```text
21.0  page-processing policy + plan contract      THIS INCREMENT

21A   native-only vertical execution
      - one public ingestion entry point
      - real native extraction
      - assembly / normalization / segmentation
      - provenance-context construction
      - DocumentIngestionResult

21B   deterministic page assessment + default policy integration
      - Healthy / Missing / Suspicious evidence
      - expected V1 mapping
      - no backend-driven policy

21C   hybrid execution
      - raster / layout
      - existing region treatment policy
      - targeted OCR
      - visual preservation
      - reconciliation
      - same common downstream path

21D   real-corpus end-to-end regression
      - born-digital control
      - missing-native recovery
      - mixed visual/text
      - suspicious Conflict
      - healthy/native control
```

Do not collapse 21A–21D into one large change.

---

## 14. Architectural invariant

The final responsibility split is:

```text
page assessment
    = deterministic evidence about the page

IPageProcessingPolicy
    = choose one supported route

processing component
    = execute the chosen route and compose capabilities

specialized capabilities
    = perform extraction/layout/OCR/reconciliation/etc.

DocumentIngestionResultBuilder
    = project the completed graph into the portable result
```

The policy decides.

The processing component coordinates.

The specialized components do the actual work.

No layer should silently absorb all three responsibilities.
