# Phase 16.3 — Organization B: namespace alignment

**Baseline:** `b96167f`
**Behavioral intent:** no runtime behavior change
**Purpose:** align C# namespaces with the physical Planning and DualRun/InProcess boundaries introduced by Organization A.

## Namespace alignment

```text
src/DocumentProcessing.Engine/Planning/
  -> DocumentProcessing.Engine.Planning

src/DocumentProcessing.Engine/DualRun/InProcess/
  -> DocumentProcessing.Engine.DualRun.InProcess

tests/DocumentProcessing.UnitTests/Planning/
  -> DocumentProcessing.UnitTests.Planning

tests/DocumentProcessing.UnitTests/DualRun/InProcess/
  -> DocumentProcessing.UnitTests.DualRun.InProcess
```

References from authoritative orchestration, hybrid execution, tests, and tooling receive explicit namespace imports only where required.

## Scope

This increment changes namespace/import organization only.

It intentionally does **not** yet rename:

```text
Shadow -> DualRun
Legacy -> Authoritative
ControlledCandidate -> DualRunCandidate
```

Those terminology/API renames remain a separate behavior-neutral Organization B change so namespace movement and identifier renaming do not become one large diff.

## Rider region convention

Production classes under the moved Engine folders must retain:

```text
#region Variables and Constants
#region ctor
#region Methods...
```

Large classes retain specialized method regions.

## Acceptance

```text
exact b96167f clean baseline          PASS
physical folder counts               PASS
namespace alignment                  PASS
executable/member fingerprint        unchanged
git diff --check                     PASS
Release build -warnaserror           PASS
focused Planning/Dual Run tests      PASS
full solution tests                  PASS
commit                               NO until review
```
