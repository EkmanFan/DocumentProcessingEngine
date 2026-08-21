# Target architecture reference V1

## Status

**PASS — frozen acceptance evidence**

This document is pinned to the implementation commit recorded below. It is not
a description of every later code-level change. The responsibility boundaries
remain active and are summarized in the
[Current architecture](../architecture/current-architecture.md).

Implementation commit:

```text
fd48b968f7cdfe726d9dc74ca98592ad26e19694
```

Normative architecture source:

```text
DPEngine - Architecture Cible - 2026-08-20.md
SHA-256: 3f564791c4a3867ad938a6cdc6c9de3c7b5a32de7d5c3650daadaf0ac31143e3
```

Validated gate:

```text
scripts/tmp/dpengine-target-cutover-step-4-4-final-architecture-gate.sh
SHA-256: 82c28c0fe8b0ea24bc36a40f58b58a77220bf3b8b5bd9bd0fee673d77d46ba4d
TARGET ARCHITECTURE GATE: PASS
```

This document freezes the accepted architecture of the production
DocumentProcessingEngine path at the implementation commit above.

## Frozen production shape

```text
Consumer
   ↓
DocumentProcessingHost
   ↓
DocumentProcessingEngine
   ↓
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

## Frozen responsibility boundaries

### Host

```text
public API
configuration
composition
lifecycle
owns/calls Engine
```

The Host does not own document-format selection, processing strategy,
layout/OCR decisions, reconciliation, assembly, or quality policy.

### Engine

The Engine owns the universal document-processing cycle:

```text
ACQUIRE
ASSESS
PLAN
ENRICH
RECONCILE
ASSEMBLE
QUALITY
```

It owns document-format selection and decides when format-specific or shared
capabilities must be used.

### Format

A format implementation understands its representation and provides native
evidence plus explicit technical capabilities that exist because of that
representation.

The root `IDocumentFormat` contract remains minimal:

```text
Format
TryExtractNativeEvidenceAsync(...)
```

A format does not own the global document-processing algorithm.

### Shared capabilities

Layout and OCR are technical capabilities independent of document format.
They perform operations; they do not decide why or when those operations are
required.

### Core

`DocumentProcessing.Core` contains neutral contracts and portable/shared
models. It does not depend on Engine, PDF, or provider-specific
implementations.

## Frozen assembly direction

```text
DocumentProcessing.Core
        ↑
        ├── DocumentProcessing.Engine
        └── DocumentProcessing.Pdf

DocumentProcessing
        └── Core + Engine + Pdf
```

Validated constraints:

```text
Engine → concrete PDF dependency    ABSENT
Pdf → Engine dependency             ABSENT
Core → implementation dependency    ABSENT
```

## Removed compatibility architecture

The following transition architecture is explicitly absent from the frozen
reference:

```text
IDocumentFormatProcessor
DocumentFormatProcessorResolver
PdfDocumentFormatProcessor
PdfDocumentFormatProcessorComposition
DocumentFormatProcessingBinding
DocumentProcessingAttemptResult
PdfDocumentExecution
PdfDocumentProcessingResultAdapter
DocumentProcessorFactory.CreateHybrid
parameterless DocumentProcessingEngine compatibility construction
```

These names must not be reintroduced as a shortcut around the Engine-owned
processing cycle without an explicit architecture decision that supersedes
this reference.

## Acceptance evidence

Final architecture gate:

```text
assembly dependency direction              PASS
Host facade/composition/lifecycle           PASS
minimal root format contract                PASS
Engine universal orchestration ownership    PASS
format facts/capabilities boundary          PASS
obsolete format→processor path absent       PASS
source-level integrity                      PASS
Release build --warnaserror                 PASS
focused target-architecture regression      23/23 PASS
full deterministic regression               727/727 PASS
```

The gate completed without modifying any tracked repository file.

## What this reference freezes

Commit `fd48b968f7cdfe726d9dc74ca98592ad26e19694` is the architecture reference for:

```text
Host → Engine production path
Engine-owned document-format selection
Engine-owned universal processing decisions
minimal IDocumentFormat acquisition contract
format-specific technical capability ownership
shared layout/OCR capability boundary
neutral Core dependency direction
portable DocumentProcessingResult production
absence of format→complete-processor architecture
```

Future refactoring and housekeeping must preserve these boundaries unless an
explicitly reviewed architecture change intentionally supersedes this
reference.

## Explicitly not frozen as complete

This reference does **not** freeze:

```text
internal class names where responsibility remains unchanged
the final shape of NativeDocumentEvidence
future EPUB/DOC/DOCX/ODT/PPT/PPTX capability contracts
provider lifecycle/performance optimizations
implementation-level code organization that does not change ownership
```

It also does not prohibit simplification. Dead code, naming cleanup, file
organization, regions, comments, and internal refactoring may change freely
when the frozen responsibility boundaries remain intact.

## Authority

The normative architecture remains:

```text
DPEngine - Architecture Cible - 2026-08-20.md
```

This frozen reference is implementation evidence that commit
`fd48b968f7cdfe726d9dc74ca98592ad26e19694` satisfies that architecture. It does not replace the
normative source.
