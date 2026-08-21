# Phase 16.3 — Organization A: Authoritative / Planning / Dual Run code layout

> **Historical implementation record.** Statements are relative to the
> baseline below. See [Current architecture](current-architecture.md) for active
> repository invariants.

**Baseline:** `c12aa64`
**Behavioral intent:** no runtime behavior change
**Purpose:** make the architecture discoverable from the filesystem and Rider project tree before introducing the production Dual Run V1 runtime.

## Physical organization

`DocumentProcessing.Engine/Orchestration` remains focused on the current authoritative production orchestration.

Shared deterministic planning implementations move physically to:

```text
DocumentProcessing.Engine/Planning/
```

The existing non-authoritative in-process comparison runtime moves physically to:

```text
DocumentProcessing.Engine/DualRun/InProcess/
```

`InProcess` is deliberately explicit: this runtime is transitional. The Phase 16.3 production target is an isolated Dual Run worker. V1 must not end with both the old in-process shadow runtime and the isolated worker as overlapping permanent mechanisms.

Unit tests mirror the same physical separation:

```text
DocumentProcessing.UnitTests/Planning/
DocumentProcessing.UnitTests/DualRun/InProcess/
```

## Namespace scope

This increment intentionally keeps the existing C# namespaces unchanged.

That constraint keeps Organization A a physical/code-readability refactor with no public API or dependency-surface rename. Namespace alignment and broader `Legacy` / `Shadow` terminology cleanup belong to the next behavior-neutral organization increment, after this filesystem boundary is accepted.

## Region convention

Production classes touched by this increment use the Rider readability convention:

```text
#region Variables and Constants
#region ctor
#region Methods
```

Classes with substantial method volume use specialized groups such as:

```text
#region Methods Planning
#region Methods Validation
#region Methods Execution
#region Methods Comparison
#region Methods Telemetry
```

Additional regions such as `Properties` or `Internal Types` are allowed where they improve navigation.

Regions are for eagle-eye navigation only; they do not replace architectural decomposition.

## Architectural classification

```text
Orchestration/
  authoritative production orchestration

Planning/
  deterministic logic shared by authoritative and Dual Run paths

DualRun/InProcess/
  current non-authoritative in-process comparison runtime
  transitional until the isolated Dual Run worker is proven
```

`GuardedDocumentPageExecutionPlanner` is classified under `Planning`, not `DualRun`, because its deterministic logic is now consumed by both authoritative visual planning and non-authoritative comparison.

## Acceptance

Organization A is accepted only if:

```text
git diff --check                    PASS
Release build -warnaserror          PASS
focused planning/Dual Run tests     PASS
full solution tests                 PASS
runtime behavior                    unchanged by design
commit                              NO until review
```
