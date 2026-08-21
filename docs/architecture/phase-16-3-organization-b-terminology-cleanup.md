# Phase 16.3 — Organization B terminology cleanup

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `ecbe497`

**Behavioral intent:** terminology, naming, and formatting cleanup only.

## Canonical runtime terminology

```text
Authoritative
  current production path that determines the returned result

Dual Run
  non-authoritative comparison/evaluation path

Candidate
  alternative plan/output evaluated inside Dual Run
```

`Legacy` remains valid only where it genuinely denotes historical or
compatibility behavior. Historical orchestration documents are not renamed.
