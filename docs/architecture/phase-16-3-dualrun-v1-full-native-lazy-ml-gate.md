# Phase 16.3 — Dual Run V1 Full NativeText execution and lazy ML gate

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `ce53785`

## Purpose

This increment begins Full worker execution without prematurely composing the
OCR/layout runtime.

The Full worker now executes candidate pages when every candidate plan selects
`NativeText`. If any page requires an OCR-backed text mode, the worker fails
closed before creating a rasterizer, PP-StructureV3 client, or PaddleOCR client.

This is the first half of Full execution. The next increment replaces the
explicit OCR-backed gate with lazy runtime composition.

## Shared worker planning pipeline

PlanningOnly and Full now use one worker-local deterministic planning pipeline:

```text
source.bin
    -> PdfPigDocumentExtractor.ExtractWithRasterObservationsAsync
    -> DocumentTextNormalizer
    -> DefaultVisualStructuralEvidenceEnricher
    -> GuardedDocumentPageExecutionPlanner.CreateDefault()
```

This prevents Full from introducing a second native/PdfPig planning
implementation or a second extraction pass.

## Full NativeText execution

For each NativeText candidate page:

```text
native blocks
    -> HybridDocumentElementFactory.FromNative
    -> HybridDocumentAssembler.AssemblePage
    -> authoritative text projection
    -> shared V1 text fingerprints
    -> compact Full worker page result
```

The result carries:

```text
ExecutedNativeText
selected-text-sequence equality
text-projection equality
authoritative/candidate text counts
authoritative/candidate reconciliation-evidence counts
planning agreement
candidate visual requirement axes
```

No runtime `HybridDocumentPage` crosses the process boundary.

## Lazy ML gate

The worker scans the completed candidate plan before Full execution.

If every page is `NativeText`, Full completes without:

```text
pdftoppm
PP-StructureV3
PaddleOCR
layout HTTP
OCR HTTP
```

If any candidate page requires:

```text
TargetedOcrRecovery
TargetedOcrVerification
TargetedOcrReconciliation
```

this checkpoint returns a structured worker failure:

```text
stage = CandidateExecution
exception type = System.InvalidOperationException
message identifies the first OCR-backed physical page
pages = []
```

No partial Full result is retained.

## Parity

A process-boundary integration test compares an all-native generated PDF against
the current in-process planning and candidate execution path.

The comparison covers:

```text
authoritative planning agreement
candidate text mode
candidate execution status
candidate removes authoritative text ML
selected-text-sequence equality
text-projection equality
authoritative/candidate text counts
authoritative/candidate reconciliation counts
candidate visual requirement axes
```

A second generated fixture contains a blank page and proves that Full fails
closed at the OCR-backed runtime gate.

## Why this increment precedes PP/Paddle composition

The ML runtime is the highest-cost and highest-operational-risk part of Full
execution.

Separating the NativeText cut first proves:

1. Full transport semantics independently of ML;
2. shared planning is reused rather than duplicated;
3. all-native Full documents incur zero ML/raster execution;
4. OCR-backed execution cannot accidentally start before explicit composition.

## Still unresolved

The following remain intentionally outside this increment:

```text
lazy pdftoppm + PP-StructureV3 + PaddleOCR Full runtime
OCR-backed Full parity
background dispatcher consumer
OS cgroup/container CPU/memory isolation
worker/parent version compatibility enforcement
DocumentProcessor integration
removal of transitional InProcess production scaffolding
```

The next increment is the lazy OCR-backed Full runtime and its parity proof.
