# Document output information boundary V1

## Status

Accepted architecture decision for Phase 19.

This document freezes the information boundary that must guide provenance,
quality observations and the later `DocumentIngestionResult`.

It is intentionally defined before Phase 19A implementation so the engine's
internal object graph does not accidentally become the public contract.

---

## 1. Core rule

The default engine result is not a minimal convenience DTO.

It is a **portable, persistence-ready, custody-complete document result**.

A consumer must be able to discard all DPEngine runtime/internal state after the
call and still retain enough information to:

- persist the processed document in SQL or another datastore;
- build vector-search inputs and downstream retrieval chunks;
- build ordinary lexical/search indexes;
- create citations;
- reconstruct document structure;
- audit where every returned piece of information came from;
- migrate the result into another system without rerunning DPEngine.

DPEngine still does **not** own:

- embeddings;
- vector databases;
- retrieval chunking policy;
- RAG orchestration;
- consumer-specific knowledge semantics;
- persistent application storage.

The engine provides the neutral documentary material and lineage required for
those downstream operations.

---

## 2. Three information boundaries

### 2.1 Default portable result

Always returned.

It contains everything required for normal downstream use, persistence and
chain-of-custody reconstruction.

### 2.2 Optional audit / diagnostic data

Returned only when explicitly requested.

It exists for troubleshooting, evaluation, support, deep forensic inspection or
backend-specific reproduction.

Normal consumers must not depend on it.

### 2.3 Internal processing state

Never part of the public contract.

It exists only to execute the pipeline and may change without compatibility
guarantees.

---

## 3. Custody completeness invariant

For every piece of derived documentary information returned by default, there
must be a deterministic, machine-readable path back to:

```text
derived information
        ↓
derived artifact identity / content hash
        ↓
source element(s)
        ↓
selected source evidence
        ↓
physical page + normalized region
        ↓
source document identity
        ↓
source SHA-256
```

The same derived information must also make the producing processing path
auditable:

```text
derived information
        ↓
processing profile(s)
        ↓
engine / backend / model configuration identity
```

The output must therefore preserve **documentary lineage**, not merely labels
such as `TextOrigin = Ocr`.

---

## 4. Default portable information

The exact `DocumentIngestionResult` type is Phase 20 work, but Phase 19 must
produce information compatible with this boundary.

### 4.1 Source document identity

Default output must contain:

```text
DocumentIdentity
  format
  SHA-256
  byte length
  optional source filename
  optional declared media type
  physical page count
```

The SHA-256 of the original source bytes is the root identity for custody.

Filename and media type are descriptive metadata, not identity keys.

### 4.2 Pages

Default output must preserve:

```text
Page
  stable document-local id
  physical page number
  canonical page coordinate space
  content viewport
  ordered element ids
```

A consumer must be able to locate any returned information on the physical
source page without backend-specific knowledge.

### 4.3 Neutral document elements

Default output must preserve, as applicable:

```text
Element
  stable document-local id
  physical page
  reading order
  normalized bounds
  neutral kind
  selected source text
  normalized final text
  selected text origin
  resolved / unresolved state
  exclusion state/reason
  source evidence reference(s)
  processing provenance
  content hash(es)
```

`selected source text` means the authoritative selected text immediately before
the final document normalization projection. It is not the raw backend payload.

`normalized final text` is the text exposed for downstream document use.

When those texts differ, the output must make that fact observable.

### 4.4 Structural segments

Default output must preserve enough information to build arbitrary downstream
retrieval/index units without DPEngine owning retrieval chunking:

```text
StructuralSegment
  stable document-local id
  ordinal
  normalized text
  content SHA-256
  optional heading
  first physical page
  last physical page
  ordered source element ids
  text origins
  mixed-origin flag
  unresolved-evidence flag
```

This supports:

```text
DPEngine StructuralSegment
        ↓
consumer-specific RetrievalChunk
        ↓
embedding
        ↓
vector store
```

