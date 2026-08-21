# Phase 16.3 — Dual Run V1 best-effort submission

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `b896a89`

## Purpose

This increment creates the parent-side seam that turns one immutable,
per-document Dual Run profile snapshot into a best-effort local job submission.

It still does **not** modify `DocumentProcessor`, start a background consumer, or
launch a worker process.

## Configuration snapshot versus selection

The configured values are captured once in:

```text
DocumentDualRunProfileSnapshot
    Profile
    SampledBasisPoints
```

This snapshot is immutable.

The source SHA-256 is already computed by authoritative source preparation.
Once that identity is available:

```text
profile snapshot
    + source SHA-256
    -> deterministic DocumentDualRunSelection
```

Configuration is not reread during selection.

## Disabled and unselected cost boundary

Selection happens before any selected-only work.

For:

```text
Disabled
Sampled + not selected
```

the coordinator does **not**:

```text
resolve/create a dispatcher
invoke the selected-submission factory
compute authoritative transport fingerprints
create source.bin
create request.json
create a queue
launch a background task
launch a worker
```

The future `DocumentProcessor` integration must preserve this order.

## Selected-only envelope

`DocumentDualRunSelectedSubmission` is intentionally constructed lazily after
selection.

It carries:

```text
repeatably readable prepared source
authoritative source SHA-256
authoritative source byte length
document format
engine version
authoritative page baselines
```

The future integration should derive page baselines from the final
authoritative decisions/pages only in this selected branch.

For a non-seekable caller source, the existing authoritative prepared-source
layer already provides a repeatably readable temporary stream. The submission
coordinator must receive that prepared stream, not the original consumed stream.

## Best-effort stages

For selected documents:

```text
resolve dispatcher
    -> build selected envelope
    -> materialize source.bin
    -> construct + materialize request.json
    -> TryDispatch
```

Ordinary failures are converted to a non-authoritative `Failed` result with one
of:

```text
DispatcherResolution
SelectedSubmissionCreation
SourceSnapshot
RequestPreparation
Dispatch
```

Pre-cancelled submission returns `Cancelled`.

`OutOfMemoryException` is deliberately not swallowed in the parent process.
The source copy is streaming and bounded, but true resource exhaustion is not
made safe by try/catch. Worker memory isolation remains the process-boundary
responsibility.

## Ownership after dispatch

```text
TryDispatch == Enqueued
    -> dispatcher owns prepared job

TryDispatch == QueueFull
    -> coordinator disposes rejected prepared job

TryDispatch == Stopped
    -> coordinator disposes rejected prepared job

TryDispatch throws
    -> coordinator disposes prepared job
```

This increment therefore closes the local preparation/dispatch ownership loop.

## Current cancellation boundary

The coordinator accepts a submission cancellation token.

Future `DocumentProcessor` integration must obey the frozen authority rule:

```text
while authoritative work is active
    caller cancellation remains authoritative

after authoritative result is complete
    Dual Run worker lifecycle is detached from caller cancellation
```

The exact integration point is intentionally deferred until the old in-process
Dual Run path is removed.

## Still prohibited

At this checkpoint:

```text
DocumentProcessor -> SubmissionCoordinator
background queue consumer
Process.Start
ProcessStartInfo
worker executable
PP/Paddle launch from parent submission
```

The next increment is process supervision / worker executable planning, after
the parent-side profile and submission seam is reviewed.
