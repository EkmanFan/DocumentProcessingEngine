# Phase 21E.1H.3C — Guarded shadow page planner V1

## Status

Additive planner integration only.

This increment composes the complete two-axis planning chain beside the legacy
route planner while keeping runtime execution on the legacy path.

`DocumentProcessor` is deliberately untouched.

---

## 1. Why this is a shadow integration

The new planning layers now exist independently:

```text
native assessment
    -> TextAuthority

visual observation
    -> VisualEvidenceKind

TextAuthority + VisualEvidenceKind
    -> PageProcessingRequirements

PageProcessingRequirements
    -> PageExecutionPlan
```

What did not yet exist was one production composition root proving that these
layers work together on page-aligned document input.

Directly replacing the current `DocumentPageProcessingPlanner` would still be
premature because production does not yet have the upstream component that
creates the validated `VisualEvidenceObservation` values from source PDFs.

H.3C therefore introduces:

```text
GuardedDocumentPageExecutionPlanner
```

which requires those observations explicitly and produces both:

```text
legacy PageProcessingDecision
+
candidate PageExecutionPlanningDecision
```

The candidate is observable and testable but is not executed.

---

## 2. Complete visual coverage is mandatory

The guarded planner does not accept partial visual evidence.

For each extraction page:

```text
PageVisualEvidenceObservations.PhysicalPageNumber
    ==
DocumentExtractionPage.PhysicalPageNumber
```

and:

```text
visual observation count
    ==
DocumentExtractionPage.RasterImageCount
```

and source visual indexes must cover exactly:

```text
0 .. RasterImageCount - 1
```

once each.

This is intentionally stricter than silently treating absent observations as
presentation-only or as "no visual".

Missing evidence is an integration failure.

The later production measurement stage may choose to emit an explicit
`VisualForegroundState.Unavailable`, which correctly becomes
`VisualEvidenceKind.Unknown`, but it may not omit the source visual occurrence.

---

## 3. Candidate planning trace

`PageExecutionPlanningDecision` retains the complete explanation chain:

```text
PageProcessingAssessment
PageProcessingEvidence
PageProcessingRequirements
PageExecutionPlan
```

All four artifacts must refer to the same physical page.

This is useful during shadow validation because a changed execution plan can be
traced back to:

```text
native status
visual evidence class
policy disposition
execution compilation
```

without reconstructing hidden reasoning.

---

## 4. Legacy/candidate pair

`GuardedPagePlanningDecision` stores:

```text
Legacy
Candidate
```

for the same physical page.

Two diagnostic properties are exposed.

### CandidateRemovesLegacyTextMl

True when:

```text
legacy route != NativeOnly
AND
candidate TextMode == NativeText
```

This identifies the intended Phase 21E optimization on pages whose old route
performed layout/OCR only because raster geometry made native text
`Unverified`.

It is a diagnostic signal only in H.3C.

### CandidateHasIndependentVisualWork

True when the candidate independently requires:

```text
AnalyzeVisual
OR
PreserveMeaningfulVisual
```

This exposes work the legacy atomic route model could not represent cleanly.

---

## 5. Text-safety guard

Before a candidate result can leave the guarded planner, its text execution mode
must satisfy the existing native-text status.

```text
Healthy
    -> NativeText

Missing
    -> TargetedOcrRecovery

Suspicious
    -> TargetedOcrReconciliation

Unverified
    -> NativeText
       OR TargetedOcrVerification
```

Anything else is rejected as an integration error.

This means visual policy can never weaken:

```text
Missing
Suspicious
```

text safety.

For `Unverified`, the two allowed outcomes encode the intended policy:

```text
resolved visual evidence
    -> NativeText

unresolved visual evidence
    -> TargetedOcrVerification
```

---

## 6. Important representative behaviors

### Missing text

```text
legacy:
    LayoutWithTargetedOcrRecovery

candidate:
    TargetedOcrRecovery
```

No optimization is allowed.

### Suspicious text

```text
legacy:
    LayoutWithTargetedOcrReconciliation

candidate:
    TargetedOcrReconciliation
```

No optimization is allowed.

### Unverified + presentation-only visual

```text
legacy:
    LayoutWithTargetedOcrReconciliation

candidate:
    NativeText
    NoAdditionalSemanticProcessing
```

This is the main intended cost reduction.

### Unverified + unknown visual

```text
legacy:
    LayoutWithTargetedOcrReconciliation

candidate:
    TargetedOcrVerification
    AnalyzeVisual
```

The candidate remains conservative.

### Healthy + meaningful visual

```text
legacy:
    NativeOnly

candidate:
    NativeText
    PreserveMeaningfulVisual
```

This closes the Habermas mixed-page blind spot without inventing OCR.

### Healthy + unknown visual

```text
legacy:
    NativeOnly

candidate:
    NativeText
    AnalyzeVisual
```

The visual requires raster/layout analysis, but trusted native text does not
therefore require OCR.

---

## 7. Runtime remains unchanged

This increment does not modify:

```text
DocumentPageProcessingPlanner
DocumentProcessor
DefaultPageProcessingPolicy
PageProcessingRoute
PageProcessingPlan
hybrid page executors
```

`DocumentProcessor` continues to execute only the legacy
`PageProcessingDecision`.

This is intentional.

A shadow planner is only useful if a divergence cannot accidentally change
runtime behavior before the candidate has been exercised against real corpus
evidence.

---

## 8. What H.3C proves

H.3C proves in production code that:

1. native assessment can feed `TextAuthority`;
2. complete per-visual observations can feed the deterministic visual assessor;
3. the resulting evidence can feed the two-axis policy;
4. the requirements can compile to the independent execution plan;
5. legacy and candidate plans can be retained side-by-side;
6. Missing/Suspicious text safety is guarded explicitly;
7. incomplete visual coverage is rejected rather than guessed.

It does **not** prove that production can yet generate those visual observations.

---

## 9. Next boundary

The next step should be the production visual-observation source.

It must turn actual PDF/source evidence into the signals already validated in
Phase 21E:

```text
foreground state / ratio
pixel-native-word interaction
effective visual bounds
significant component count
heading association
native-text containment
caption association
```

without embedding policy decisions.

Only after that component is available should `DocumentProcessor` run the new
planner in true shadow mode over real documents.

The final cutover should happen only after real-corpus parity/safety evidence
shows:

```text
Missing
    -> recovery unchanged

Suspicious
    -> reconciliation unchanged

Unverified presentation-only
    -> text ML safely removed

meaningful visual
    -> preserved

Unknown visual
    -> analyzed conservatively
```
