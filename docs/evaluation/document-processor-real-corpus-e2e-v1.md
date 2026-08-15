# DocumentProcessor real-corpus end-to-end proof V1

## Status

**PASS**

Validated baseline:

```text
5532f8e6d55ab5904fef385c659bb8dce09f4cb1
```

Public entry point under test:

```text
DocumentProcessor.ProcessAsync(...)
```

This document freezes the sanitized acceptance evidence produced by Phase 21D.1.
No production source or test code was changed to obtain this proof.

## Scope

The proof exercises the public processor boundary, not the page executors in
isolation:

```text
DocumentSource
  -> document type detection
  -> PdfPig native extraction
  -> PDF preflight
  -> deterministic page planning
  -> selected page route
  -> rasterization when required
  -> PP-StructureV3 layout when required
  -> deterministic region policy
  -> targeted PaddleOCR when required
  -> native/layout pairing when required
  -> deterministic native/OCR reconciliation
  -> figure preservation
  -> common hybrid assembly
  -> normalization
  -> structural segmentation
  -> provenance / quality projection
  -> DocumentIngestionResult
```

## Evaluation fixture policy

The complete source documents are cryptographically pinned.

For expensive hybrid controls, Phase 21D.1 derives one-page PDF fixtures from
the exact pinned Ehrman physical pages 233, 380 and 405. The public processor
therefore processes real source-page material without paying the cost of running
all 617 pages merely to re-prove three already-selected authority controls.

This is an evaluation optimization only. It is not a production ingestion
format.

The result source identity refers to the exact derivative fixture bytes actually
processed, while each report also records the original pinned document SHA-256
and the corresponding original physical page number.

## Model residency policy

The live evaluation uses the production PP-StructureV3 and PaddleOCR HTTP
adapters from inside the public processor.

On the constrained local workstation, the evaluation harness performs a
controlled handoff after the complete live layout response has returned:

```text
DocumentProcessor
  -> PP-StructureV3
  -> layout response complete in .NET
  -> evaluation-only handoff gate
  -> PP-StructureV3 stopped
  -> PaddleOCR started
  -> processor resumes
  -> targeted OCR
```

PP-StructureV3 and PaddleOCR are therefore never resident concurrently.

This handoff belongs only to the evaluation harness. DPEngine does not acquire
Docker/model lifecycle responsibility.

## De Decretis native control

Original physical source range:

```text
512-561
```

Observed through the public processor:

```text
pages                         50
native words                  29044
native blocks                 269
Healthy -> NativeOnly         50/50
result elements               269
result structural segments    50
exact selected-source parity  true
sentinel preserved            true
hybrid manifest absent        true
```

The established native regression baseline of **29,044 words / 269 blocks** is
therefore retained through `DocumentProcessor.ProcessAsync(...)`.

## Ehrman physical page 233

Automatic route:

```text
Missing -> LayoutWithTargetedOcrRecovery
```

Live layout:

```text
observations       10
OCR text targets   7
Figures            1
OCR sequences      [2, 3, 5, 6, 7, 8, 9]
```

Public authority result:

```text
OcrOnly / Ocr
```

Observed:

```text
real PaddleOCR requests          7
Figure observation sequence      4
Figure preserved                 true
Figure sent to OCR               false
visual custody exact-byte SHA    true
```

The papyrus/facsimile remains visual evidence and does not become narrative OCR
text.

## Ehrman physical page 380

Automatic route:

```text
Unverified -> LayoutWithTargetedOcrReconciliation
```

Pinned pairing control:

```text
layout sequence          5
native source block      2
comparable native words  299
pairing status           Comparable
```

Public authority result:

```text
Conflict / None
resolved=false
divergence=true
```

The native/OCR conflict remains unresolved. Native text is not silently promoted
to authority.

## Ehrman physical page 405

Automatic route:

```text
Unverified -> LayoutWithTargetedOcrReconciliation
```

Pinned pairing control:

```text
layout sequence          9
native source block      6
comparable native words  132
pairing status           Comparable
```

Public authority result:

```text
Agreement / NativePdf
resolved=true
divergence=false
```

Agreement retains native PDF text as authoritative while preserving OCR
verification evidence.

## Aggregate live OCR evidence

Across the three Ehrman controls:

```text
real PaddleOCR requests = 24
```

Breakdown:

```text
p233  7
p380  9
p405  8
```

No Figure entered OCR in any control.

## Acceptance result

```text
De Decretis
  50/50 Healthy -> NativeOnly
  native 29,044-word / 269-block baseline retained
  PASS

Ehrman p233
  Missing -> targeted OCR recovery
  OcrOnly / Ocr
  Figure seq4 preserved and never OCR'd
  exact-byte visual custody verified
  PASS

Ehrman p380
  Unverified -> reconciliation
  target seq5 / block2 / 299 comparable words
  Conflict / None
  unresolved
  PASS

Ehrman p405
  Unverified -> reconciliation
  target seq9 / block6 / 132 comparable words
  Agreement / NativePdf
  PASS

public DocumentProcessor used for final controls
  PASS

PP-StructureV3 and PaddleOCR never concurrently resident
  PASS

full regression before and after live execution
  316/316
  PASS
```

## What this establishes

Phase 21D closes the correctness proof for the current PDF V1 processing
architecture behind the public processor.

The next work should **not** immediately add consumer-specific RAG behavior.
Before ApologiaStudio integration, the engine needs a measured performance and
operability phase covering long-document cost, peak memory, throughput, bounded
concurrency, service residency, checkpoint/resume, and deterministic cache/reuse
of expensive intermediate evidence.

See the separate performance/operability architectural note for that roadmap
decision.
