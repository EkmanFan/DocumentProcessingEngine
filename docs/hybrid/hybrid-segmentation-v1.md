# Hybrid structural segmentation V1

## Status

Phase 18D production-boundary increment.

```text
18A  unified hybrid assembly boundary          DONE
18B  real page-233 hybrid runtime integration  DONE
18C  unified hybrid normalization              DONE
18D  structural segmentation over hybrid      THIS INCREMENT
18E  broader end-to-end corpus regression      NEXT
```

## Purpose

Structural segmentation now consumes the **single normalized hybrid stream**.

The engine must not perform:

```text
native normalization -> native segmentation
OCR normalization    -> OCR segmentation
                         ↓
                     merge segments
```

Instead:

```text
HybridDocumentNormalizationResult
        ↓
HybridDocumentSegmenter
        ↓
HybridDocumentSegmentationResult
```

This permits one structural unit to cross physical pages and native/OCR origin
transitions while retaining provenance per source element.

## Why a separate hybrid segment model exists

The existing legacy `DocumentSegment` owns:

```text
IReadOnlyList<NormalizedDocumentTextBlock> SourceBlocks
```

That is correct for the born-digital native pipeline, but OCR-only text has no
`DocumentTextBlock`.

Creating fake native blocks for OCR would destroy provenance.

Phase 18D therefore introduces:

```text
HybridDocumentSegment
HybridDocumentSegmentationResult
HybridDocumentSegmenter
```

without changing the legacy public segmentation result.

## Segment source evidence

A hybrid segment retains:

```text
SourceElements
TextElements
TextOrigins
VisualElements
HasUnresolvedEvidence
```

`SourceElements` may include:

```text
Text
Heading
Caption
Visual
Deferred
UnresolvedText
```

provided the evidence falls inside the text-led structural unit.

Only `TextElements` contribute to `HybridDocumentSegment.Text`.

Therefore:

- a Figure can belong to a section without becoming narrative text;
- Deferred evidence can remain visible inside a section;
- a reconciliation Conflict can remain visible inside a section;
- neither Deferred nor Conflict text is silently invented.

Excluded recurring headers/footers do not enter segment source evidence.

## Coverage invariant

`HybridDocumentSegmentationResult` validates that every non-excluded
`IsTextFlowElement` from the normalization belongs to exactly one segment.

It also rejects:

- duplicate segment ids;
- non-contiguous ordinals;
- segment evidence from another normalization;
- the same normalized source element appearing in multiple segments.

Pure visual/deferred pages are allowed to produce no textual structural segment;
their evidence remains in the source normalization.

## Heading evidence precedence

Hybrid heading decisions are deterministic.

### 1. Explicit layout Heading

```text
HybridDocumentElementKind.Heading
```

is accepted directly as structural evidence.

This is the selected layout model's neutral classification, already validated by
the live mixed-content runtime.

### 2. Explicit editorial heading hint

A caller-supplied `DocumentSegmentationOptions.HeadingHints` value may promote a
`Text` element to heading.

The existing deterministic `HeadingHintMatcher` now exposes a source-agnostic
text overload so both legacy and hybrid segmentation use the same matcher.

### 3. Layout-less native typography fallback

For born-digital native elements created without a `LayoutObservation`, strict
typography remains available:

```text
NativeBlock
+
normalized text
+
weighted document body font
```

Phase 18D extracts the existing native typography policy into
`NativeHeadingEvidenceRules`.

The legacy `HeadingEvidenceEvaluator` delegates to these exact rules, so existing
legacy segmentation behavior remains covered by its regression suite.

### 4. Explicit layout Text is not silently overridden

If a hybrid element already has a layout observation classified as `Text`, native
typography does **not** silently promote it to Heading.

Only an explicit editorial hint can override that layout Text classification.

### 5. Caption is not a heading candidate

`Caption` remains authoritative text but is not promoted to a structural heading.

## Cross-page behavior

The existing conservative behavior remains:

```text
recognized heading-led structure
        -> may span physical pages

unstructured fallback
        -> page bounded
```

The source origin does not create a segment boundary.

Phase 18D unit coverage explicitly exercises:

```text
Native -> Native
OCR    -> OCR
Native -> OCR
OCR    -> Native
```

A mixed-origin segment exposes:

```text
TextOrigins
IsMixedTextOrigin
```

but its text remains one ordered structural unit.

## Non-text evidence inside a section

Once a heading-led structure has started, non-excluded evidence encountered in
document reading order remains part of the segment's `SourceElements`.

For example:

```text
Heading / OCR
Text / OCR
Visual
Deferred
UnresolvedText
Text / Native
```

can remain one structural section.

Segment narrative text is still only:

```text
Heading
Text
Text
```

The source evidence list preserves the gaps and visual material for later
provenance/quality integration.

## Deterministic identifiers

Hybrid segment ids follow the existing document-local shape:

```text
p000233-s000012
```

They remain deterministic within one segmentation result and are not global
document identities.

## Explicit non-goals

18D does not:

- run PDF extraction;
- call layout or OCR services;
- reconcile native/OCR text;
- normalize text;
- resolve Deferred evidence;
- resolve reconciliation Conflict;
- OCR figures;
- create retrieval chunks;
- create `DocumentIngestionResult`;
- add a generic ingestion/orchestration framework.

## Next: 18E

18E is the broader real corpus regression.

It must validate the complete currently available hybrid path through
segmentation on:

### Ehrman raster-only pages 14-20

- native text remains absent;
- modern text is recovered through layout + targeted OCR;
- no visual-only content becomes narrative text;
- segmentation consumes the unified OCR-backed stream.

### Ehrman hybrid pages 1-10

- trustworthy native content remains authoritative where appropriate;
- missing regions are OCR-recovered;
- duplicate native/OCR content is not emitted;
- mixed-origin structural segments preserve per-element provenance.

### Ehrman page 233

- Heading/Text/Caption remain textual;
- papyrus Figure remains visual-only;
- Deferred regions remain explicit;
- page-level reading order survives normalization and segmentation.

### De Decretis born-digital regression

- layout-less native strict typography continues to match the legacy structural
  behavior;
- adding the hybrid path does not degrade the existing born-digital pipeline.

### Cross-page origin transitions

At least:

```text
Native -> Native
OCR    -> OCR
Native -> OCR
OCR    -> Native
```

must occur in real or pinned deterministic corpus scenarios.

Only after 18E should the project treat the hybrid normalization + segmentation
path as the stable basis for Phase 19 provenance/quality integration.