A consumer may combine, split or otherwise transform structural segments, but it
must not have to invent missing source lineage.

### 4.5 Visual assets / references

For a preserved visual, default output must retain at least:

```text
source document SHA-256
physical page
normalized source region
source raster dimensions
pixel crop
media type
content length
content SHA-256
preservation profile
```

Binary visual bytes may remain externally persisted/referenced; the result must
still identify exactly what was preserved.

### 4.6 Processing manifest

The default result must contain a compact deterministic manifest of the
processing identities that produced it.

It is **not** a chronological runtime log.

Expected shape:

```text
ProcessingManifest
  engine/library version

  native extraction
    backend id
    profile id

  rasterization, if used
    backend id
    profile id

  layout analysis, if used
    backend id
    profile id

  OCR, if used
    backend/profile identities actually represented in evidence

  reconciliation
    profile id

  normalization
    profile id

  segmentation
    profile id
```

A versioned `ProfileId` may identify the concrete implementation/model/config
combination without requiring a generic vendor-specific schema.

### 4.7 Provenance

Default provenance must be neutral and consumer-usable.

For a final text element it must be possible to answer:

```text
which source document?
which physical page?
which region?
which source evidence?
native, OCR, merged or none?
which processing backend/profile?
which reconciliation decision?
which normalization changed the selected text?
which structural segment consumed it?
was evidence unresolved or divergent?
```

### 4.8 Content hashes

Default output must include deterministic SHA-256 identities for derived
documentary content where they improve custody.

At minimum:

```text
source document bytes      -> source SHA-256
selected source text       -> text SHA-256 when text exists
normalized element text    -> text SHA-256 when text exists
structural segment text    -> text SHA-256
preserved visual bytes     -> content SHA-256
```

Text hashing must use one explicitly documented encoding. V1 should use UTF-8
without BOM over the exact returned string value.

Hashes prove identity of bytes/strings at each stage; they do not by themselves
constitute a legal digital-signature scheme.

---

## 5. Default vs optional vs internal classification

| Information | Default portable | Optional diagnostic | Internal |
|---|---:|---:|---:|
| source SHA-256 / bytes / format | yes | | |
| physical page number | yes | | |
| canonical normalized bounds | yes | | |
| content viewport | yes | | |
| selected source text | yes | | |
| normalized final text | yes | | |
| neutral element kind | yes | | |
| structural segment text | yes | | |
| segment -> element membership | yes | | |
| native/OCR/merged origin | yes | | |
| reconciliation decision / divergence | yes | | |
| deterministic exclusion reason | yes | | |
| processing backend/profile identity | yes | | |
| deterministic transformation/profile identity | yes | | |
| derived text/content hashes | yes | | |
| preserved visual integrity metadata | yes | | |
| decomposable quality observations | yes | | |
| raw PP-Structure/PaddleX labels | | yes | |
| raw backend response payloads | | yes | |
| individual OCR fragment/polygon dump | | yes | |
| detailed OCR token confidence dump | | yes | |
| page rasters | | yes | |
| OCR/visual temporary crops | | yes | |
| stage timings / retries / HTTP diagnostics | | yes | |
| model memory/benchmark diagnostics | | yes | |
| PdfPig runtime objects | | | yes |
| PaddleX client DTOs | | | yes |
| streams / buffers / temporary paths | | | yes |
| planner intermediate objects | | | yes |
| HTTP request/response objects | | | yes |
| Docker/service process state | | | yes |

---

## 6. Backend-neutral public contract

Backend-specific vocabulary must not leak into the default portable model when a
neutral concept already exists.

Example:

```text
DEFAULT
  LayoutKind = Heading

OPTIONAL DIAGNOSTIC
  RawBackendLabel = "paragraph_title"
```

Therefore raw PP-Structure labels are not part of default provenance.

Backend/profile identity **is** default provenance because reproducibility and
custody require knowing which configured processor produced evidence.

---

