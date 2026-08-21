# Phase 16.3 — Dual Run candidate text naming

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `029bc84`

## Decision

All eleven `ControlledCandidate...` declarations are renamed.

```text
ControlledCandidate -> DualRunCandidate
```

Classification:

```text
RENAME  11
KEEP     0
REMOVE   0
```

## Rationale

`ControlledCandidate` was historical scaffolding terminology. It no longer
describes a distinct architectural boundary: these contracts and runners are
the candidate-side text execution portion of the Dual Run mechanism.

`Candidate` remains intentional vocabulary. The candidate is the alternative
logic/result evaluated against the authoritative path. Properties such as
`CandidateTextMode`, `CandidatePage`, and candidate-vs-authoritative agreement
therefore remain candidate-oriented.

`DualRunCandidate` makes both concepts explicit:

- `DualRun` identifies the non-authoritative evaluation mechanism.
- `Candidate` identifies the alternative side being executed or compared.

This increment is naming-only. It does not promote candidate execution to
authoritative execution and does not change failure-isolation behavior.

The current implementation remains physically under `DualRun/InProcess` and
is transitional until the isolated out-of-process worker boundary is introduced.

Historical phase labels are not removed by this increment. They are reviewed
separately so provenance comments are not accidentally erased while renaming
runtime contracts.
