# Phase 21E.1H.3A — Two-axis page-processing requirements policy V1

## Status

Production policy increment only.

This increment maps the independent evidence axes introduced in 21E.1H.1 and
produced by 21E.1H.2 into independent processing requirements.

It deliberately does **not** wire those requirements into the existing
`DocumentPageProcessingPlanner` or change any current execution route.

Current runtime behavior therefore remains unchanged.

---

## 1. Why this is a separate policy layer

The old V1 routing model is atomic:

```text
NativeOnly
LayoutWithTargetedOcrRecovery
LayoutWithTargetedOcrReconciliation
```

That model was correct before text trust and visual handling were separated,
but it cannot faithfully represent combinations such as:

```text
trusted native text
+
meaningful visual to preserve
```

or:

```text
trusted native text
+
unknown visual requiring analysis
```

without unnecessarily coupling visual work to OCR/reconciliation.

Phase 21E established that these are independent concerns.

The new flow is:

```text
PageProcessingEvidence
  ├── TextAuthority
  └── VisualElementEvidence[]
          ↓
DefaultPageProcessingRequirementsPolicy
          ↓
PageProcessingRequirements
  ├── TextProcessingRequirement
  └── VisualElementDisposition[]
```

The execution route remains a later concern.

---

## 2. Text requirements

The V1 text requirements are:

```text
UseNativeText
RecoverMissingNativeText
VerifyNativeText
ReconcileCorruptedNativeText
```

### Authoritative failures remain authoritative

Visual evidence never overrides explicit text failure:

```text
TextAuthority.Missing
    -> RecoverMissingNativeText

TextAuthority.Corrupted
    -> ReconcileCorruptedNativeText
```

This holds regardless of the visual evidence.

### Trusted native text remains trusted

```text
TextAuthority.Trusted
    -> UseNativeText
```

An unknown or meaningful visual may require visual work, but it does not invent
a text-verification requirement.

### NeedsVerification is resolved conservatively

`NeedsVerification` was historically triggered by dominant declared raster
geometry.

Phase 21E showed that this trigger frequently represented presentation-only
graphics, blank canvases, tiny ornaments, or meaningful visuals independent of
the native text rather than native-text corruption.

The policy therefore uses:

```text
NeedsVerification
+
at least one classified visual
+
all visual dispositions non-ambiguous
    -> UseNativeText
```

but:

```text
NeedsVerification
+
no classified visual
    -> VerifyNativeText
```

and:

```text
NeedsVerification
+
any RequiresVisualAnalysis
    -> VerifyNativeText
```

The second and third cases fail closed.

---

## 3. Visual disposition mapping

The mapping is deliberately simple and contains no thresholds.

Thresholds belong to `DefaultVisualEvidenceAssessor`, not to this policy.

```text
Unknown
    -> RequiresVisualAnalysis

BlankCanvas
TinyOrNoise
SmallHeadingAssociatedVisual
HeadingBackplateOrPresentation
NativeTextContainerOrFrame
    -> PresentationOnly

CaptionedMeaningfulVisual
LargeIndependentVisual
    -> PreserveMeaningfulVisual
```

The mapping is per visual occurrence.

A page may therefore contain both:

```text
PresentationOnly
+
PreserveMeaningfulVisual
```

without collapsing the whole page to one visual label.

---

## 4. Meaning of PresentationOnly

`PresentationOnly` remains a document-understanding policy decision.

It means:

> the visual does not need to be promoted as independent documentary meaning
> and does not justify text verification by itself.

It does **not** mean:

- delete the source bytes;
- erase native text contained by a frame;
- discard source-fidelity information required by a consumer.

A consumer that needs faithful archival reconstruction may still retain the
source asset.

---

## 5. Meaning of PreserveMeaningfulVisual

`PreserveMeaningfulVisual` means the visual carries documentary meaning and
must remain preservable.

Examples validated in Phase 21E include:

- captioned figures;
- independent diagrams;
- timelines;
- other substantial visuals spatially independent from native text.

This disposition is independent from text authority.

For example:

```text
TextAuthority.Trusted
+
LargeIndependentVisual
    ->
UseNativeText
+
PreserveMeaningfulVisual
```

No OCR requirement is implied by the visual.

---

## 6. Why the legacy planner is not modified in this increment

The current `PageProcessingRoute` model derives rasterization, layout, OCR and
reconciliation from one atomic route.

It cannot express:

```text
UseNativeText
+
RequiresVisualAnalysis
```

without also implying OCR through the existing hybrid routes.

It also cannot express:

```text
UseNativeText
+
PreserveMeaningfulVisual
```

as an explicit independent requirement.

Mapping the new two-axis policy directly back into the old route enum in this
same increment would therefore recreate the coupling that Phase 21E was meant
to remove.

H.3A stops at requirements.

The next integration increment must decide how execution planning represents
independent text and visual work before replacing the legacy planner path.

---

## 7. Contract invariants

`PageProcessingRequirements` contains:

```text
PhysicalPageNumber
TextProcessingRequirement
VisualElementDisposition[]
```

It deliberately contains no:

```text
PageProcessingRoute
PageProcessingPlan
```

The policy is pure and performs no I/O.

`VisualElementDisposition` rejects `NoVisual`, because it represents an actual
visual occurrence. A page with no visuals uses an empty collection.

Collections are snapshotted and duplicate source visual indexes are rejected.

---

## 8. Regression strategy

The unit-test matrix evaluates every current combination:

```text
4 TextAuthority values
x
8 VisualEvidenceKind values
=
32 combinations
```

Additional tests cover:

- `NeedsVerification` with no visuals;
- `NeedsVerification` with multiple known visuals;
- `NeedsVerification` with one unknown visual;
- trusted text with unknown visual;
- missing text with meaningful visual;
- corrupted text with presentation-only visual;
- contract immutability and invalid states;
- absence of legacy execution routes from the new contracts.

This increment adds policy regression only. It does not replace the real-corpus
regression evidence already frozen in H.2.

---

## 9. Next integration boundary

The next step must not blindly translate requirements back into the old three
routes.

It should first define an execution-plan representation capable of expressing
independent requirements such as:

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

while preserving:

```text
RecoverMissingNativeText
ReconcileCorruptedNativeText
```

as authoritative text requirements.

Only after that representation is explicit should the production planner be
rewired.
