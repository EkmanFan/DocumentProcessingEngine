# Phase 16.3 — Dual Run V1 PlanningOnly worker composition and parity

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `dd4ad89`

## Purpose

This increment replaces the worker bootstrap's synthetic
`PlanningNotImplemented` result with real deterministic `PlanningOnly`
execution.

The worker still does not execute candidate OCR/layout ML or `Full` mode.

## Worker PlanningOnly pipeline

The isolated worker owns `source.bin` and independently re-extracts the PDF.

The pipeline is:

```text
source.bin
    -> PdfPigDocumentExtractor.ExtractWithRasterObservationsAsync
    -> DocumentTextNormalizer
    -> DefaultVisualStructuralEvidenceEnricher
    -> GuardedDocumentPageExecutionPlanner.CreateDefault()
    -> compact DocumentDualRunWorkerPageResult[]
    -> Completed result.json
```

The PDF extractor and `PdfPigVisualRasterObservationSource` use the existing
coordinated single-pass seam. PlanningOnly therefore does not perform one native
PDF pass followed by a second PdfPig raster-observation pass inside the worker.

## No ML in PlanningOnly

PlanningOnly invokes no:

```text
PP-StructureV3
PaddleOCR
pdftoppm page rasterization
candidate OCR execution
candidate visual preservation
```

The candidate plan may require future layout/OCR work; PlanningOnly only reports
that deterministic requirement.

## Authoritative comparison

The worker never reconstructs an authoritative `HybridDocumentPage` graph.

It compares the recomputed guarded-planner authoritative branch to the compact
transport baseline:

```text
NativeTextStatus
PageProcessingRoute
```

`AuthoritativePlanningAgreement` is true only when both match.

`CandidateRemovesAuthoritativeTextMl` is evaluated against the transported
authoritative route, not against the worker's recomputed route. This preserves
the meaning of the Dual Run comparison if the recomputed authoritative branch
itself disagrees.

The transported text fingerprints and counts remain reserved for `Full`
candidate execution comparison and are not consumed by PlanningOnly.

## Parity proof

A real process-boundary integration test builds a deterministic two-page PDF:

```text
page 1: native text -> Healthy / NativeOnly
page 2: blank       -> Missing / LayoutWithTargetedOcrRecovery
```

The current in-process `DocumentDualRunPlanningRunner` is executed first on the
same bytes.

The worker then independently re-extracts the snapshot and its compact
PlanningOnly result is compared page-by-page against the current in-process
report for:

```text
physical page
authoritative planning agreement
candidate text mode
candidate removes authoritative text ML
candidate requires visual analysis
candidate requires meaningful visual preservation
```

A separate test intentionally changes the transported authoritative route for
page 1 and proves that the worker reports disagreement while retaining the same
candidate plan.

## Full mode

`Full` remains explicitly unavailable at this checkpoint.

After source validation the worker returns:

```text
status = Failed
failure stage = CandidateExecution
exception type = FullExecutionNotImplemented
pages = []
```

This prevents accidental partial Full execution from being mistaken for a
completed result.

## Failure boundary

PlanningOnly extraction, raster-observation, normalization, enrichment, or
planning failures become a structured worker result with:

```text
status = Failed
failure stage = Planning
pages = []
```

Process launch/crash/timeout remain supervisor outcomes.

`OutOfMemoryException` is still not swallowed by the worker application layer.

## Still unresolved

The following remain intentionally outside this increment:

```text
background dispatcher consumer
Full candidate execution
PP-StructureV3/PaddleOCR worker composition
OS cgroup/container CPU/memory isolation
worker/parent version compatibility enforcement
DocumentProcessor integration
removal of transitional InProcess production scaffolding
```

The next engineering step is `Full` worker execution only after this
PlanningOnly parity checkpoint is reviewed and committed.
