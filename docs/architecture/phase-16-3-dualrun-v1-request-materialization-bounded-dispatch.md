# Phase 16.3 — Dual Run V1 request materialization and bounded dispatch

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `999e76d`

## Scope

This increment adds:

```text
atomic request.json materialization
explicit local-job ownership
bounded non-blocking dispatch semantics
consumer-side non-blocking dequeue
```

It still does **not**:

```text
modify DocumentProcessor
run a background consumer
launch a worker process
launch PP-StructureV3
launch PaddleOCR
```

## Materialized local job

A successfully prepared job is:

```text
<spool-root>/
    job-<job-id>-<random>/
        source.bin
        request.json
```

`request.json` is first written as:

```text
request.json.partial
```

and promoted only after the complete strict V1 JSON has been written.

The materializer validates before writing:

```text
request JobId == snapshot JobId
request source path == owned source.bin path
request SHA-256 == snapshot SHA-256
request byte length == snapshot byte length
source.bin resides directly in the owned job directory
```

The transport model itself remains the strict
`document-dual-run-request-v1` contract.

## Ownership

Ownership is explicit:

```text
source snapshot factory succeeds
    -> caller owns DocumentDualRunSourceSnapshot

request materializer succeeds
    -> DocumentDualRunPreparedJob owns source snapshot
    -> caller owns prepared job

TryDispatch == Enqueued
    -> dispatcher owns prepared job

TryDispatch == QueueFull
    -> caller still owns prepared job
    -> caller must dispose/drop it

TryDispatch == Stopped
    -> caller still owns prepared job
    -> caller must dispose/drop it

TryTake succeeds
    -> future consumer/supervisor owns prepared job

dispatcher DisposeAsync
    -> dispatcher stops
    -> queued jobs are drained and disposed
```

This makes queue saturation a resource-ownership event rather than an
exceptional authoritative failure.

## Bounded dispatch

The dispatcher constructor requires an explicit positive capacity.

`TryDispatch` never waits for capacity.

Producer outcomes are:

```text
Enqueued
QueueFull
Stopped
```

There is deliberately no `WriteAsync`/wait-for-capacity producer path in V1.

The dispatcher implementation currently contains no background task. Its
consumer-side `TryTake` is also non-blocking and exists only as the seam for the
next supervisor increment.

## Disabled profile

This increment creates no dispatcher automatically.

Future composition must preserve:

```text
Disabled
    -> no snapshot
    -> no request.json
    -> no dispatcher instance
    -> no queue
    -> no background task
    -> no worker
```

## Failure boundary

Snapshot/request preparation can throw because those are deterministic local
primitives.

The future Dual Run submission coordinator is responsible for translating:

```text
snapshot failure
request materialization failure
QueueFull
Stopped
```

into non-authoritative dispatch telemetry plus cleanup.

None of these outcomes may change the authoritative document result.

## Still prohibited

At this checkpoint:

```text
Process.Start
ProcessStartInfo
worker executable
background queue consumer
unbounded queue
DocumentProcessor -> DualRun worker implementation
```

The next increment should add the parent-side best-effort submission coordinator
and profile snapshot/selection seam before process supervision.
