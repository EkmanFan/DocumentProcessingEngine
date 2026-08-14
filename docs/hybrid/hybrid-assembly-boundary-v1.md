# Hybrid assembly boundary V1

## Status

Phase 18A production-boundary increment.

```text
17 Native/OCR reconciliation                    DONE

18 End-to-end hybrid regression
   18A unified hybrid assembly boundary          THIS INCREMENT
   18B real corpus/runtime hybrid regression     NEXT
```

## Purpose

The engine already has independently validated boundaries for:

- native PDF extraction;
- layout analysis;
- targeted OCR;
- visual preservation;
- native/OCR reconciliation.

18A introduces the next missing boundary: a single neutral page/document stream
that can contain all of those evidence types **before final normalization and
segmentation**.

```text
native-only text
resolved native/OCR text
unresolved native/OCR evidence
preserved visual evidence
deferred Unknown/Table evidence
        ↓
HybridDocumentAssembler
        ↓
HybridDocumentAssemblyResult
        ↓
future unified normalization / segmentation
```

18A is intentionally not the real service-orchestration regression. That is 18B.

## Why the existing normalized-text model is not reused

`NormalizedDocumentTextBlock` requires a `DocumentTextBlock` source.

That is appropriate for the born-digital extraction path, but it cannot represent
OCR-only text without inventing a fake native block. Doing so would destroy
provenance.

Therefore 18A introduces a neutral hybrid element stream rather than forcing OCR
evidence through a native-only normalization model.

## Element kinds

```text
Text
Heading
Caption
Visual
UnresolvedText
Deferred
```

Rules:

- `Text`, `Heading`, and `Caption` contain authoritative selected text and an
  explicit `TextSelectionOrigin`;
- `Visual` contains `PreservedVisualEvidence` and no text;
- `UnresolvedText` contains unresolved reconciliation evidence and no selected
  text;
- `Deferred` contains neutral layout evidence only and no selected text.

`Unknown` and `Table` therefore remain non-authoritative.

## Evidence adapters

`HybridDocumentElementFactory` is deliberately small.

It can adapt:

```text
DocumentTextBlock
TextReconciliationResult
PreservedVisualEvidence
LayoutObservation (Deferred only)
```

It does not perform:

- PDF extraction;
- layout analysis;
- OCR;
- spatial matching;
- reconciliation;
- visual persistence.

Those remain explicit upstream boundaries.

## Reading order

For layout-backed elements, explicit `LayoutObservation.ReadingOrder` is
required.

`ObservationSequence` is **not** silently used as reading order because the
layout model explicitly treats backend emission sequence as distinct evidence.

For standalone native blocks, the factory uses:

```text
DocumentTextBlock.ReadingOrder
```

and falls back to:

```text
DocumentTextBlock.SourceSequence
```

when no derived native reading order exists.

`HybridDocumentAssembler` rejects duplicate reading-order values on one page
rather than inventing a tie-break rule.

## Duplicate prevention

The assembler rejects:

1. the same layout observation emitted more than once;
2. a standalone native block emitted together with reconciled text from the same
   block;
3. multiple reconciliations from the same native block when they do not carry
   explicit comparable extents;
4. overlapping comparable native word ranges from the same native block.

It allows multiple non-overlapping comparable extents from one larger native
block. This is necessary for a future page where one extractor block spans more
than one layout/OCR region.

The assembler does not itself split native blocks or discover pairings.

## Mixed-content page semantics

The intended page-233 shape is representable directly:

```text
Heading             authoritative text
Text                authoritative text
Visual              preserved papyrus, no OCR text
Caption             authoritative text
Text                authoritative text
```

The visual element cannot contain narrative text and must be backed by
`LayoutObservationKind.Figure`.

## Unresolved evidence

A `Conflict`, `SuspiciousNativeUnverified`, or other unresolved reconciliation
does not disappear from the document.

It becomes:

```text
HybridDocumentElementKind.UnresolvedText
Text = null
TextOrigin = None
```

This keeps the ambiguity visible without contaminating downstream text.

## Output

`HybridDocumentAssemblyResult` contains ordered `HybridDocumentPage` instances.

It is intentionally **not** named `DocumentIngestionResult` yet. The final
generic ingestion result should only be introduced after the unified stream has
passed real hybrid runtime regression and has a proven path into normalization
and structural segmentation.

## 18A acceptance

The unit regression proves that:

- mixed text/figure/caption elements are ordered deterministically;
- a figure is textless;
- conflicts remain unresolved and textless;
- Unknown/Table evidence remains deferred;
- standalone native text retains NativePdf provenance;
- duplicate layout evidence is rejected;
- standalone/reconciled native duplication is rejected;
- non-overlapping comparable extents from one larger native block are allowed;
- ambiguous reading order is rejected;
- document pages are ordered by physical page;
- unresolved evidence remains visible at document level.

## Explicit non-goals

18A does not:

- run PP-StructureV3 or PaddleOCR;
- rasterize PDFs;
- persist visual bytes;
- automatically pair native/OCR regions;
- normalize the unified hybrid stream;
- segment the unified hybrid stream;
- create `DocumentIngestionResult`;
- perform cross-page paragraph reconstruction.

## Next: 18B

18B should execute the real runtime path on the pinned corpora.

Minimum real corpus requirements:

### Ehrman pages 14-20 — raster-only

- native text baseline remains absent;
- modern text is recovered through layout + targeted OCR;
- text is emitted once;
- provenance remains OCR.

### Ehrman pages 1-10 — hybrid

- trustworthy native text is retained where present;
- missing regions/pages are OCR-recovered;
- duplicate native/OCR text is not emitted;
- unresolved evidence remains explicit.

### Ehrman page 233 — mixed content

- modern heading/body/caption become textual elements;
- papyrus figure is preserved as visual evidence;
- papyrus is never emitted as narrative OCR text;
- cross-column reading order remains usable.

### De Decretis — born-digital regression

- native extraction/normalization quality does not regress merely because the
  hybrid path exists.

### Cross-page boundaries

Exercise at least:

```text
Native -> Native
OCR    -> OCR
Native -> OCR
OCR    -> Native
```

Physical page remains provenance, not a semantic segmentation boundary.
