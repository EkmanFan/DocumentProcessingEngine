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

Layout analysis and OCR are format-independent technical operations. Their
Core ports are implemented by provider adapters. Each adapter translates the
neutral Core contract to a concrete provider client and translates the
provider-native result back to neutral evidence.

Current Host composition uses `PpStructureV3LayoutAdapter` over
`PpStructureV3ServingClient` and `PaddleOcrAdapter` over
`PaddleOcrServingClient`. Provider clients own transport and provider protocol;
they do not implement Core capability ports and do not own Engine processing
policy. Provider outputs remain untrusted evidence interpreted by deterministic
Engine policy.

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
        ├── DocumentProcessing.Epub
        ├── DocumentProcessing.Layout.Adapters
        └── DocumentProcessing.Ocr.Adapters

DocumentProcessing
        └── Core + Engine + Pdf + Epub
          + Layout.Adapters + Ocr.Adapters

DocumentProcessing.DualRunWorker
        └── Core + Engine + Pdf
```

Required constraints:

- Engine has no concrete PDF dependency.
- PDF has no Engine dependency.
- Core has no implementation or provider dependency.
- Engine does not reference layout or OCR adapter assemblies.
- layout and OCR adapter assemblies do not reference Engine.
- adapters implement Core capability ports and own neutral/provider translation.
- provider serving clients return provider-native results and do not implement
  Core capability ports.
- the Host is the production composition root for concrete provider selection.

## Current format and execution status

- PDF and EPUB are registered production formats.
- `DocumentProcessing.Epub` owns EPUB recognition, the EPUBCheck 5.3.0
  conformance boundary, package/spine/XHTML acquisition, EPUB source-location
  facts, exact packaged-image materialization and physical EPUB publication
  writing.
- `EpubPublicationExporter` writes a completed portable
  `DocumentProcessingResult` as reflowable EPUB. The Engine remains the owner of
  content meaning and selection; the format project owns only EPUB packaging.
  Caller-owned visual bytes are reopened by an explicit reader and verified
  against their portable length and SHA-256 custody before packaging.
- the Engine projects structured EPUB evidence through its native non-paged
  assembly path, qualifies visuals from publication facts, optionally analyzes
  only unresolved raster images through PP-Structure when the user requests
  it, and invokes `UserVisualAssetWriter`; EPUB spine items and images are never
  represented as physical pages.
- EPUB navigation-table targets can promote publisher-styled paragraphs to
  headings; structured figures, repeated small presentational resources and
  narrowly identified terminal presentation matter are acquired as neutral
  source facts before Engine visual policy.
- native EPUB extraction retains XHTML `aside` containers, including standard
  `epub:type="footnote"` notes, as one ordered text block without duplicating
  nested block content.
- the EPUBCheck report reader materializes only validation messages and skips
  the potentially large package inventory; the five-corpus regression includes
  a 1,181-item spine whose official report is larger than one MiB.
- PdfPig supplies native extraction and native visual measurements.
- `pdftoppm` supplies document-scoped rasterization.
- PP-StructureV3 supplies layout observations when planned.
- source-visual page geometry is retained through authoritative planning;
  every source visual already planned for meaningful preservation supplies one
  preservation crop and one layout-order Figure.
- PP may classify or fragment that source visual, but PP regions never create
  additional visual assets without a corresponding source-visual plan.
- PP `formula` remains a neutral Figure label. When it corresponds to a planned
  source image, the source image is preserved; without a source image, the
  `formula` region is discarded as visual evidence and native text remains.
- PaddleOCR supplies targeted text recognition when planned.
- the Engine returns the portable `DocumentProcessingResult` through the Host.
- publication export is currently a direct `DocumentProcessing.Epub` API and is
  not yet exposed through `DocumentProcessingHost`.

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
