# Document Processing Engine

Document Processing Engine is a .NET 10 library that transforms source
documents into structured, normalized, traceable, and quality-assessed
`DocumentProcessingResult` instances.

The primary product is the in-process `DocumentProcessingHost` API. The current
runtime supports PDF and EPUB sources. Its contracts and processing model are
format-neutral so that additional formats can supply native evidence and
explicit technical capabilities without taking ownership of the processing
policy.

## Current status

- Runtime: .NET 10.
- Supported source formats: PDF and EPUB.
- Native extraction: PdfPig.
- Optional paged enrichment: `pdftoppm`, PP-StructureV3 and PaddleOCR.
- Provider lifecycle: PP-StructureV3 and PaddleOCR start lazily through the Host
  by default; external endpoint ownership remains configurable.
- Consumer result: format-neutral `DocumentProcessingResult`.
- Dual Run: non-authoritative evaluation infrastructure; it is not wired into
  the default Host composition.
- Manager foundation: durable lifecycle semantics, globally leased sequential
  dispatch and hexagonal ports.
- Manager persistence: versioned PostgreSQL state, runtime lease, fenced global
  queue, durable workshop settings, immutable submission/result manifests and
  append-only custody events.
- Source custody: exact bytes are retained through a content-addressed SHA-256
  filesystem adapter and verified before reading.
- Managed execution V1: `WholeDocument` and PDF `PageRange` results run through the Host, are
  retained as verified content-addressed JSON and are registered idempotently
  in PostgreSQL. Caller-owned visual bytes are staged, checked against the
  Engine result and atomically published in one subdirectory per processing
  unit. A page-range result preserves the complete source physical-page count
  and the exact source-relative processed selection without renumbering pages.
- Manager Host: a key-protected ASP.NET Core process composes schema migration,
  source/result custody, sequential background execution, lifecycle commands,
  submission, queue observation/reordering and result retrieval.
- Manager workshop: a server-side Blazor adapter presents pending, active and
  completed work with lifecycle controls while retaining the Manager API key
  outside the browser. Its reusable sprite-animated librarian reflects waiting,
  reading, paused, stopped and unavailable states and celebrates newly
  completed work. English and French presentation follows the embedding
  application's ambient .NET culture, with English as the standalone default.
  Its persistent animation stage also accepts streamed PDF/EPUB submissions;
  immutable custody and queue registration remain owned by the Manager Host.
  New documents are shelved by default, can be released explicitly or marked
  ready at reception, and pending units can be reordered across documents by
  buttons or drag and drop. A shared visual splitter creates validated,
  non-overlapping PDF page-range units from an existing pending item, an upload
  suggestion or an explicit global split action. Successful retained JSON results can be downloaded
  through the authenticated server-side workshop circuit. A settings dialog
  persists the reception default and validates the filesystem destination used
  for completed visual assets. The three workshop columns remain viewport-bound;
  recent results scroll independently, while a configurable retention window
  exposes older terminal work through a paged, filtered archive dialog.

The processing library deliberately excludes RAG, embeddings, retrieval
chunking, vector storage, LLM/VLM processing, application-specific concepts
and persistent document storage. The separate Manager bounded context owns
durable execution orchestration without moving those concerns into the Engine.

## Run the Manager locally

Start or reuse the development PostgreSQL container, then launch the Manager
Host and its Blazor workshop with one command:

```bash
./scripts/run-manager-dev.sh
```

Open `http://127.0.0.1:5092` and press `Ctrl+C` when finished. The Host and UI
are stopped together; the PostgreSQL container and its named volume remain
available for the next run. The launcher creates a `visuals` directory below
the development custody root and prints the path to select in Manager Settings.
For local testing it also enables the confirmed **Delete permanently** action;
production configuration keeps that custody purge disabled by default until
administrator authorization is available.
The launcher also registers the local Apologia Studio callback. When a result
becomes durable, the Manager wakes Apologia on port `5090`; failed callbacks are
retried and a five-minute reconciliation remains as a safety net.
`DPE_MANAGER_*` environment variables can override the development ports,
credentials, API key, container names and custody root.

