# Manager processing progress V1

Status: implemented on 2026-09-02 as `MGR-OPS-01`.

## Purpose

The Manager previously exposed only the durable `Active` queue status. Long
executions therefore looked stationary even while DPEngine was making progress.
The active workshop card now exposes real pipeline completion and the current
stage without claiming to estimate remaining wall-clock time.

## Contract

DPEngine accepts an optional request-scoped synchronous progress observer. Each
observation contains:

- a format-neutral pipeline stage;
- a monotonic integer completion percentage;
- optional completed and total source-unit counts.

Paged processing reports physical pages. Structured processing reports native
content units and, when applicable, selected visuals. The stages are:

```text
preparing source
inspecting format and native evidence
planning
analyzing selected content
processing pages or native content units
assembling the portable result
```

The Manager maps DPEngine completion into its larger execution lifecycle and
adds source loading, canonical result storage and durable result publication.
The displayed percentage is pipeline completion, not elapsed time, throughput
or an ETA.

## Runtime ownership

Progress is a process-local latest-value observation keyed by the active
processing-unit ID. It is deliberately not written to PostgreSQL:

- progress is operational telemetry, not custody or audit evidence;
- the current Manager executes strictly one unit at a time;
- a process failure interrupts the execution and lease recovery requeues it;
- persisting every page update would create write amplification and queue
  contention without improving recovery semantics.

The durable queue remains authoritative for `Pending`, `Active`, `Succeeded`
and `Failed`. A new attempt starts again from source loading. Late decreasing
observations from one attempt are ignored.

## API and UI

The administrative queue response contains an optional progress object only
for the active unit. Older or restarting Hosts may temporarily expose an active
unit without progress; the UI then shows source loading at the beginning of the
bar.

The existing active-document card is the progress bar. Its accessible
`progressbar` role exposes values from zero through one hundred. The current
stage is shown above the document title, with `completed/total` when the active
stage reports countable source units. Reduced-motion preferences disable the
fill animation.

## Failure and security boundaries

Progress never authorizes queue transitions and cannot mark a unit complete.
Only the fenced dispatcher and durable result registration retain those
responsibilities. The consumer publication API is unchanged; Apologia Studio
observes only completed, durably published results.

## Acceptance evidence

- unit tests prove that DPEngine observations are monotonic and include real
  page counts;
- workshop projection tests preserve stage, percentage and source-unit counts;
- the full solution builds without warnings;
- an isolated PostgreSQL/custody run completed synthetic 50-, 600- and
  10,000-page native PDFs without changing the real Manager queue;
- the idle workshop remains unchanged and the active card is exposed as an
  accessible progress bar during execution.
