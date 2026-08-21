# Phase 16.3 — Dual Run V1 isolated worker boundary

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `1a40d05`

## Status

Architecture frozen before worker mutation.

This note defines the V1 direction. The existing
`DocumentProcessing.Engine/DualRun/InProcess` implementation remains
transitional evidence until out-of-process parity is demonstrated.

## Non-negotiable invariants

- The authoritative result is complete before candidate execution can affect
  anything outside Dual Run telemetry.
- Candidate output never chooses or mutates the authoritative result.
- Worker crash, native crash, abort, OOM, deadlock, timeout, queue saturation,
  launch failure, IPC/file failure, or non-zero exit cannot fail authoritative
  processing.
- `Disabled` creates no Dual Run source snapshot, queue item, worker, envelope,
  layout/OCR request, or candidate execution.
- PP-StructureV3 and PaddleOCR remain lazy. V1 permits at most one Full Dual Run
  worker at a time on the current machine.
- No message broker, plugin hot-loading, or distributed worker infrastructure is
  introduced in V1.

## Profiles and document selection

Configuration is snapshotted once per document.

```text
Disabled
  -> no Dual Run work

PlanningOnly
  -> deterministic candidate planning/comparison

Sampled
  -> stable source-SHA-256 bucket
  -> selected document: Full
  -> unselected document: no Dual Run work

Full
  -> complete candidate planning + candidate execution
```

`Sampled` uses 10,000 deterministic buckets. The first 64 bits of the source
SHA-256 are interpreted as an unsigned hexadecimal integer and reduced modulo
10,000. The configured basis-point threshold selects the stable cohort.

Sampling is resolved in the parent. The worker receives only
`PlanningOnly` or `Full`; it does not implement sampling policy.

## Dependency direction

Target conceptual dependency:

```text
Authoritative -> Planning <- DualRun
```

`DocumentProcessor` must end Phase 16.3 without a dependency on
`DocumentProcessing.Engine.DualRun.InProcess`.

The worker host is a separate executable project:

```text
src/DocumentProcessing.DualRunWorker
```

It may reference:

```text
DocumentProcessing.Core
DocumentProcessing.Engine
DocumentProcessing.Pdf
```

None of those libraries reference the worker executable project.

The current in-process runners are retained only until worker parity is proven,
then removed rather than kept as a second production execution path.

## Parent / worker transport

V1 uses a same-host file-backed job directory plus a process boundary.

No named pipes, Unix sockets, broker, or custom streaming protocol are required
for V1.

One accepted job owns a private spool directory containing conceptually:

```text
source.bin
request.json
result.json
```

The parent creates an independent immutable source snapshot for Dual Run. It
must not lend the caller-owned `DocumentSource.Content` stream or the
authoritative processor's temporary-stream lifetime to the worker.

Snapshot acquisition is best-effort Dual Run preparation. Failure to create the
snapshot drops Dual Run work and cannot fail the authoritative result.

The worker verifies source SHA-256 and byte length before processing.

## Minimum request envelope

The request is a versioned transport DTO, not serialized runtime object graphs.

It contains:

```text
schema version
job ID
resolved execution mode: PlanningOnly | Full
engine version
source snapshot path
source SHA-256
source byte length
file name, if supplied
declared media type, if supplied
document format
authoritative page baselines
```

The request must not contain:

```text
Stream
DocumentSource
DocumentExtractionResult
DocumentExtractionWithRasterObservationsResult
HybridDocumentPage
raster sessions
layout/OCR client instances
observers
service-provider objects
```

## Authoritative page baseline

The worker independently re-extracts the immutable source snapshot.

The parent sends only the comparison facts needed to prove parity against the
actual authoritative run. Per physical page the baseline contains:

```text
physical page number
authoritative native-text status
authoritative PageProcessingRoute
authoritative selected-text-sequence fingerprint
authoritative text-projection fingerprint
authoritative text-element count
authoritative reconciliation-evidence count
```

Fingerprints use a versioned canonical projection plus SHA-256. They replace
transport of the full authoritative `HybridDocumentPage` graph while retaining
the existing equality checks:

```text
AuthoritativePlanningAgreement
SelectedTextSequenceExact
TextProjectionExact
```

The canonical projection algorithm is shared deterministic code, not duplicated
between parent and worker.

## Worker composition

`PlanningOnly` composes only what its planning chain requires:

```text
native PDF extraction
native normalization
visual raster observation
structural evidence enrichment
guarded page planning
comparison against authoritative baseline
```

`Full` adds candidate execution capability lazily:

```text
Pdftoppm rasterization
PP-StructureV3 layout
PaddleOCR targeted recognition
candidate page assembly/reconciliation
candidate comparison projection
```

PP-StructureV3 and PaddleOCR must not become eagerly resident merely because the
worker exists.

## Dispatch and supervision

The parent-side dispatcher owns a bounded in-memory queue.

V1 uses one consumer and at most one active Full worker. Queue capacity is
explicit configuration.

Enqueue is `Try` semantics: it does not wait for capacity. Queue saturation
returns a dropped-dispatch outcome and leaves the authoritative result intact.

Each job launches an isolated worker process. The supervisor owns:

```text
launch
timeout
process-tree termination
exit-code observation
result-file validation
spool cleanup
telemetry
```

Worker timeout is explicit configuration. No unmeasured production default is
invented before Phase 16.4 evidence.

Production composition must apply an OS resource boundary to the worker and any
worker-owned ML service processes, using an external cgroup/container/service
wrapper appropriate to the deployment. Process isolation alone is not memory or
CPU isolation.

## Cancellation

Caller cancellation retains normal authority while authoritative processing is
still running.

Once the authoritative result has been completed, the background Dual Run
worker lifecycle is detached from the caller cancellation token. Candidate
cancellation or worker termination cannot retroactively invalidate a completed
authoritative result.

## Result and telemetry boundary

The worker writes a versioned result DTO containing comparison summaries and
failure evidence.

It does not return candidate document objects to `DocumentProcessor`.

The parent supervisor may translate the worker result into Dual Run telemetry,
but telemetry export remains best-effort and separate from authoritative
processing.

Authoritative and Dual Run timing/resource metrics remain separate.

## Existing in-process tests

Current in-process tests remain useful while parity is being built.

Tests that assert `OutOfMemoryException` propagates from an in-process candidate
runner describe the transitional runner itself; they are not the final
authoritative-process failure contract.

Before the in-process path is removed, new process-boundary tests must prove at
least:

```text
queue full                 -> authoritative result unaffected
worker launch failure      -> authoritative result unaffected
worker non-zero exit       -> authoritative result unaffected
worker crash/abort         -> authoritative result unaffected
worker timeout/deadlock    -> worker killed; authoritative result unaffected
worker OOM                 -> worker loss only; authoritative result unaffected
malformed result.json      -> telemetry failure only
source hash mismatch       -> worker rejects job; authoritative result unaffected
```

## Migration sequence

```text
1. freeze profile/mode/selection contracts
2. add versioned request/result transport contracts
3. add source-snapshot custody
4. add bounded dispatcher + supervisor abstraction
5. add worker executable with PlanningOnly
6. prove PlanningOnly parity
7. add Full candidate execution lazily
8. prove Full parity and crash/timeout/OOM isolation
9. remove DocumentProcessor -> DualRun/InProcess coupling
10. remove transitional in-process production scaffolding
11. measure final Disabled / PlanningOnly / Sampled / Full baselines
```

This ordering keeps each increment reversible and prevents a transport decision
from silently becoming an authority decision.
