# Phase 16.3 — Active-code historical label cleanup

**Baseline:** `3ce4a9e`

## Decision

Historical deep phase labels such as `H.4D.1`, `H.4C`, and `21E.1H.*`
must not remain as the vocabulary of active runtime contracts.

The active C# comments now describe durable semantics instead:

- authoritative versus candidate execution;
- NativeText-only versus OCR-capable candidate composition;
- guarded planning and explicit future cutover;
- frozen regression policy and blind-holdout validation;
- deterministic visual evidence and raster measurement.

Historical provenance remains available in Git history and permanent
evaluation/architecture documentation. Removing deep phase labels from active
comments does not remove that evidence.

Residual standalone `Controlled` wording in the Dual Run candidate source and
its direct tests is aligned with the current `Dual Run candidate` vocabulary.

This increment changes comments, diagnostic text, test names, and test-helper
parameter names only. It does not change runtime policy or execution logic.
