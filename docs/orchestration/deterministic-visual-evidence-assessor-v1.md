# Phase 21E.1H.2 — Deterministic visual evidence assessor V1

## Status

Production classifier increment only.

This increment converts already-measured deterministic visual signals into the
neutral `VisualEvidenceKind` vocabulary introduced by Phase 21E.1H.1.

It deliberately does **not**:

- assign `VisualDisposition`;
- change `PageProcessingRoute`;
- modify `DefaultPageProcessingAssessor`;
- modify `DocumentPageProcessingPlanner`;
- execute layout or OCR;
- decode PDF images in production;
- discard or preserve a source asset;
- add ML or model dependencies.

The current execution behavior is therefore unchanged.

---

## 1. Input boundary

The assessor consumes `VisualEvidenceObservation`.

The observation is policy-neutral and contains only the signals that were
validated during Phase 21E:

```text
VisualForegroundState
ForegroundPixelRatio
VisualPixelInteractionKind
NativeWordsTouchedRatio
SignificantComponentCount
EffectiveVisualAreaRatio
HeadingAssociationEvidenceKind
NativeTextContainmentEvidenceKind
CaptionAssociationEvidenceKind
```

The observation intentionally contains no:

```text
VisualEvidenceKind
VisualDisposition
PageProcessingRoute
```

That keeps measurement, evidence classification and processing policy as
separate responsibilities.

---

## 2. Frozen classifier order

The production classifier uses the exact Phase 21E.1F precedence:

```text
1. blank canvas
2. unavailable / indeterminate foreground -> Unknown
3. strong caption
4. strong small heading association
5. tiny/noise
6. heading-dominated contained text
7. text-rich container
8. large independent visual
9. Unknown
```

The ordering matters.

### Caption before container

A meaningful figure can contain native labels or text and therefore look
container-like.

Phase 21E p79 demonstrated this failure mode.

The rule therefore remains:

```text
StrongCaptionAssociation
        ↓
CaptionedMeaningfulVisual
```

before considering:

```text
TextRichContainer
        ↓
NativeTextContainerOrFrame
```

### Strong heading association before tiny/noise

A tiny visual beside a semantic heading is classified as
`SmallHeadingAssociatedVisual`, not collapsed into generic noise.

This preserves the structural distinction learned from pages such as Ehrman
p331.

---

## 3. Frozen thresholds

These thresholds are not newly tuned in this production increment.

They are the values frozen by Phase 21E.1F and subsequently exercised by the
independent blind holdout.

```text
small foreground maximum                0.005
small-heading touched-word maximum      0.01
small-heading effective-area maximum    0.02
tiny/noise touched-word maximum         0.02
tiny/noise significant components       2
large-independent foreground minimum    0.05
```

No threshold change is allowed merely to make a future regression pass.

A regression mismatch must first be explained by missing or incorrect
evidence.

---

## 4. Fail-closed behavior

The classifier returns:

```text
VisualEvidenceKind.Unknown
```

when deterministic evidence is insufficient.

It does not guess.

Examples include:

- foreground decode/measurement unavailable;
- conflicting signals not covered by the validated policy;
- a visual that is neither safely presentation-like nor safely meaningful
  under the frozen rules.

The previously discussed Ehrman p148 remains an intentional example:

```text
human review:
    decorative small illustration beside a title

frozen deterministic classifier:
    Unknown
```

This is a conservative false positive for expensive fallback, not a
destructive false negative.

Phase 21E.1H.3 must map `Unknown` to fail-closed visual handling.

---

## 5. Regression evidence carried into production tests

The unit tests reproduce deterministic observations from two sources.

### Development / diagnostic controls

Representative controls include:

- Ehrman p2 — heading backplate while semantic title remains native;
- Ehrman p79 — captioned meaningful figure;
- Ehrman p185 / p551 — text-rich presentation containers;
- Ehrman p331 — small heading-associated ornament;
- Ehrman p543 — blank canvas;
- Ehrman p114 — post-freeze caption generalization;
- Ehrman p148 — conservative `Unknown`;
- Ehrman p233 — missing-text mixed page whose visual evidence remains
  insufficient by this classifier alone.

### Independent blind holdout

All twenty Phase 21E.1G pages are embedded as frozen assessor regression
vectors.

The human review result was:

```text
20 / 20 exact disposition agreement
0 destructive false negatives
```

The holdout included:

- four meaningful independent Habermas visuals;
- sixteen Ehrman presentation-only visuals across heading-associated,
  tiny/noise, blank-canvas and native-text-container classes.

The assessor tests reproduce the **frozen evidence classes**, not the human
labels themselves. Human labels justified those frozen classes as a safe basis
for productionization.

---

## 6. Architectural boundary

The production flow after this increment is conceptually:

```text
upstream deterministic measurement
        ↓
VisualEvidenceObservation
        ↓
DefaultVisualEvidenceAssessor
        ↓
VisualElementEvidence / VisualEvidenceKind

---------------- future policy boundary ----------------

VisualDisposition
        ↓
two-axis page planning
```

The upstream measurement implementation remains a separate concern.

That separation is intentional: raster decoding, pixel/component analysis,
native-text containment and caption extraction have different dependencies and
failure modes from the pure evidence classifier.

Phase 21E.1H.3 can now introduce the policy mapping from
`TextAuthority × VisualEvidenceKind` to `VisualDisposition` and page-planning
requirements without embedding threshold logic into the planner.

---

## 7. Safety invariants for the next increment

The next policy layer must preserve:

```text
TextAuthority.Missing
    -> text recovery remains required

TextAuthority.Corrupted
    -> text reconciliation remains required
```

regardless of visual evidence.

For visual handling:

```text
CaptionedMeaningfulVisual
LargeIndependentVisual
    -> preserve meaningful visual

Unknown
    -> RequiresVisualAnalysis
```

Presentation-like evidence may remove unnecessary **text verification work**,
but it must not silently delete native semantic text or source-fidelity assets.
