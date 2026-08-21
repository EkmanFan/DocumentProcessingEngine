# Phase 16.3 — Dual Run V1 source-snapshot custody

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `a85da24`

## Scope

This increment implements custody for the worker's immutable `source.bin`.

It does **not** wire `DocumentProcessor`, create a queue, write `request.json`,
or launch a worker process.

## Lifecycle

`DocumentDualRunSourceSnapshotFactory` is a composition object only.
Construction performs no file-system I/O.

Actual `CreateAsync` performs:

```text
authoritative prepared source
    -> read from byte zero when seekable
    -> restore original source position
    -> private job directory
    -> source.bin.partial
    -> SHA-256 + byte-length verification
    -> rename to source.bin
    -> DocumentDualRunSourceSnapshot ownership
```

The expected SHA-256 and byte length come from the already-established
authoritative source identity.

A mismatch fails snapshot creation. The future dispatcher must treat that
failure as Dual Run loss only; it must not affect the authoritative result.

## Private spool

The configured spool root must be an absolute path.

The factory constructor does not create the root. This is intentional:
`Disabled` composition must not create Dual Run spool state.

When a snapshot is actually requested, the factory creates:

```text
<spool-root>/
    job-<job-id>-<random>/
        source.bin
```

On Unix:

```text
job directory   user rwx only
source file     user rw only
```

The random suffix prevents deterministic reuse of a stale job directory.
The job ID remains visible for diagnostics.

The worker request will later point at the fully-qualified `source.bin`.

## Copy and identity verification

The source snapshot is independent from caller stream lifetime and from
`DocumentProcessor.PreparedDocumentSource`.

For seekable streams the factory:

```text
captures original position
seeks to byte zero
copies and hashes
restores original position
```

For non-seekable streams it copies the readable bytes from the current position.
The expected SHA-256 and byte-length comparison therefore fails closed if those
bytes are not the complete authoritative source.

The file is first written as:

```text
source.bin.partial
```

Only after SHA-256 and byte length agree with the authoritative identity is it
renamed to:

```text
source.bin
```

No worker should observe a partially materialized `source.bin`.

## Failure cleanup

Creation failure removes the private job directory best-effort, including:

```text
cancellation
source read failure
destination write failure
byte-length mismatch
SHA-256 mismatch
final rename failure
source-position restoration failure
```

`DocumentDualRunSourceSnapshot` owns the successful job directory.

Its `DisposeAsync` performs idempotent best-effort recursive cleanup. Cleanup
failure is deliberately non-authoritative; future supervisor telemetry will
record such failures without invalidating the authoritative result.

## Next boundary

The next increment may consume this custody object to build a versioned
`request.json` and enqueue a bounded background job.

Still prohibited at this checkpoint:

```text
DocumentProcessor -> Process.Start
DocumentProcessor -> worker implementation
worker launch
ML service launch
unbounded queue
```
