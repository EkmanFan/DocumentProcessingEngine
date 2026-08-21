# Current architecture

## Status

**Current — normative repository summary**

This document describes the active architectural responsibilities of
DocumentProcessingEngine. Exact APIs and behavior remain enforced by source
code and tests. The accepted cutover evidence is preserved separately in the
[Target architecture reference V1](../evaluation/target-architecture-reference-v1.md).

## Universal processing cycle

`DocumentProcessing.Engine` owns the transformation from `DocumentSource` to
`DocumentProcessingResult`:

```text
ACQUIRE NATIVE EVIDENCE
        ↓
ASSESS
        ↓
PLAN REQUIRED ENRICHMENT
        ↓
ACQUIRE SUPPLEMENTAL EVIDENCE
        ↓
RECONCILE
        ↓
ASSEMBLE
        ↓
QUALITY GATE
        ↓
DocumentProcessingResult
```

Rasterization, layout analysis and OCR are optional enrichment capabilities.
They are not the universal processing algorithm.

## Responsibility boundaries

### Host

`DocumentProcessingHost` owns only:

- the consumer-facing facade;
- configuration and composition;
- shared-provider lifecycle;
- invocation of the Engine.

The Host does not select processing routes or own assessment, reconciliation,
assembly or quality policy.

### Engine

`DocumentProcessing.Engine` owns:

- document-format selection;
- sufficiency assessment of native evidence;
- enrichment planning;
- use of available technical capabilities;
- native/OCR reconciliation;
- portable document assembly;
- deterministic quality observations.

Internal strategies are implementation mechanisms inside this Engine-owned
cycle. They do not transfer processing ownership to a format adapter.

### Formats

An `IDocumentFormat` implementation recognizes and understands its source
representation. It returns native evidence and may implement orthogonal
technical capabilities that exist because of that representation.

The root format contract remains minimal:

```text
Format
TryExtractNativeEvidenceAsync(...)
```

The current paged/hybrid Engine strategy requires rasterization and native
visual-observation capabilities to be implemented together. An assembly test
enforces that pairing for concrete production formats.

A format must not decide whether evidence is sufficient, whether OCR is
required, which route is authoritative or whether the final result passes a
quality gate.

### Shared capabilities

Layout analysis and OCR are format-independent technical operations. Current
Host composition uses HTTP-configured PP-StructureV3 and PaddleOCR adapters.
Provider outputs remain untrusted inputs that are validated and interpreted by
deterministic Engine policy.

### Core

`DocumentProcessing.Core` contains format-neutral contracts and portable
models. It does not depend on Engine, PDF or provider implementations. Native
text provenance is represented by `TextSelectionOrigin.Native`, not by a
format-specific origin.

## Current assembly direction

```text
DocumentProcessing.Core
        ↑
        ├── DocumentProcessing.Engine
        ├── DocumentProcessing.Pdf
        └── DocumentProcessing.Epub

DocumentProcessing
        └── Core + Engine + Pdf + Epub

DocumentProcessing.DualRunWorker
        └── Core + Engine + Pdf
```

Required constraints:

- Engine has no concrete PDF dependency.
- PDF has no Engine dependency.
- Core has no implementation or provider dependency.
- the Host is the production composition root.

## Current format and execution status

- PDF and EPUB are registered production formats.
- `DocumentProcessing.Epub` owns EPUB recognition, the EPUBCheck 5.3.0
  conformance boundary, package/spine/XHTML acquisition and EPUB source-location
  facts.
- the Engine projects structured EPUB evidence through its native non-paged
  assembly path; EPUB spine items are never represented as physical pages.
- PdfPig supplies native extraction and native visual measurements.
- `pdftoppm` supplies document-scoped rasterization.
- PP-StructureV3 supplies layout observations when planned.
- PaddleOCR supplies targeted text recognition when planned.
- the Engine returns the portable `DocumentProcessingResult` through the Host.

Unsupported, invalid, ambiguous or temporarily unavailable formats are
consumer-facing functional failures. Expected failures of a required external
format capability are mapped to a consumer-safe unavailable result; their
technical diagnostics remain internal. Cancellation and unhandled technical
failures remain exceptional.

## Dual Run

Dual Run is non-authoritative evaluation infrastructure. Candidate output must
never choose or mutate the authoritative result, and candidate failures must
remain isolated from authoritative processing.

The repository contains in-process Dual Run components and an isolated worker,
but the default `DocumentProcessingHost` composition does not enable them. Their
presence must not be interpreted as a second production processing path.

## Extension rules

Adding a format must start with native evidence acquisition through
`IDocumentFormat`. Add only the technical capabilities that the representation
can genuinely provide.

Do not introduce:

- a format-owned complete processor;
- Engine dependencies on concrete format implementations;
- format or provider terminology in Core;
- policy decisions inside acquisition adapters;
- a second authoritative path hidden behind Dual Run or compatibility code.

Any change to these rules requires an explicit architecture decision and an
updated architecture regression gate.

## Deliberate non-goals

The Engine does not own RAG, retrieval chunking, embeddings, vector storage,
LLM/VLM processing, application-specific policy or persistent document
storage. Those concerns belong downstream of the portable processing result.
