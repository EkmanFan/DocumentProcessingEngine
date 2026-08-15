# Phase 21E.1H.3B — Independent page execution plan V1

## Status

Production execution-plan model and pure requirements compiler.

This increment introduces a plan capable of representing text and visual work
independently.

It deliberately does **not** replace or modify:

- `DocumentPageProcessingPlanner`;
- `DefaultPageProcessingPolicy`;
- `PageProcessingRoute`;
- `PageProcessingPlan`;
- `DocumentProcessor`;
- layout/OCR execution code.

Current runtime behavior therefore remains unchanged.

---

## 1. Problem with the legacy atomic route

The legacy V1 plan stores exactly one `PageProcessingRoute`.

Its convenience flags derive the whole execution chain from that route:

```text
NativeOnly
    -> no raster
    -> no layout
    -> no OCR

LayoutWithTargetedOcrRecovery
    -> raster
    -> layout
    -> OCR

LayoutWithTargetedOcrReconciliation
    -> raster
    -> layout
    -> OCR
    -> reconciliation
```

That model prevents contradictory execution combinations, which was useful,
but it also couples all visual work to OCR.

Phase 21E.1H.3A can now produce valid requirements such as:

```text
UseNativeText
+
PreserveMeaningfulVisual
```

and:

```text
UseNativeText
+
RequiresVisualAnalysis
```

The first should not require layout or OCR.

The second may require raster/layout for the visual while still requiring
**no OCR** because native text is already authoritative.

The old atomic route cannot express either case faithfully.

---

## 2. New execution model

The new plan is:

```text
PageExecutionPlan
  ├── PhysicalPageNumber
  ├── TextExecutionMode
  └── VisualElementExecutionPlan[]
```

The text modes are closed engine mechanisms:

```text
NativeText
TargetedOcrRecovery
TargetedOcrVerification
TargetedOcrReconciliation
```

Each real source visual independently receives:

```text
NoAdditionalSemanticProcessing
PreserveMeaningfulVisual
AnalyzeVisual
```

A page with no visuals uses an empty visual-plan collection.

---

## 3. Requirements compiler

`DefaultPageExecutionPlanCompiler` is a pure deterministic compiler:

```text
PageProcessingRequirements
        ↓
DefaultPageExecutionPlanCompiler
        ↓
PageExecutionPlan
```

It performs no I/O.

Text mapping:

```text
UseNativeText
    -> NativeText

RecoverMissingNativeText
    -> TargetedOcrRecovery

VerifyNativeText
    -> TargetedOcrVerification

ReconcileCorruptedNativeText
    -> TargetedOcrReconciliation
```

Visual mapping:

```text
PresentationOnly
    -> NoAdditionalSemanticProcessing

PreserveMeaningfulVisual
    -> PreserveMeaningfulVisual

RequiresVisualAnalysis
    -> AnalyzeVisual
```

`NoVisual` is invalid for a real `VisualElementDisposition` and therefore has
no normal compilation path.

---

## 4. Derived prerequisites instead of a mutable bag of booleans

The plan does not accept arbitrary flags such as:

```text
RequiresRasterization = false
RequiresLayoutAnalysis = false
RequiresTargetedOcr = true
```

That would allow impossible combinations.

Instead, prerequisites are derived from the closed text/visual execution modes.

### Rasterization

Required when:

```text
text mode is OCR-backed
OR
any visual action is AnalyzeVisual
```

### Layout analysis

Required under the same V1 conditions:

```text
text mode is OCR-backed
OR
any visual action is AnalyzeVisual
```

### Targeted OCR

Required **only** by the text axis:

```text
TargetedOcrRecovery
TargetedOcrVerification
TargetedOcrReconciliation
```

`AnalyzeVisual` alone does not authorize OCR.

This is the key independence guarantee.

### Native/OCR reconciliation

Required for:

```text
TargetedOcrVerification
TargetedOcrReconciliation
```

but not:

```text
TargetedOcrRecovery
```

because missing-text recovery has no authoritative native text to reconcile.

---

## 5. Key combinations

### Trusted native text + presentation-only visual

```text
NativeText
+
NoAdditionalSemanticProcessing

raster = false
layout = false
OCR = false
reconciliation = false
```

This is the expected fast path for the presentation-only visual classes
validated in Phase 21E.

### Trusted native text + meaningful embedded visual

