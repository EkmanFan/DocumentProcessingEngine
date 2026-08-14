# DocumentIngestionResult JSON contract V1

## Status

Phase 20C implementation.

Phase 20.0 froze the transport policy.

Phase 20A introduced the strongly typed result.

Phase 20B introduced deterministic projection.

Phase 20C now introduces the official UTF-8 JSON V1 representation and
round-trip validation.

---

## 1. Public API

The official boundary is:

```text
DocumentIngestionResultJson
  SerializeToUtf8Bytes(DocumentIngestionResult)
  Deserialize(ReadOnlySpan<byte>)
```

The serializer lives in:

```text
DocumentProcessing.Core.Results.Serialization
```

Normal in-process .NET consumers still use `DocumentIngestionResult` directly.

JSON is for persistence, process boundaries and interoperability.

---

## 2. Explicit transport contract

The domain model is intentionally **not** passed directly to
`JsonSerializer`.

Instead:

```text
DocumentIngestionResult
        │
        ▼
internal explicit JSON V1 contract DTOs
        │
        ▼
System.Text.Json
```

and on read:

```text
untrusted UTF-8 JSON
        │
        ▼
internal JSON V1 contract DTOs
        │
        ▼
validated public constructors
        │
        ▼
DocumentIngestionResult
```

This keeps the transport format separate from the domain model.

Every JSON property name is fixed with an explicit
`JsonPropertyName` on internal transport DTOs.

Renaming a CLR/domain property therefore does not silently rename the portable
wire contract.

No JSON attributes are added to `DocumentIngestionResult` or the Phase 19
portable domain records.

---

## 3. Root shape

V1 root properties are:

```json
{
  "schemaVersion": "document-ingestion-result-v1",
  "source": {},
  "processingManifest": {},
  "pages": [],
  "elements": [],
  "structuralSegments": [],
  "qualityObservations": {}
}
```

All root properties are required.

`schemaVersion` must equal:

```text
document-ingestion-result-v1
```

A different declared schema raises
`UnsupportedDocumentIngestionResultSchemaException`.

A missing required property raises `JsonException`.

---

## 4. Source format representation

`DocumentFormatId` is represented as a scalar string:

```json
{
  "source": {
    "format": "pdf"
  }
}
```

It is intentionally not serialized as the CLR value-object shape:

```json
{
  "format": {
    "value": "pdf"
  }
}
```

The portable contract expresses the semantic format identifier, not its .NET
wrapper implementation.

---

## 5. Enum representation

All enum-valued documentary fields use exact camelCase strings.

Examples:

```json
{
  "kind": "heading",
  "textOrigin": "nativePdf",
  "layoutKind": "table",
  "reconciliationDecision": "agreement",
  "exclusionReason": "repeatedHeader"
}
```

Numeric enum values are rejected.

Unknown enum strings are rejected.

Case variants are not accepted as aliases; the V1 representation is exact.

---

## 6. Optional values and collections

Optional null values are omitted on write.

For example a native-only element does not emit:

```text
ocrBackendId
ocrProfileId
reconciliationDecision
```

when those values are absent.

Required collections are always emitted, including when empty:

```json
{
  "ocr": [],
  "visualPreservationProfileIds": [],
  "ocrConfidenceObservations": []
}
```

A required collection explicitly set to JSON `null` is rejected.

---

## 7. Derived convenience properties are omitted

Phase 20.0 requires one serialized authoritative location for each documentary
fact.

Therefore derived .NET convenience properties are deliberately absent from JSON.

Examples:

```text
DocumentIngestionPage.PageId
  derived from PhysicalPageNumber
  -> omitted

DocumentElementProvenance.IsExcluded
  derived from ExclusionReason
  -> omitted

DocumentSegmentProvenance.IsMixedTextOrigin
  derived from TextOrigins
  -> omitted

PixelRectangle.Width / Height
  derived from Left/Top/Right/Bottom
  -> omitted
```

