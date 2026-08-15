# Phase 21E.1H.1 — Page-processing evidence model V1

## Status

Production contract increment only.

This increment introduces the evidence vocabulary required for the later
two-axis planner. It deliberately changes **no current routing behavior**.

The existing `PageProcessingAssessment -> IPageProcessingPolicy ->
PageProcessingPlan` path remains untouched.

---

## 1. Why this model exists

The Phase 21E real-corpus diagnostics showed that native-text trust and visual
handling are independent questions.

A page may have:

```text
trusted native text
        +
meaningful independent visual
```

or:

```text
trusted / verifiable native text
        +
presentation-only frame / ornament / blank canvas
```

or:

```text
missing / corrupted native text
        +
any visual evidence
```

The old dominant-raster trigger conflated these questions by using declared
image geometry as a reason to request OCR verification.

The validated direction is therefore:

```text
native text evidence
        ↓
TextAuthority

embedded visual evidence
        ↓
VisualEvidenceKind

        ↓ later policy, NOT this increment

VisualDisposition
        +
PageProcessingPlan
```

---

## 2. TextAuthority

`TextAuthority` is the policy-facing semantic interpretation of the existing
`NativeTextStatus`.

The mapping is explicit and deterministic:

| NativeTextStatus | TextAuthority |
|---|---|
| `Missing` | `Missing` |
| `Healthy` | `Trusted` |
| `Unverified` | `NeedsVerification` |
| `Suspicious` | `Corrupted` |

This mapping selects no page-processing route.

Important invariant:

> Visual evidence must never turn `Missing` or `Corrupted` text into trusted
> native text.

---

## 3. VisualEvidenceKind is evidence, not policy

Evidence is tracked per source visual occurrence.

V1 vocabulary:

```text
Unknown
BlankCanvas
TinyOrNoise
SmallHeadingAssociatedVisual
HeadingBackplateOrPresentation
NativeTextContainerOrFrame
CaptionedMeaningfulVisual
LargeIndependentVisual
```

These names summarize deterministic evidence established during Phase 21E.

They do **not** mean:

```text
delete this image
trust this OCR
skip this page
```

In particular:

```text
SmallHeadingAssociatedVisual
```

does not make the semantic heading decorative.

Likewise:

```text
NativeTextContainerOrFrame
```

does not make the native text inside the frame disposable.

---

## 4. VisualDisposition is a separate policy vocabulary

The later deterministic visual policy may select among:

```text
NoVisual
PresentationOnly
PreserveMeaningfulVisual
RequiresVisualAnalysis
```

This increment defines the vocabulary but performs no mapping.

`PresentationOnly` means:

> the visual does not need to be promoted as documentary meaning for document
> understanding.

It does **not** necessarily mean that source bytes must be physically deleted.
A fidelity-oriented consumer can still retain the source asset.

`RequiresVisualAnalysis` is the fail-closed outcome when deterministic evidence
is insufficient.

---

## 5. PageProcessingEvidence

`PageProcessingEvidence` combines the two independent evidence axes:

```text
PhysicalPageNumber
TextAuthority
VisualElements[]
```

It intentionally has no:

- `PageProcessingRoute`;
- `PageProcessingPlan`;
- `VisualDisposition`;
- layout/OCR execution behavior;
- model/backend dependency;
- consumer-specific semantics.

An empty `VisualElements` collection means no embedded visual occurrence needs
classification. The model does not create a synthetic "no visual" element.

The constructor snapshots the caller-owned collection and rejects duplicate
source visual indexes.

---

## 6. Evidence lineage from Phase 21E

The production vocabulary comes from the deterministic evidence validated in
the Phase 21E diagnostics:

- actual raster foreground rather than declared image canvas size;
- foreground/native-word interaction;
- effective visual geometry;
- heading structural relationship;
- native-text containment inside visual bounds;
- caption association;
- conservative unknown/fallback behavior.

The development/control set reached 22/22 exact counterfactual action matches
after containment and caption evidence were added.

A subsequent blind holdout of 20 previously unused pages produced:

```text
20 / 20 exact matches
0 destructive false negatives
```

The blind set contained fresh examples of:

- meaningful independent Habermas diagrams;
- Ehrman heading ornaments;
- tiny/noise visuals;
- blank canvases;
- native-text presentation containers.

This evidence justifies introducing the vocabulary into production contracts.
It does not prove universal correctness for arbitrary PDFs.

---

## 7. Architectural boundaries

This increment does not:

- modify `NativeTextStatus`;
- modify `PageProcessingAssessment`;
- modify `DefaultPageProcessingAssessor`;
- modify `DefaultPageProcessingPolicy`;
- modify `DocumentPageProcessingPlanner`;
- change `PageProcessingRoute`;
- execute rasterization, layout, OCR or reconciliation;
- classify a production visual;
- discard a visual asset;
- add ML, Python or model dependencies;
- change `DocumentProcessor`;
- change `DocumentIngestionResult`;
- add persistence, RAG or ApologiaStudio concerns.

Phase 21E.1H.2 can now implement a deterministic visual assessor against this
contract without changing page routing in the same increment.

---

## 8. Safety rule for the next increment

The next implementation must preserve this ordering:

```text
explicit native-text failure
        ↓
Missing / Corrupted remains authoritative for text planning

visual evidence
        ↓
may optimize visual handling
but must not erase the text failure
```

For visual uncertainty:

```text
Unknown
    ↓
RequiresVisualAnalysis
```

not:

```text
Unknown
    ↓
PresentationOnly
```

The production system remains fail closed.