## Architecture

The Engine owns the universal processing cycle:

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

Ownership is intentionally separated:

- `DocumentProcessingHost` owns the public facade, configuration, composition
  and lifecycle.
- `DocumentProcessing.Engine` owns format selection and all processing policy.
- `DocumentProcessing.Pdf` understands PDF representation and exposes native
  evidence, rasterization and native visual observations.
- `DocumentProcessing.Epub` understands EPUB representation, owns the EPUBCheck
  boundary and can write a reflowable EPUB from a completed portable result.
- `DocumentProcessing.Core` contains format-neutral contracts and portable
  models.
- `DocumentProcessing.Layout.Adapters` and `DocumentProcessing.Ocr.Adapters`
  implement Core capability ports and translate between provider-specific
  protocols/results and neutral Core evidence.
- concrete layout and OCR provider clients execute technical operations; they
  do not decide when those operations are required.

Dependency direction:

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
```

See [Current architecture](docs/architecture/current-architecture.md) for the
active invariants and [Documentation guide](docs/README.md) for the status of
the historical design and evaluation records.

## Solution structure

```text
src/
  DocumentProcessing.Core/          neutral contracts and portable models
  DocumentProcessing.Engine/        assessment, planning and processing policy
  DocumentProcessing.Pdf/           PDF acquisition and technical capabilities
  DocumentProcessing.Epub/          EPUB acquisition, validation and export
  DocumentProcessing.Layout.Adapters/ provider-specific layout translation/client
  DocumentProcessing.Ocr.Adapters/  provider-specific OCR translation/client
  DocumentProcessing/               consumer-facing Host and composition root
  DocumentProcessing.Manager/       queue/control/runtime core and hexagonal ports
  DocumentProcessing.Manager.Persistence/ PostgreSQL and storage adapters
  DocumentProcessing.Manager.DPEngine/ Manager-to-Host execution adapter
  DocumentProcessing.Manager.Host/  executable Manager API and composition root
  DocumentProcessing.Manager.Blazor/ server-side Manager workshop UI adapter
  DocumentProcessing.DualRunWorker/ isolated non-authoritative worker

tests/
  DocumentProcessing.UnitTests/
  DocumentProcessing.IntegrationTests/

tools/
  DocumentProcessing.EvaluationCli/ deterministic evaluation tooling

docs/                               architecture, contracts and evidence records
scripts/                            deterministic validation/evaluation workflows
```

## Build and test

Prerequisites for the deterministic .NET regression are the .NET 10 SDK and
the native tools exercised by the relevant tests.

```bash
dotnet build DocumentProcessingEngine.sln -c Release --warnaserror
dotnet test DocumentProcessingEngine.sln -c Release --no-build
```

The PostgreSQL Manager integration tests are environment-gated so the ordinary
deterministic regression does not require Docker or a database server. Run them
against an isolated initialized test database with:

```bash
export DOCUMENT_PROCESSING_MANAGER_POSTGRES_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=dpengine_manager_test;Username=postgres;Password=...'

dotnet test tests/DocumentProcessing.IntegrationTests \
  -c Release \
  --filter FullyQualifiedName~DocumentProcessing.IntegrationTests.Manager