The portable JSON contains the authoritative values required to recompute these
properties.

---

## 8. Visual policy

Preserved visual metadata remains nested on the authoritative element:

```text
preservedVisual
  profileId
  mediaType
  sourceRasterPixelWidth
  sourceRasterPixelHeight
  crop
    left
    top
    right
    bottom
  contentLength
  contentSha256
```

No binary content is embedded.

V1 does not emit `contentBase64`.

---

## 9. Reader security and strictness

Input JSON is untrusted.

The official options are internal and not exposed for consumer mutation.

V1 reader behavior:

```text
duplicate property names          rejected
trailing commas                   rejected
comments                          rejected
non-finite numeric literals       rejected
property-name casing              exact
unknown properties                tolerated
required properties               enforced
nullable annotations              respected
maximum JSON depth                64
```

Unknown properties are tolerated specifically to permit compatible additive V1
evolution.

Duplicate properties are rejected to avoid last-value-wins ambiguity.

---

## 10. Domain invariants remain authoritative

The JSON DTO layer does not attempt to replace domain validation.

After structural JSON parsing, it reconstructs:

```text
DocumentSourceIdentity
DocumentProcessingManifest
DocumentIngestionPage
DocumentElementProvenance
DocumentSegmentProvenance
DocumentIngestionQualityObservations
DocumentIngestionResult
```

through their normal constructors.

Therefore tampered input still encounters the existing custody and graph
invariants, including:

```text
SHA-256 / exact text consistency
source identity consistency
page membership
segment membership
segment page span
OCR/layout/reconciliation manifest identity
visual profile identity
OCR confidence attachment
geometry/count validation
```

Domain/invariant failures caused by input JSON are surfaced as `JsonException`
with the original invariant exception retained as `InnerException`.

No partially trusted result object is returned.

---

## 11. Stable output versus custody identity

For the same valid `DocumentIngestionResult`, the V1 writer produces stable
compact UTF-8 JSON suitable for regression fixtures.

Round-trip is tested as:

```text
result
  -> UTF-8 JSON
  -> result
  -> UTF-8 JSON
```

with:

```text
semantic field/sequence equality
+
identical emitted bytes
```

The semantic comparison is intentionally structural. Record equality is not
used as a shortcut for aggregates that contain `IReadOnlyList` values, because
their generated record equality follows the collection object's equality
semantics rather than sequence contents.

This stability is a serializer regression property only.

The JSON bytes are still **not** documentary custody identity.

Authoritative custody remains:

```text
source SHA-256
selected text SHA-256
normalized text SHA-256
segment text SHA-256
preserved visual content SHA-256
```

---

## 12. No-go fields

The V1 portable result does not serialize:

```text
raw backend responses
raw backend labels
temporary paths
Docker/service state
retry state
timings
raster/crop binary bytes
retrieval chunks
embeddings
vector records
consumer-specific semantics
```

Those remain internal, diagnostic or downstream concerns.

---

## 13. Tests

Phase 20C tests cover:

```text
stable UTF-8 round-trip
source/document custody preservation
explicit root shape
format scalar representation
camelCase enum strings
numeric enum rejection
unknown enum rejection
optional null omission
required empty arrays
unknown-property tolerance
unsupported schema rejection
missing schema rejection
case-sensitive required properties
duplicate-property rejection
tampered text/hash rejection
required-null collection rejection
no derived duplicate properties
no visual Base64
no obvious internal/runtime fields
```

---

## 14. Roadmap

```text
20.0  result contract + serialization policy       DONE
20A   DocumentIngestionResult model                 DONE
20B   deterministic result projection              DONE
20C   JSON contract + round-trip tests              THIS INCREMENT
20D   real-corpus serialized result proof           NEXT

21    deterministic end-to-end ingestion orchestrator
22    ApologiaStudio consumer integration
```

Phase 20D can now validate this exact portable contract on the pinned real
corpora without changing the JSON model.
