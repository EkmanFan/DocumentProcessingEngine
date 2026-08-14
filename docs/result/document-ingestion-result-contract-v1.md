# DocumentIngestionResult contract and serialization policy V1

## Status

Accepted architecture decision for **Phase 20.0**.

This freezes how the future `DocumentIngestionResult` is exposed and transported
before Phase 20A implements the public model.

It builds on the Phase 19 output boundary, custody-complete provenance,
deterministic quality observations, and the real-corpus Phase 19C proof.

This decision does **not** implement `DocumentIngestionResult`.

---

## 1. Decision summary

V1 uses two complementary representations:

```text
CANONICAL IN-MEMORY CONTRACT
        │
        ▼
DocumentIngestionResult
strongly typed immutable/read-only .NET model
        │
        ├──────────────────────────┐
        │                          │
        ▼                          ▼
direct .NET consumption       portable serialization
                                   │
                                   ▼
                                 JSON V1
```

| Concern | V1 decision |
|---|---|
| Canonical contract | strongly typed .NET `DocumentIngestionResult` |
| Normal .NET-to-.NET use | pass the object directly |
| Portable representation | JSON |
| JSON implementation | `System.Text.Json` |
| Schema version | mandatory root field |
| Schema identifier | `document-ingestion-result-v1` |
| JSON property names | camelCase |
| Enum representation | camelCase strings |
| Optional null values | omitted when written |
| Collections | present and non-null; empty as `[]` |
| Unknown JSON properties | V1 readers tolerate/ignore |
| Unsupported schema version | explicit failure |
| XML | not supported in V1 |
| Protobuf / MessagePack | not supported in V1 |
| Raw internal objects | never serialized as public API |
| Visual binary bytes | not inline in default JSON |
| Diagnostics | separate optional boundary |
| Embeddings / vector records | outside DPEngine result |
| Serialized JSON bytes | not a custody identity |
| Field-level documentary hashes | remain authoritative |

> **Class first, JSON second.**

The .NET model defines the documentary contract. JSON is its official portable
transport/persistence representation.

---

## 2. Canonical contract: the .NET model

DPEngine is first a .NET library.

A normal in-process consumer uses:

```text
consumer
   │
   ▼
DPEngine
   │
   ▼
DocumentIngestionResult
```

It must not require an artificial:

```text
object -> JSON -> object
```

round trip in the same process.

The future operation therefore returns a strongly typed result conceptually
equivalent to:

```csharp
Task<DocumentIngestionResult>
```

The exact orchestrator API remains Phase 21 work.

---

## 3. Portable V1 representation: JSON

JSON is the official portable representation because it supports the current
requirements with one compatibility surface:

- persistence of a completed result;
- regression fixtures;
- offline inspection;
- process-boundary transport;
- future HTTP APIs;
- non-.NET consumers;
- migration without rerunning DPEngine.

JSON is a representation of the portable contract, not the internal processing
model.

Consumers must not depend on CLR namespaces, assembly names, constructor shape,
or private implementation details.

---

## 4. XML is intentionally not supported in V1

XML is not rejected technically. It has no demonstrated consumer requirement.

Adding it now would create a second naming, null, enum, versioning, compatibility
and test surface without changing a real product decision.

If an XML consumer appears later, XML may be implemented as an adapter over the
canonical `DocumentIngestionResult`.

The engine must not be designed around hypothetical XML requirements.

---

## 5. Internal processing state is never the transport contract

Forbidden:

```text
internal processing graph
        ↓
generic serializer
        ↓
public contract by accident
```

Internal/runtime data remains internal, including:

- PdfPig runtime objects;
- HTTP request/response DTOs;
- PaddleX / PP-Structure wire payloads;
- raw OCR service payloads;
- planner/rasterization intermediates;
- streams and buffers;
- temporary paths;
- Docker/service state;
- retry/timing state.

`DocumentIngestionResult` is a deliberate public projection.

---

## 6. Single-authoritative-representation rule

Phase 20 must not create duplicated serialized truth.

For example, this is forbidden if both copies serialize independently:

```text
DocumentIngestionResult.Elements[]
        +
DocumentIngestionResult.Provenance.Elements[]
```

Likewise:

```text
DocumentIngestionResult.Source
        +
DocumentIngestionResult.Provenance.Source
```

must not become two independent serialized copies that can diverge.

The rule is:

> **one serialized authoritative location for each documentary fact.**

Read-only .NET convenience projections may exist only when deterministically
derived from the authoritative representation and omitted from JSON.

This rule is critical to custody integrity.

---

## 7. Minimum semantic root

Phase 20A decides the exact C# type decomposition.

