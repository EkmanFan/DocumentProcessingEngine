# Phase 21A — native-only vertical execution V1

## Status

Implementation increment after Phase 21.0.

Phase 21.0 froze the page-processing policy/plan boundary. Phase 21A now proves
the first real end-to-end processing path while intentionally remaining limited
to healthy born-digital/native documents.

---

## 1. Public entry point

The project terminology is "Document Processing Engine", so the concrete public
entry point is:

```csharp
DocumentProcessor.ProcessAsync(
    DocumentSource source,
    CancellationToken cancellationToken = default)
```

It returns the already-established canonical:

```text
DocumentIngestionResult
```

No `IDocumentProcessor` interface is introduced in V1. There is currently one
concrete processing implementation, and no second implementation or volatility
requires another abstraction.

---

## 2. Phase 21A execution path

```text
DocumentSource
        ↓
repeatable-source preparation + source SHA-256
        ↓
IDocumentTypeDetector
        ↓
IDocumentExtractor
        ↓
IDocumentPreflightAnalyzer
        ↓
require HealthyBornDigital
        ↓
native blocks
        ↓
HybridDocumentElementFactory.FromNative
        ↓
HybridDocumentAssembler
        ↓
HybridDocumentNormalizer
        ↓
HybridDocumentSegmenter
        ↓
DocumentProcessingProvenanceContext
        ↓
DocumentIngestionResultBuilder
        ↓
DocumentIngestionResult
```

The processor coordinates existing proven components. It does not reimplement
their extraction, normalization, segmentation, provenance or quality rules.

---

## 3. Why preflight now has an interface

`PdfPreflightAnalyzer` previously existed as a concrete PDF capability.

The end-to-end processor belongs to `DocumentProcessing.Engine`, which must not
depend on `DocumentProcessing.Pdf`.

Phase 21A therefore adds the narrow capability boundary:

```text
IDocumentPreflightAnalyzer
  CanAnalyze(DocumentFormatId)
  Analyze(DocumentExtractionResult)
```

and makes:

```text
PdfPreflightAnalyzer : IDocumentPreflightAnalyzer
```

This is a real format-specific volatility/capability boundary, not a generic
pipeline abstraction.

No preflight registry or plugin system is introduced.

---

## 4. Native-only safety gate

Phase 21A accepts only:

```text
DocumentPreflightClassification.HealthyBornDigital
```

It explicitly rejects:

```text
Hybrid
RasterOrScanned
Problematic
```

This is intentionally conservative.

A mixed/raster document must **not** be converted into a partial result merely
because the native extractor happened to recover some pages.

Phase 21B/21C will replace this document-level native-only gate with the
page-level assessment/policy and hybrid execution already designed in 21.0.

---

## 5. Relationship to the 21.0 policy contract

Phase 21A does not yet create or consume:

```text
PageProcessingAssessment
IPageProcessingPolicy
PageProcessingPlan
```

That is intentional.

The first vertical establishes that the common deterministic execution chain
works end-to-end for a source that needs only the already-proven `NativeOnly`
path.

Phase 21B will introduce the deterministic page assessment plus default mapping:

```text
Healthy
    → NativeOnly

Missing
    → LayoutWithTargetedOcrRecovery

Suspicious
    → LayoutWithTargetedOcrReconciliation
```

without rewriting the common downstream execution proven here.

---

## 6. Source custody and repeatable reads

The final result requires a cryptographic source identity before provenance can
be built.

`DocumentProcessor` therefore prepares the input as follows.

### Seekable source

```text
remember caller position
    ↓
read from byte zero + SHA-256
    ↓
reset to zero for detection/extraction
    ↓
process
    ↓
restore caller position
```

The caller-owned stream is never disposed by the processor.

### Non-seekable source

The document may be too large to buffer safely in memory.

Phase 21A therefore:

```text
non-seekable stream
    ↓
single copy + SHA-256
    ↓
internal delete-on-close temporary file
    ↓
seekable exact-byte source
    ↓
detection/extraction
    ↓
dispose/delete temporary storage
```

The temporary path is internal runtime state and is never included in:

- provenance;
- `DocumentIngestionResult`;
- JSON transport;
- logs produced by the result contract.

This makes the public `DocumentSource` readable-stream contract usable without
introducing unbounded in-memory buffering.

---

## 7. Provenance-context ownership

Phase 21A is the first production component that constructs:

```text
DocumentProcessingProvenanceContext
```

from the actual run.

It owns:

```text
source format
source SHA-256
source byte length
physical page count
source file name/media type
engine version
native extraction identity
```

For a native-only run it deliberately supplies no:

```text
rasterization identity
layout identity
OCR identity
reconciliation identity
visual-preservation profile
```

The downstream proven provenance/result builders derive the remaining
assembly/normalization/segmentation profile identities from the actual graph.

---

## 8. Explicit evidence validation

The processor does not blindly trust capability outputs.

It rejects:

- "supported" type detection without a format identifier;
- an extractor that cannot handle the detected format;
- a preflight analyzer that cannot handle the detected format;
- extraction format differing from detected format;
- zero extracted pages;
- non-contiguous/non-one-based physical page numbering;
- preflight format differing from extraction format;
- preflight page count differing from extraction page count;
- a `HealthyBornDigital` page with native words but no native text blocks.

Existing assembler/result invariants continue to validate ordering, provenance,
segment membership and portable result consistency.

---

## 9. Cancellation

`ProcessAsync` accepts a `CancellationToken`.

Cancellation is observed during:

- initial source preparation/hashing;
- type detection;
- extraction;
- page projection;
- normalization;
- segmentation.

No retry behavior is introduced in 21A.

---

## 10. Real-corpus validation

The implementation script validates the public `DocumentProcessor` against a
complete derived De Decretis fixture:

```text
original physical pages 512-561
        ↓
derived fixture pages 1-50
```

The original corpus SHA-256 and byte length are verified before fixture
derivation.

The derived fixture receives its own SHA-256 and that derived SHA is the actual
`DocumentIngestionResult.Source.Sha256` custody root.

Expected established native parity:

```text
pages      50
elements   269
segments   50
origin     NativePdf only
OCR        none
layout     none
visual     none
```

This is a Phase 21A vertical integration check, not the final Phase 21D
multi-route corpus proof.

No corpus PDF/result JSON is committed by this increment.

---

## 11. Non-goals

Phase 21A does not add:

- default `IPageProcessingPolicy`;
- per-page Healthy/Missing/Suspicious assessment;
- rasterization;
- layout execution;
- targeted OCR;
- visual preservation execution;
- native/OCR reconciliation;
- model/container lifecycle;
- JSON serialization;
- persistence;
- retrieval chunks;
- embeddings;
- vector database;
- ApologiaStudio integration;
- generic pipeline/DAG/middleware framework;
- capability/plugin registry.

---

## 12. Next increment

```text
21.0  page-processing policy + plan contract        DONE
21A   native-only vertical execution                 THIS INCREMENT
21B   deterministic assessment + default policy     NEXT
21C   hybrid execution                               TODO
21D   real-corpus end-to-end proof                   TODO
```

21B should extend the decision boundary without disturbing the common
native assembly/normalization/segmentation/result path established here.
