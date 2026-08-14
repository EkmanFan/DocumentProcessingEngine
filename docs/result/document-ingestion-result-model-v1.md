# DocumentIngestionResult model V1

## Status

Phase 20A public model.

Phase 20.0 froze the contract/serialization policy. Phase 20A introduces the
strongly typed in-memory result only.

JSON serialization remains Phase 20C.

---

## 1. Shape

```text
DocumentIngestionResult
  SchemaVersion
  Source
  ProcessingManifest
  Pages[]
  Elements[]
  StructuralSegments[]
  QualityObservations
```

The root is immutable/read-only.

Its schema identifier is:

```text
document-ingestion-result-v1
```

The schema identifier is distinct from `ProcessingManifest.EngineVersion`.

---

## 2. Reuse the proven Phase 19 portable records

Phase 19A already created and real-corpus-tested portable custody records:

```text
DocumentSourceIdentity
DocumentProcessingManifest
DocumentElementProvenance
DocumentSegmentProvenance
```

Phase 20A deliberately reuses those records.

It does **not** create:

```text
DocumentIngestionElement
  copies every DocumentElementProvenance field

DocumentIngestionSegment
  copies every DocumentSegmentProvenance field
```

Such DTO mirroring would create two model definitions for the same documentary
truth and would add mapping/versioning risk without adding semantics.

Therefore:

```text
DocumentIngestionResult.Elements
  = IReadOnlyList<DocumentElementProvenance>

DocumentIngestionResult.StructuralSegments
  = IReadOnlyList<DocumentSegmentProvenance>
```

The type name `Provenance` reflects where the records were introduced; inside
the final result they are the authoritative element/segment content + custody
records.

There is no second root `Provenance` aggregate.

---

## 3. Pages

`DocumentIngestionPage` adds the page-level information required by the Phase 19
output boundary without copying element content.

```text
DocumentIngestionPage
  PageId
  PhysicalPageNumber
  ContentViewport
  OrderedElementIds[]
```

`PageId` is deterministic:

```text
p000001
p000002
...
```

V1 page coordinates use the existing normalized top-left page coordinate
semantics.

Every physical source page must have exactly one page entry, including pages
with zero returned elements.

The root validates:

```text
Pages.Count == Source.PhysicalPageCount
Pages[i].PhysicalPageNumber == i + 1
Page.OrderedElementIds
  == result elements on that page ordered by ReadingOrder
```

This makes page membership explicit without nesting a second copy of the
elements under each page.

---

## 4. Quality without duplicated truth

Phase 19B's analytical quality model intentionally exposes convenient element,
segment and document observations.

Many of those facts are already authoritative in the final element/segment
records:

```text
quality fact                     authoritative result location

resolved                         Element.IsResolved
excluded                         Element.ExclusionReason / IsExcluded
selected origin                  Element.TextOrigin
reconciliation divergence        Element.HasReconciliationDivergence
normalization changed            Element.NormalizationChangedText
preserved visual present         Element.PreservedVisual
OCR evidence present             Element.OcrBackendId / OcrProfileId
mixed text origin                Segment.TextOrigins / IsMixedTextOrigin
unresolved segment evidence      Segment.HasUnresolvedEvidence
native/OCR counts                derived from Elements
visual/deferred counts           derived from Elements
```

Serializing these again under a second quality subtree would violate the
Phase 20.0 single-authoritative-representation rule.

The Phase 19B fact not otherwise retained in the portable element graph is the
aggregated OCR confidence summary.

Therefore the V1 final quality payload is intentionally narrow:

```text
DocumentIngestionQualityObservations
  OcrConfidenceObservations[]
    ElementId
    OcrConfidenceSummary
      ObservationCount
      Minimum
      ArithmeticMean
      Maximum
```

An element with OCR evidence but no confidence summary is still distinguishable:

```text
Element.OcrBackendId != null
AND
no OcrConfidenceObservation for ElementId
```

No false zero is introduced.

No quality score, severity, threshold or admissibility policy is added.

Phase 20B will project and cross-check the richer Phase 19B analytical model into
this non-duplicating final representation.

---

## 5. Visual assets

Phase 20A does not add a root `VisualAssets` or
`PreservedVisualReferences` collection.

Visual custody metadata is already authoritative on:

```text
DocumentElementProvenance.PreservedVisual
```

and the containing element already supplies:

```text
source document
physical page
normalized bounds
```

while `PreservedVisualProvenance` supplies:

```text
profile
media type
source raster dimensions
pixel crop
content length
content SHA-256
```

A second visual collection would duplicate the same portable truth.

Binary visual bytes remain outside the default result as frozen in Phase 20.0.

---

## 6. Aggregate validation

`DocumentIngestionResult` fails closed on contradictory public state.

It reuses `DocumentProcessingProvenance` construction internally to validate:

```text
source SHA consistency
unique element IDs
unique segment IDs
bidirectional element/segment membership
```

It additionally validates result-level invariants:

```text
complete physical page set
exact page ordering
page -> element membership
unique page reading orders
element physical page range
segment page span == source-element page span

OCR backend/profile identity pairing
layout sequence/kind pairing
OCR evidence retains its source layout observation
observed OCR identity present in ProcessingManifest
layout evidence has rasterization + layout manifest identity
reconciliation evidence has reconciliation manifest identity
visual profile present in ProcessingManifest

OCR confidence observation -> known element
OCR confidence observation -> element has OCR evidence identity
```

These checks are part of the strongly typed model, not deferred to JSON.

Phase 20C deserialization must ultimately construct this model and therefore
receive the same invariant enforcement.

---

## 7. Intentional non-goals

Phase 20A does not add:

- deterministic projection from the hybrid/Phase 19 graph;
- JSON serialization;
- XML;
- an ingestion orchestrator;
- persistence;
- retrieval chunks;
- embeddings;
- vector records;
- ApologiaStudio-specific semantics;
- raw diagnostics;
- visual binary transport.

---

## 8. Next

```text
20.0  result contract + serialization policy       DONE
20A   DocumentIngestionResult model                 THIS INCREMENT
20B   deterministic result projection              NEXT
20C   JSON contract + round-trip tests
20D   real-corpus serialized result proof

21    deterministic ingestion orchestrator
22    ApologiaStudio consumer integration
```

Phase 20B should now be a small deterministic projection problem, not another
contract-design phase.