The required semantic capabilities are already frozen:

```text
DocumentIngestionResult
  SchemaVersion

  Source
  Pages
  Elements
  StructuralSegments
  QualityObservations
  ProcessingManifest

  PreservedVisualReferences
    where not already represented without duplication
```

These are semantic requirements, not final property names.

A consumer must be able to discard all DPEngine runtime state and still:

- persist the result;
- build search/index inputs;
- build downstream retrieval chunks;
- build citations;
- reconstruct structure;
- audit custody;
- migrate the result.

DPEngine still does not own retrieval chunk policy, embeddings, vector storage,
RAG orchestration, or consumer-specific knowledge semantics.

---

## 8. Schema version

Every JSON document carries:

```json
{
  "schemaVersion": "document-ingestion-result-v1"
}
```

`schemaVersion` describes the portable data contract.

It is distinct from the engine/library version in the processing manifest.

```text
schemaVersion
  document-ingestion-result-v1

engineVersion
  package version / git version / release version
```

An engine release does not automatically imply a schema change.

---

## 9. Schema evolution

### Compatible V1 change

A V1 writer may add an optional field only when:

- existing required semantics remain unchanged;
- absence has defined semantics;
- old readers can safely ignore the new field;
- no existing field changes JSON type or meaning.

V1 readers therefore tolerate unknown properties.

### Breaking change

A new schema identifier is required when a change:

- removes a required field;
- changes existing semantics;
- changes a field type incompatibly;
- changes custody/identity semantics;
- changes required membership/reference semantics;
- makes an old V1 document unsafe or ambiguous as V1.

Example:

```text
document-ingestion-result-v2
```

Unsupported schema versions fail explicitly. They are never silently interpreted
as the current schema.

---

## 10. JSON naming

V1 JSON property names are camelCase.

```json
{
  "schemaVersion": "document-ingestion-result-v1",
  "physicalPageCount": 617,
  "selectedSourceText": "example",
  "normalizationChangedText": true
}
```

CLR/PascalCase names are implementation details.

Phase 20C must lock the JSON contract with tests rather than depending on
accidental serializer defaults.

---

## 11. Enum representation

Enums serialize as descriptive camelCase strings.

Correct:

```json
{
  "textOrigin": "nativePdf"
}
```

Not V1:

```json
{
  "textOrigin": 1
}
```

Numeric enum values are coupled to CLR declaration order and are not a stable
portable contract.

Unknown required enum semantics fail clearly.

---

## 12. Null and collection policy

Required objects are non-null.

Collections are non-null.

Empty collections are represented as:

```json
[]
```

Optional values without a value are omitted by the official writer.

The contract must not require a consumer to distinguish `null` from `[]` for a
collection.

Phase 20C tests these semantics explicitly.

---

## 13. Encoding and numbers

The official portable representation is UTF-8 JSON.

Documentary strings remain the exact strings represented by the result model.
Serialization does not introduce hidden text normalization.

Coordinates and other numeric values remain JSON numbers with their existing
model semantics.

Non-finite values are invalid V1 output:

```text
NaN
Infinity
-Infinity
```

---

## 14. Serialized JSON bytes are not custody identity

V1 does not define canonical JSON bytes.

The following are transport formatting, not documentary identity:

- whitespace;
- indentation;
- object property order;
- serializer formatting choices.

Therefore DPEngine must not use:

```text
SHA256(serialized DocumentIngestionResult JSON)
```

as documentary custody identity.

Existing hashes remain authoritative:

```text
source document bytes   -> source SHA-256
selected source text    -> selected text SHA-256
normalized text         -> normalized text SHA-256
segment text            -> segment SHA-256
preserved visual bytes  -> visual content SHA-256
```

An external storage system may add a transport-file checksum independently.

---

## 15. Visual binary policy

Default JSON does not inline preserved visual bytes as Base64.

Forbidden default shape:

```json
{
  "contentBase64": "iVBORw0KGgo..."
}
```

The portable result retains the already-proven integrity/location metadata:

```text
source document SHA-256
physical page
normalized source region
source raster dimensions
pixel crop
media type
content length
content SHA-256
preservation profile/reference
```

Actual binary retrieval/persistence remains a separate concern.

Phase 20A may add a stable visual asset/reference identifier if needed.

---

## 16. Diagnostics remain separate

JSON availability does not turn diagnostics into default result data.

Optional diagnostics may later contain:

- raw OCR fragments and polygons;
- raw backend labels/responses;
- timings and retries;
- model/service metrics;
- raster/crop bytes;
- execution traces.

If diagnostics receive serialization later, that is a separate explicit
contract. Normal consumers do not depend on it.