```text
NativeText
+
PreserveMeaningfulVisual

raster = false
layout = false
OCR = false
reconciliation = false
preserve meaningful visual = true
```

Preserving an already-identified embedded source visual does not justify ML or
OCR by itself.

### Trusted native text + unresolved visual

```text
NativeText
+
AnalyzeVisual

raster = true
layout = true
OCR = false
reconciliation = false
```

This combination was impossible to express with the legacy route model.

It is also a critical safety property: uncertainty about a visual does not
invent uncertainty about already-trusted native text.

### Missing native text

```text
TargetedOcrRecovery
```

always keeps:

```text
raster = true
layout = true
OCR = true
reconciliation = false
```

Visual actions remain independent.

### Native text requiring verification

```text
TargetedOcrVerification
```

keeps the current secondary-evidence mechanism:

```text
raster = true
layout = true
OCR = true
native/OCR reconciliation = true
```

### Corrupted native text

```text
TargetedOcrReconciliation
```

keeps:

```text
raster = true
layout = true
OCR = true
native/OCR reconciliation = true
```

---

## 6. Meaning of no additional semantic processing

`NoAdditionalSemanticProcessing` is intentionally not named `Discard`.

It means the visual requires no further semantic document-understanding work
under the current evidence.

It does not authorize:

- deletion of source bytes;
- loss of source-fidelity assets;
- deletion of semantic native text contained by a visual frame;
- consumer-specific archival decisions.

This preserves the distinction established in the Phase 21E human review.

---

## 7. Meaningful visual preservation

`PreserveMeaningfulVisual` is an explicit independent action.

It does not imply:

```text
rasterization
layout analysis
OCR
```

when the meaningful visual has already been identified from deterministic
source evidence.

The execution layer may copy/reference/preserve the source visual through the
existing provenance/visual custody mechanisms when H.3C wires this plan into
runtime execution.

---

## 8. Unresolved visual analysis

`AnalyzeVisual` means classification is not yet safely resolved.

In V1 it derives:

```text
RequiresRasterization = true
RequiresLayoutAnalysis = true
```

but:

```text
RequiresTargetedOcr = false
```

unless the **text mode independently requires OCR**.

The source visual must remain available until analysis produces a safe
disposition.

---

## 9. Regression strategy

The unit tests cover the Cartesian product of:

```text
4 text requirements
x
3 dispositions valid for an existing visual
=
12 requirement/action combinations
```

Additional tests verify:

- `NativeText + AnalyzeVisual` -> layout without OCR;
- `NativeText + PreserveMeaningfulVisual` -> preservation without ML/OCR;
- `NativeText + PresentationOnly` -> no additional semantic work;
- recovery -> OCR without native/OCR reconciliation;
- verification -> OCR plus reconciliation;
- corruption reconciliation -> OCR plus reconciliation;
- mixed per-element visual actions;
- pages with no visuals;
- immutable collection snapshots;
- duplicate source visual rejection;
- invalid enum rejection;
- absence of legacy `PageProcessingRoute` / `PageProcessingPlan` from the new
  execution contracts.

---

## 10. Why H.3B still does not rewire the planner

The independent plan is now representationally sufficient, but the production
planner still begins from the old `PageProcessingAssessment`.

The new chain requires:

```text
native text assessment
        ↓
TextAuthority

source visual measurements
        ↓
VisualEvidenceObservation[]
        ↓
VisualElementEvidence[]
        ↓
PageProcessingEvidence
        ↓
PageProcessingRequirements
        ↓
PageExecutionPlan
```

H.3B intentionally does not pretend that this upstream visual-measurement
composition already exists in production.

The next integration step must wire that chain without bypassing the existing
Missing/Corrupted safeguards and without introducing unnecessary layout/OCR.

Only then should runtime execution stop consuming the legacy atomic plan.

---

## 11. Next boundary

Phase 21E.1H.3C should integrate the new planning chain while preserving the
legacy execution path as a regression reference until parity/safety is proven.

The integration must prove at minimum:

```text
Missing
    -> OCR recovery still executed

Corrupted
    -> OCR reconciliation still executed

Trusted + presentation-only
    -> no layout/OCR

Trusted + meaningful visual
    -> preserve without OCR

Trusted + unknown visual
    -> visual analysis without OCR
```

The old route model should be removed only after those behaviors are exercised
end to end.
