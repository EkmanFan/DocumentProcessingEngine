# Phase 16.2 — Authoritative Healthy-Native Visual Cutover

**Date:** 2026-08-18
**Baseline commit:** `4877565` — `fix: preserve large independent visuals`
**Canonical phase:** 16 — Performance / memory acceptance
**Scope of this checkpoint:** correctness remediation required before performance acceptance

## Problem demonstrated

The legacy authoritative page route coupled text trust and visual execution.

On the real Habermas mixed-content controls:

| Control | Native status | Legacy route | Human-confirmed meaningful visual |
|---|---|---|---|
| p40 | Healthy | NativeOnly | preserve |
| p43 | Healthy | NativeOnly | preserve |
| p44 | Healthy | NativeOnly | preserve |

Because `NativeOnly` performed no layout/visual execution, those meaningful visuals were absent from the authoritative result.

Changing the existing `0.60` raster-area threshold was rejected. That threshold belongs to native-text verification, not visual semantics. Lowering it would couple two independent decisions and would provoke unnecessary OCR/reconciliation.

## Architectural decision

Text execution and visual execution remain independent axes.

The authoritative cutover is deliberately narrow:

```text
legacy route = NativeOnly
native status = Healthy

candidate plan:
  TextMode = NativeText
  RequiresMeaningfulVisualPreservation = true
  RequiresVisualAnalysis = false
  RequiresTargetedOcr = false

=> authoritative layout-backed visual preservation
=> native PDF text remains authoritative
=> no OCR
```

Not cut over in this checkpoint:

```text
Healthy + AnalyzeVisual
candidate text-ML authority
legacy Recovery
legacy Reconciliation
```

The existing legacy Recovery/Reconciliation paths remain authoritative and unchanged.

## New production seam

The cutover adds:

```text
DocumentAuthoritativeVisualPlanningDependencies
DocumentAuthoritativeVisualPlanningRunner
HealthyNativeVisualPageExecutor
NativeLayoutVisualPageAssembler
```

and extends the existing hybrid dependency composition so the new capability is explicit and opt-in.

The execution path for the new branch is:

```text
source visual evidence
  -> deterministic guarded page plan
  -> full-page raster
  -> PP-StructureV3 layout
  -> semantic Figure assessment
  -> VisualEvidenceDispositionPolicy
  -> LayoutVisualRegionPreserver
  -> NativeLayoutVisualPageAssembler
  -> HybridDocumentAssembler
```

The merger preserves whole native blocks. Native block splitting was not introduced.

Fail-closed conditions include:

```text
ambiguous native/layout ownership
unmapped native block
native block straddling a semantic visual
duplicate/ambiguous layout order
source plan requests preservation but live layout produces no preservable Figure
live layout returns unresolved Figure evidence in the narrow Preserve-only branch
```

The existing `HybridDocumentAssembler` duplicate-order guard remains intact.

## Real-corpus evidence

### Healthy mixed pages — live merger proof

Using live PP-StructureV3 and no OCR:

```text
Habermas p40
  native blocks: 7
  visual: 1
  dense reading order: PASS
  native text unchanged: PASS
  native relative order: PASS
  visual custody: PASS
  crop: 1678x1928
  bytes: 798598
  SHA-256:
    bffd8d7fd27462ab530f50c483aa39bad7849653c81960d15e8cf73c8c829f03

Habermas p43
  native blocks: 3
  visual: 1
  dense reading order: PASS
  native text unchanged: PASS
  native relative order: PASS
  visual custody: PASS
  crop: 1702x1173
  bytes: 878872
  SHA-256:
    bf20176970e097a3d20d1142db44488f7aba4e7ca8e76c5c8bf7b6b799b813f2

Habermas p44
  native blocks: 1
  visual: 1
  dense reading order: PASS
  native text unchanged: PASS
  native relative order: PASS
  visual custody: PASS
  observed crop: 1767x1746
  observed bytes: 865904
  observed SHA-256:
    b7ea9fe5058bfa7363847934190f6553628a513e4750084b601c1e61c220f58f
```

The p44 crop is recorded as observed evidence only; it is not promoted here to a permanent exact-crop oracle.

### Public `DocumentProcessor.ProcessAsync` — Healthy visual authority

The real public orchestrator was executed on p40/p43/p44 with PP-StructureV3.

Results:

```text
PP layout calls: 1/page
OCR recognizer calls: 0
PaddleOCR service: never started

native selected-source text: preserved
every native block: preserved exactly once
native relative order: preserved
meaningful visual: preserved
hybrid reading order: dense / collision-free
raster/layout manifest custody: present
OCR manifest/evidence: absent
reconciliation manifest/evidence: absent
```

### Permanent semantic regression after cutover

The complete permanent suite passed:

```text
native/provenance: PASS
  fixture provenance: 67/67
  Habermas native controls: 3/3
  De Decretis native controls: 50/50
  De Decretis words: 29044/29044
  De Decretis blocks: 269/269

layout --all-pass: PASS
  controls: 7
  semantic PASS/FAIL: 7/0

real PP + PaddleOCR: PASS
  p233 Recovery: PASS
  p380 Conflict: PASS
  p405 Agreement: PASS
  Figure OCR: 0 across all three controls
```

### Public `DocumentProcessor.ProcessAsync` — legacy OCR paths

The legacy controls were also executed through the modified public orchestrator.

```text
Ehrman p233
  Native: Missing
  Route: LayoutWithTargetedOcrRecovery
  PP calls: 1
  OCR calls: 7
  Figure OCR: 0
  Healthy-only visual observer calls: 0
  exact visual:
    841x1398
    1505768 bytes
    c4170e36da6d0bfdec419f8db199ba972baf3075887a264aa2e9e4d46e6e4e77

Ehrman p380
  Native: Unverified
  Route: LayoutWithTargetedOcrReconciliation
  PP calls: 1
  OCR calls: 9
  Figure OCR: 0
  Healthy-only visual observer calls: 0
  targetSequence: 5
  Conflict / None
  resolved: false
  divergence: true
  nativeBlock: 2

Ehrman p405
  Native: Unverified
  Route: LayoutWithTargetedOcrReconciliation
  PP calls: 1
  OCR calls: 8
  Figure OCR: 0
  Healthy-only visual observer calls: 0
  targetSequence: 9
  Agreement / NativePdf
  resolved: true
  divergence: false
  nativeBlock: 6
```

PP-StructureV3 and PaddleOCR were never concurrently resident during these controls.

## Automated code regression

After the guarded cutover:

```text
Release build with warnings as errors: PASS
focused cutover/merger tests: 11/11 PASS
full regression: 558/558 PASS
git diff --check: PASS
```

## Invariants retained

```text
SourceVisualIndex != semantic visual occurrence
Figure != meaningful visual
Figure OCR = 0
model/layout output = evidence, not policy
native text remains authoritative on Healthy native pages
unknown/unresolved visual evidence fails closed
caption remains a text candidate
no source-centric visual materialization
shadow fail-open semantics unchanged
legacy Recovery/Reconciliation semantics unchanged
```

## Performance status

This checkpoint closes the semantic/correctness blocker discovered during Phase 16.

It does **not** establish a performance SLA or complete Phase 16.

The next step is to resume performance/memory acceptance on the now-correct authoritative `DocumentProcessor` path, keeping service startup separate and PP-StructureV3/PaddleOCR non-concurrent on the reference machine.