## 7. OCR evidence boundary

Default output must preserve enough OCR provenance to explain authoritative OCR
text without exposing the raw OCR backend payload.

Default:

```text
selected OCR-backed source text
selected-source-text SHA-256
source page / normalized region
neutral layout role
OCR backend id
OCR profile id
reconciliation decision
reconciliation equivalence/divergence when applicable
normalized final text + SHA-256
```

Optional diagnostic detail may include:

```text
individual OCR fragments
fragment confidence
fragment polygons/bounds
raw recognizer response
```

Quality integration may expose neutral **aggregated observations** derived from
OCR confidence, but raw fragment dumps are not required for normal consumers.

---

## 8. Normalization custody

If final normalized text differs from the selected source text, default output
must make the transformation auditable.

At minimum:

```text
selected source text
selected source text SHA-256

normalized final text
normalized final text SHA-256

normalization profile id
whether text changed
deterministic transformation evidence when already modeled
```

For current V1 dehyphenation evidence, retain the available deterministic facts
such as soft-hyphen removals and boundary joins when a change occurred.

The public contract should not expose internal normalizer implementation objects.

---

## 9. Quality boundary

Phase 19 quality is public **fact reporting**, not an opaque ranking.

Prefer decomposable observations such as:

```text
unresolved evidence count
deferred evidence count
native/OCR origin distribution
mixed-origin segments
OCR confidence observations
normalization exclusions
preserved visual count
reconciliation divergence/conflict
```

Do not introduce a single unexplained:

```text
QualityScore = 0.87
```

and do not introduce an application-specific:

```text
Admissible = true
```

Consumer policy belongs downstream.

---

## 10. Vector/search downstream invariant

DPEngine does not emit embeddings or retrieval chunks.

However, its default result must be sufficient for a consumer to create them
without re-reading DPEngine internals.

Expected lineage:

```text
Vector record
    ↓
consumer RetrievalChunk
    ↓
DPEngine StructuralSegment(s)
    ↓
DPEngine Element(s)
    ↓
selected evidence
    ↓
page + normalized region
    ↓
source document SHA-256
```

The consumer may add domain metadata at its own layer.

For ApologiaStudio this later means, conceptually:

```text
DocumentIngestionResult
        ↓
KnowledgeImportPackage
        ↓
RetrievalChunk
        ↓
Embedding
        ↓
pgvector
```

Perspective, EvidenceRole and other apologetics/editorial semantics remain
outside DPEngine.

---

## 11. Processing history boundary

The default result carries a **processing manifest**, not an exhaustive event
history.

Default:

- component/backend/profile identities actually used;
- deterministic processing profile identifiers;
- important element-level transformation lineage.

Optional diagnostics:

- stage timing;
- retries;
- HTTP details;
- memory metrics;
- backend warnings;
- detailed execution trace.

This keeps the portable result reproducible without turning it into an
observability/event-log API.

---

## 12. Phase sequencing after this decision

```text
Phase 19.0  output information boundary          THIS DECISION
Phase 19A   custody-complete provenance model    NEXT
Phase 19B   deterministic quality observations
Phase 19C   real-corpus provenance/quality proof

Phase 20    DocumentIngestionResult
Phase 21    deterministic end-to-end orchestrator
Phase 22    consumer integration
```

Phase 19A must be redesigned from this document.

The earlier pre-freeze Phase 19A draft is obsolete and must not be executed.

---

## 13. Acceptance rule for future changes

A proposed field belongs in the default result when removing it would force a
normal consumer to:

- rerun DPEngine;
- inspect internal objects;
- inspect backend-specific payloads;
- lose document structure;
- lose information needed for persistence/indexing/vectorization; or
- lose the ability to reconstruct custody from derived information to the
  cryptographic source identity.

A field belongs in optional diagnostics when it is useful for investigation but
not required for ordinary portable consumption or custody.

A field remains internal when it exists only to implement the processing
algorithm.
