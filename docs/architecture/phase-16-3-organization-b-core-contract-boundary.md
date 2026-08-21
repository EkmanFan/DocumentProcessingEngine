# Phase 16.3 — Organization B Core contract boundary

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `2be116e`

**Behavioral intent:** physical organization and namespace alignment only.

## Final V1 Core boundary

```text
DocumentProcessing.Core
├── Planning        23 contracts
├── DualRun         12 contracts
└── Orchestration   10 contracts
```

### Planning

Contains the shared page-planning vocabulary used by authoritative execution
and by non-authoritative Dual Run evaluation. This includes the current
route-based model, the two-axis requirements/execution model,
`GuardedPagePlanningDecision`, and `LayoutVisualEvidence`.

`LayoutVisualEvidence` belongs to Planning because it is semantic visual
evidence produced after observation/assessment and carries `VisualEvidenceKind`;
it is not a raw raster/observation transport contract.

### DualRun

Contains only non-authoritative comparison, reporting, failure/status, and
observer contracts. DualRun may depend on Planning; Planning must not depend
on DualRun.

### Orchestration

Contains document/raster observation integration contracts. These contracts
remain policy-neutral and do not depend on Planning or DualRun in executable
code.

## Dependency invariants

```text
Core.Planning -> Core.DualRun = 0
Core.Planning -> Core.Orchestration = 0
Core.Orchestration -> Core.Planning = 0
Core.Orchestration -> Core.DualRun = 0
Core.DualRun -> Core.Orchestration = 0
Core.DualRun -> Core.Planning = allowed
```

## Test organization

`PageProcessingEvidenceContractTests` and `PageProcessingPolicyContractTests`
move from `UnitTests/Orchestration` to `UnitTests/Planning` because their
subjects are Planning contracts.

`DocumentProcessorHybridRoutingTests` remains under Orchestration because it
tests `DocumentProcessor` route integration. The older
`Orchestration/MissingNativeHybridPageExecutorTests` is deliberately retained
for now; hybrid-test consolidation is a separate organization concern.

No runtime policy or execution behavior is intentionally changed by this split.