```

The tests apply the Manager schema themselves and reset only objects inside the
dedicated `document_processing_manager` schema. Never point them at a database
containing retained Manager data.

Real layout/OCR semantic scripts have additional Docker, model-cache, memory
and corpus prerequisites. They are evidence workflows, not substitutes for the
deterministic .NET regression. See `scripts/run-semantic-*-regression.sh` and
the corresponding records under `docs/evaluation/`. Local copyrighted test
documents must follow the
[test-document layout](docs/evaluation/local-fixture-layout.md).

The first EPUB reference can be checked independently of the Engine with
`scripts/run-epub-reference-validation.sh`. It uses the official EPUBCheck tool
plus exact Habermas source and p18/p28 observations.

The EPUB-1 production path is documented in
[`docs/epub/epub-1-validation-boundary-v1.md`](docs/epub/epub-1-validation-boundary-v1.md).
It adds the pinned EPUBCheck runtime boundary, consumer-safe failure mapping,
native reflowable acquisition and non-paged portable projection. The Habermas
result is reproduced by `scripts/run-epub-native-regression.sh`.

EPUB-2 adds deterministic image discovery and exact packaged-byte preservation
without inventing page geometry. Its policy and Habermas evidence are
documented in
[`docs/epub/epub-2-visual-preservation-v1.md`](docs/epub/epub-2-visual-preservation-v1.md)
and reproduced by `scripts/run-epub-visual-regression.sh`.

EPUB-3 qualifies body images through deterministic EPUB landmarks and invokes
Paddle only for unresolved images when the user explicitly enables the
per-request fallback. See
[`docs/epub/epub-3-visual-qualification-v1.md`](docs/epub/epub-3-visual-qualification-v1.md).

EPUB-4 hardens structural extraction across the Habermas, Calvin and Bauckham
corpora by using EPUB navigation targets for headings and local XHTML context
for visual qualification. See
[`docs/epub/epub-4-multi-corpus-hardening-v1.md`](docs/epub/epub-4-multi-corpus-hardening-v1.md).

EPUB-5 validates the V1 boundary on the structurally different Brenner and
Septante corpora, including a large conformant EPUB with 1,181 spine items.
See
[`docs/epub/epub-5-expanded-corpus-validation-v1.md`](docs/epub/epub-5-expanded-corpus-validation-v1.md).

The Habermas PDF/EPUB comparison subsequently identified standard XHTML
`aside epub:type="footnote"` containers omitted by native profile V2. Profile
V3 retains those notes exactly once, with cross-format and multi-corpus evidence
in
[`docs/evaluation/habermas-pdf-epub-text-comparison-v1.md`](docs/evaluation/habermas-pdf-epub-text-comparison-v1.md).

Native profile V4 concludes unambiguous XHTML note relations from
`epub:type="noteref"`, annotated payloads and equivalent ARIA roles, including
cross-resource references. The Engine projects them through the portable
`DocumentNote`/`Notes` contract and excludes identified note payloads from the
narrative flow. Explicit or reciprocally identified note payloads remain
auditable elements outside narrative segments even when their relation is not
resolved; no portable `DocumentNote` is invented for an unresolved relation.

The first reflowable publication-export increment lives in
`DocumentProcessing.Epub`. `EpubPublicationExporter` consumes the canonical
`DocumentProcessingResult`; the caller supplies publication metadata and a
reader for the caller-owned visual bytes. The exporter verifies every visual
against the result before packaging it. Current scope and the first Ehrman
experiment are documented in
[`docs/epub/epub-publication-export-prototype-v1.md`](docs/epub/epub-publication-export-prototype-v1.md).

## Consumer entry point

Consumers configure the shared providers once and process a `DocumentSource`
through `DocumentProcessingHost`:

```csharp
using DocumentProcessing;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;

var options = new DocumentProcessingHostOptions(
    engineVersion: "my-application-v1",
    ppStructureV3: new PpStructureV3Options(
        new Uri("http://localhost:8080/layout-parsing")),
    paddleOcr: new PaddleOcrOptions(
        new Uri("http://localhost:8081/ocr"),
        profileId: "paddleocr-3.7.0-ppocrv6-medium-cpu-v1"));

using var host = new DocumentProcessingHost(options);
await using var content = File.OpenRead("document.pdf");

var outcome = await host.ProcessDocumentAsync(
    new DocumentSource(
        content,
        fileName: "document.pdf",
        declaredMediaType: "application/pdf"));
```

Unsupported or ambiguous formats produce a functional failure outcome.
Technical failures and cancellation remain exceptions.

## Commit workflow

The repository helper validates whitespace, builds, runs the full regression,
stages, commits and optionally pushes. Project convention is to keep local
`main` synchronized with `origin/main`:

```bash
./scripts/commit-document-processing.sh --push "commit message"
```