---

## 17. Deserialized JSON is untrusted

Deserialization must not bypass the invariants already established by Phase 19.

A reader validates at least:

- supported schema version;
- source SHA format;
- unique IDs;
- page/reference integrity;
- segment/element bidirectional membership;
- text/hash consistency;
- quality source identity;
- enum values;
- required collections;
- geometry/count invariants where applicable.

A reader either produces a valid `DocumentIngestionResult` or fails.

It must not return a partially trusted graph.

---

## 18. Determinism

For identical completed evidence and processing profiles,
`DocumentIngestionResult` is semantically deterministic.

Avoid nondeterministic default fields such as:

- current timestamps;
- random GUIDs;
- machine-local temp paths;
- process IDs;
- container IDs.

Runtime timestamps/metrics, if ever required, belong to optional diagnostics.

The writer should produce stable JSON for regression fixtures, but byte-for-byte
JSON stability is not elevated to a custody requirement.

---

## 19. Direct .NET use versus JSON use

### Same-process .NET

```text
DPEngine
  ↓
DocumentIngestionResult
  ↓
consumer adapter
```

### Persistence / process boundary / interoperability

```text
DocumentIngestionResult
  ↓
official System.Text.Json serializer
  ↓
JSON V1
```

### ApologiaStudio target boundary

```text
Document
   ↓
DPEngine
   ↓
DocumentIngestionResult
   ↓
ApologiaStudio adapter
   ↓
KnowledgeImportPackage
   ↓
consumer retrieval chunks
   ↓
embeddings
   ↓
pgvector
```

No JSON round trip is required merely because ApologiaStudio and DPEngine use
the same process and .NET runtime.

---

## 20. Phase 20C serializer policy

Phase 20C provides one official serialization boundary.

Conceptually:

```text
DocumentIngestionResultJson
  Serialize(...)
  Deserialize(...)
```

The exact type/API name is not frozen here.

Its explicit `System.Text.Json` policy must implement:

```text
property naming       camelCase
enum values           camelCase strings
optional nulls        omitted
collections           never null
unknown properties    tolerated by V1 reader
unsupported schema    explicit failure
non-finite numbers    rejected
reference cycles      not part of contract
```

Do not expose a mutable global `JsonSerializerOptions` object as the public
contract.

Consumers must not need to reconstruct the official options manually.

---

## 21. Acceptance criteria for Phase 20A–20D

### 20A — `DocumentIngestionResult` model

Must:

- introduce a strongly typed immutable/read-only public result;
- enforce one authoritative serialized location per documentary fact;
- use the proven Phase 19 information boundary;
- avoid JSON-specific implementation leakage where possible;
- contain no retrieval chunks, embeddings or consumer semantics.

### 20B — deterministic result projection

Must:

- project completed DPEngine evidence into the result;
- fail closed on inconsistent provenance/quality membership;
- preserve Phase 19 custody and quality invariants;
- exclude backend/internal payloads.

### 20C — JSON contract and round-trip tests

Must test:

```text
result -> JSON -> result
```

for semantic equality and invariants.

Also test:

- mandatory schema version;
- unsupported schema rejection;
- camelCase property names;
- string enums;
- null omission;
- empty collection representation;
- unknown-property tolerance;
- invalid/dangling custody rejection;
- text/hash consistency after round trip;
- no visual Base64 payload;
- no internal/runtime fields;
- no numeric enums.

### 20D — real-corpus serialized-result proof

Must prove on representative real documents:

- source identity preserved;
- page/element/segment structure preserved;
- visual custody metadata preserved;
- native/OCR origins preserved;
- reconciliation conflict preserved;
- quality observations preserved;
- processing manifest preserved;
- no internal/runtime dependency is required after deserialization.

---

## 22. Roadmap

```text
19.0  output information boundary                 DONE
19A   custody-complete provenance                  DONE
19B   deterministic quality observations           DONE
19C   real-corpus provenance/quality proof         DONE

20.0  result contract + serialization policy       THIS DECISION
20A   DocumentIngestionResult model                 NEXT
20B   deterministic result projection
20C   JSON contract + round-trip tests
20D   real-corpus serialized result proof

21    deterministic end-to-end ingestion orchestrator
22    ApologiaStudio consumer integration
```

---

## 23. Final V1 rule

A normal consumer can choose either:

```text
direct strongly typed .NET result
```

or:

```text
portable JSON V1
```

without losing document structure, quality facts, or chain of custody.

The transport format must not become the domain model.

The internal processing model must not become the transport format.

`DocumentIngestionResult` sits deliberately between them.
