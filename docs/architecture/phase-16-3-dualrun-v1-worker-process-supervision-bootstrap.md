# Phase 16.3 — Dual Run V1 worker process supervision bootstrap

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `371b395`

## Scope

This increment creates the first real out-of-process Dual Run boundary.

It adds:

```text
DocumentProcessing.DualRunWorker executable
parent-side one-job process supervisor
strict job bootstrap validation
atomic result.json materialization
process launch / exit / timeout / kill-tree handling
strict parent result validation
actual integration tests that cross the process boundary
```

It still does **not** add:

```text
background queue consumer
DocumentProcessor integration
PlanningOnly candidate planning implementation
Full candidate execution
PP-StructureV3/PaddleOCR worker composition
cgroup/container resource enforcement
```

## Project dependency direction

The new executable is a leaf:

```text
DocumentProcessing.DualRunWorker
    -> DocumentProcessing.Core
    -> DocumentProcessing.Engine
    -> DocumentProcessing.Pdf
```

No Core/Engine/Pdf project references the worker.

`DocumentProcessing.IntegrationTests` carries a build-only project reference to
the worker so direct integration-test builds produce the child executable.

## Process launch

The supervisor launches one trusted configured executable directly:

```text
UseShellExecute = false
ArgumentList:
    --job-directory <absolute private job directory>
    --max-request-bytes <explicit configured boundary>
```

No shell command string is constructed.

The supervisor redirects and continuously drains both stdout and stderr so a
child cannot deadlock by filling a redirected pipe.

Only stderr is retained, and it is capped by an explicit configuration value.
Stdout is drained and discarded.

## Timeout

There is deliberately no default process timeout.

`DocumentDualRunWorkerProcessConfiguration.Timeout` is nullable and must be
chosen by deployment/Phase 16.4 evidence.

When an explicit timeout fires, or when the supervisor lifecycle token is
cancelled, the supervisor attempts:

```text
Process.Kill(entireProcessTree: true)
```

and waits only for the separately configured termination grace period.

The returned parent-side evidence records whether kill was attempted and
whether process termination was confirmed.

## Worker bootstrap

The worker currently implements only the process/protocol bootstrap:

```text
validate absolute job directory
reject symbolic-link job directory
load bounded request.json
strict transport deserialization
validate request source path == local source.bin
reject symbolic-link request/source files
validate source byte length
stream SHA-256 validation
```

After successful validation it writes a strict worker result with:

```text
status = Failed
failure stage = Planning
exception type = PlanningNotImplemented
```

This is intentional. It proves the complete process/protocol path without
pretending that PlanningOnly execution already exists.

If source identity is wrong, the worker writes a structured
`SourceValidation` failure.

If no trusted request can be established, the worker exits non-zero and emits
only bounded/generic stderr information.

## result.json

The worker writes:

```text
result.json.partial
    -> complete strict JSON
    -> atomic rename
    -> result.json
```

On Unix the result file is created user read/write only.

The parent accepts a zero exit code only when:

```text
result.json exists
result.json.partial does not exist
result file is not a symlink
result size is within explicit configured boundary
strict V1 result deserialization succeeds
result.JobId == request.JobId
result.ExecutionMode == request.ExecutionMode
result.SourceDocumentSha256 == request.SourceDocumentSha256
```

A zero exit without a result is `MissingResult`.
Malformed or mismatched result evidence is `InvalidResult`.
A non-zero exit is `NonZeroExit`.

## Ownership

`RunAsync` takes ownership of the supplied `DocumentDualRunPreparedJob` for the
entire process lifetime.

Every outcome disposes the job directory after the parent has materialized the
process/result evidence:

```text
launch failure
timeout
supervisor cancellation
non-zero exit
missing result
invalid result
structured worker result
```

The future queue consumer will therefore transfer ownership:

```text
dispatcher.TryTake
    -> process supervisor RunAsync
    -> supervisor disposes local job after evidence capture
```

## Security / production gap

A process boundary protects the authoritative process from ordinary worker
crashes and unhandled worker exceptions.

This checkpoint does **not** yet satisfy the frozen resource-isolation
requirement for memory/CPU. Before production Dual Run integration, the worker
and worker-owned ML processes still require an OS-level cgroup/container/service
boundary.

The worker currently inherits the parent process environment. Environment
minimization is also unresolved and should be addressed with the resource
boundary rather than hidden behind application exception handling.

## Next increment

Implement real `PlanningOnly` worker composition and parity:

```text
native PDF extraction
native normalization
visual raster observation
structural evidence enrichment
guarded page planning
compact comparison against authoritative baselines
strict Completed result.json
```

Only after PlanningOnly parity and process-failure isolation evidence should
Full candidate execution or `DocumentProcessor` cutover be attempted.
