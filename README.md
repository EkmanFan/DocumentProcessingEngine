# Document Processing Engine

Document Processing Engine is a .NET 10 library that transforms source
documents into structured, normalized, traceable, and quality-assessed
`DocumentProcessingResult` instances.

The primary product is the in-process `DocumentProcessingHost` API. The current
runtime supports PDF sources. Its contracts and processing model are
format-neutral so that future formats can supply native evidence and explicit
technical capabilities without taking ownership of the processing policy.

## Current status

- Runtime: .NET 10.
- Supported source format: PDF.
- Native extraction: PdfPig.
- Optional paged enrichment: `pdftoppm`, PP-StructureV3 and PaddleOCR.
- Consumer result: format-neutral `DocumentProcessingResult`.
- Dual Run: non-authoritative evaluation infrastructure; it is not wired into
  the default Host composition.

The project deliberately excludes RAG, embeddings, retrieval chunking, vector
storage, LLM/VLM processing, application-specific concepts and persistent
document storage.

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
- `DocumentProcessing.Core` contains format-neutral contracts and portable
  models.
- layout and OCR providers execute technical operations; they do not decide
  when those operations are required.

Dependency direction:

```text
DocumentProcessing.Core
        ↑
        ├── DocumentProcessing.Engine
        └── DocumentProcessing.Pdf

DocumentProcessing
        └── Core + Engine + Pdf
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
  DocumentProcessing/               consumer-facing Host and composition root
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

## Consumer entry point

Consumers configure the shared providers once and process a `DocumentSource`
through `DocumentProcessingHost`:

```csharp
using DocumentProcessing;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;

var options = new DocumentProcessingHostOptions(
    engineVersion: "my-application-v1",
    ppStructureV3: new PpStructureV3Options(
        new Uri("http://localhost:8080")),
    paddleOcr: new PaddleOcrOptions(
        new Uri("http://localhost:8081"),
        profileId: "paddleocr-profile-v1"));

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
