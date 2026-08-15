# Phase 21C.3 — DocumentProcessor hybrid route integration

## Status

Phase 21C.3 connects the deterministic page planner to the public
`DocumentProcessor` and executes every currently supported V1 page route.

No new document-understanding algorithm is introduced here. This increment is
composition.

## Public flow

```text
DocumentSource
    |
    v
type detection
    |
    v
native extraction
    |
    v
document preflight
    |
    v
DocumentPageProcessingPlanner
    |
    +----------------------+-------------------------------+
    |                      |                               |
 NativeOnly     LayoutWithTargetedOcrRecovery   LayoutWithTargetedOcrReconciliation
    |                      |                               |
 native page        MissingNativeHybridPageExecutor   NativePresentHybridPageExecutor
    |                      |                               |
    +----------------------+-------------------------------+
                           |
                           v
                HybridDocumentAssemblyResult
                           |
                           v
                     normalization
                           |
                           v
                      segmentation
                           |
                           v
                 provenance + quality
                           |
                           v
                 DocumentIngestionResult
```

The processor does not duplicate page-executor logic.

## Hybrid runtime composition

Hybrid dependencies are explicit:

```text
DocumentHybridExecutionDependencies
    - IDocumentRasterizer
    - MissingNativeHybridPageExecutor
    - NativePresentHybridPageExecutor
    - layout processing identity
    - reconciliation processing identity
```

This is a fixed composition object, not a capability/plugin registry.

The existing five-argument `DocumentProcessor` constructor is retained. It can
process documents whose page plans are all `NativeOnly`. If planning selects a
hybrid route, it fails explicitly rather than returning partial output.

## One document-scoped raster session

When at least one page requires hybrid execution:

```text
prepared source
    -> IDocumentRasterizer.OpenAsync exactly once
    -> one IDocumentRasterizationSession
    -> reused by every hybrid page
    -> disposed before final result projection
```

Native-only pages never invoke rasterization.

The raster processing identity recorded in provenance is taken from the actual
opened session (`BackendId` + `ProfileId`), not copied from a separate caller
string.

## Visual destination ownership

The existing call remains valid:

```csharp
ProcessAsync(source, cancellationToken)
```

A second overload permits caller-owned visual storage:

```csharp
ProcessAsync(
    source,
    openVisualDestinationAsync,
    cancellationToken)
```

The engine does not select filesystem paths, database records, object-store
keys, or another storage backend.

If a page contains a Figure and no visual destination was supplied, the
page executor continues to fail closed before region OCR/preservation side
effects.

## Preflight versus page routing

Document preflight remains document-level evidence. Page assessment/policy owns
execution routing.

The processor rejects the concrete contradiction:

```text
preflight != HealthyBornDigital
AND
every page route == NativeOnly
```

because that means the configured evidence producers disagree.

It does not use preflight classification as a blanket ban on hybrid execution.

## Provenance

Native-only runs continue to omit unused hybrid component identities.

For an executed hybrid run:

- rasterization identity comes from the actual document-scoped raster session;
- layout identity comes from the explicitly configured versioned layout
  component identity;
- OCR identities are still derived from actual `OcrRegionResult` evidence;
- reconciliation identity is emitted only when reconciliation evidence exists;
- visual-preservation profile IDs are derived from actual preserved visual
  evidence.

## Synthetic orchestration proof

The focused integration tests exercise a three-page document:

```text
page 1
  Healthy -> NativeOnly

page 2
  Missing -> targeted OCR recovery
  + Figure -> preserve, never OCR

page 3
  Unverified -> targeted OCR reconciliation
  -> Agreement / NativePdf
```

The proof requires:

```text
one raster session for the document
two full-page raster renders
three region renders
two OCR calls
one visual destination
```

It also verifies that an all-native document does not open the raster runtime,
that a hybrid route without configured hybrid dependencies fails explicitly,
and that Figure execution without a caller-owned destination fails before
region OCR.

## Non-goals

Phase 21C.3 adds no:

- new OCR/layout model;
- fuzzy reconciliation;
- overlap authority threshold;
- retries;
- concurrent page execution;
- Docker/model lifecycle management;
- persistence;
- RAG;
- ApologiaStudio adapter;
- generic DAG/pipeline framework;
- generic plugin registry.

## Next step

Phase 21D must prove the complete public `DocumentProcessor` path against the
pinned real corpus:

```text
De Decretis
  Healthy -> NativeOnly
  established native parity retained

Ehrman p233
  automatic Missing route
  OCR recovery
  Figure preserved and never OCR

Ehrman p380
  automatic Unverified route
  Conflict / None

Ehrman p405
  automatic Unverified route
  Agreement / NativePdf
```

That proof should use the public processor rather than calling page executors
directly.
