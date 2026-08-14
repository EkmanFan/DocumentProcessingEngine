# Table text fallback V1

## Status

Phase 18E evidence-driven production correction.

## Why this exists

The initial conservative policy intentionally used:

```text
Table   -> Deferred
Unknown -> Deferred
```

That was appropriate before a representative real corpus established what a
`Table` layout observation meant operationally.

Phase 18E then exercised the pinned Ehrman raster table-of-contents/front-matter
pages 14–20 through the real PP-StructureV3 boundary.

Observed layout:

```text
p14  Table + Unknown
p15  Table + Text
p16  Table + Unknown + Unknown
p17  Table + Text
p18  Table + Unknown
p19  Table + Text
p20  Heading + Text + Unknown
```

With `Table -> Deferred`, pages 14, 16, and 18 produced:

```text
0 OCR requests
0 authoritative text elements
0 text-flow elements
```

while the other corpus gates passed.

The Phase 18E gate:

```text
everyTocPageRecoversText
```

therefore failed for a real product reason, not because the gate was too strict.

## Decision

The deterministic production policy is amended to:

```text
Text     -> RecognizeText
Heading  -> RecognizeText
Caption  -> RecognizeText
Table    -> RecognizeText
Figure   -> PreserveVisualWithoutOcr
Unknown  -> Deferred
```

This is generic. It does not special-case Ehrman, a table of contents, page
numbers, or any raw PP-Structure label beyond the existing neutral `Table`
classification.

## Semantic boundary

`Table -> RecognizeText` means:

```text
Table layout region
        ↓
targeted OCR
        ↓
OcrRegionResult
        ↓
native/OCR reconciliation
        ↓
authoritative neutral text flow
```

It does **not** mean:

```text
infer rows
infer columns
infer cells
reconstruct a semantic table
interpret a table of contents
promote TOC entries to document chapters
```

The source `LayoutObservationKind.Table` remains attached to the hybrid element
as provenance.

Because the hybrid stream currently has text-flow kinds rather than a structured
table AST, a resolved Table OCR result maps to:

```text
HybridDocumentElementKind.Text
```

This is intentionally a flow classification, not a claim that the source region
was ordinary prose.

## Safety boundaries retained

```text
Figure   -> PreserveVisualWithoutOcr
Unknown  -> Deferred
undefined layout kind -> Deferred
```

The change therefore does not weaken the papyrus/figure safety invariant proven
on Ehrman page 233.

## Validation plan

This increment must pass:

- policy unit regression;
- targeted OCR planner regression;
- Table OCR -> reconciliation -> hybrid-element provenance regression;
- all existing repository tests;
- second-worktree patch reproducibility.

After commit, Phase 18E must rerun the memory-bounded live corpus closeout. The
expected live consequence is that the six Table observations on Ehrman pages
14–19 are added to the OCR plan, allowing pages 14, 16, and 18 to recover text.

The live rerun, not this unit increment, is the evidence required to close Phase
18.
