# DocumentIngestionResult deterministic projection V1

## Status

Phase 20B implementation decision.

Phase 20.0 froze the contract/serialization policy.

Phase 20A introduced the strongly typed result model.

Phase 20B now introduces the single deterministic projection from a completed
hybrid segmentation into that model.

JSON remains Phase 20C.

---

## 1. Boundary

The public projection entry point is intentionally narrow:

```text
HybridDocumentSegmentationResult
+
DocumentProcessingProvenanceContext
        │
        ▼
DocumentIngestionResultBuilder
        │
        ▼
DocumentIngestionResult
```

The builder does not accept prebuilt:

```text
DocumentProcessingProvenance
DocumentQualityObservations
```

from callers.

Instead it invokes the already-proven Phase 19 builders itself:

```text
completed segmentation
        │
        ├── DocumentProcessingProvenanceBuilder
        │       ↓
        │   provenance + processing manifest
        │
        └── DocumentQualityObservationsBuilder
                ↓
            deterministic quality facts

then
        ↓
DocumentIngestionResult projection
```

This prevents a caller from combining a provenance graph from one completed
document state with quality observations from another.

---

## 2. Why this is not Phase 21 orchestration

Phase 20B starts only after the document has already completed:

```text
assembly
normalization
segmentation
```

and after the caller supplies run-level custody identities through
`DocumentProcessingProvenanceContext`.

It performs no:

- source opening;
- format detection;
- PDF extraction;
- rasterization;
- layout execution;
- OCR;
- reconciliation;
- normalization;
- segmentation;
- service lifecycle;
- retry policy;
- persistence;
- JSON serialization.

Phase 21 will own deterministic end-to-end execution and construction of the
provenance context from the configured run.

Phase 20B is only the final projection boundary.

---

## 3. Reuse Phase 19 projections

The builder must reuse:

```text
DocumentProcessingProvenanceBuilder
DocumentQualityObservationsBuilder
```

rather than implementing a second provenance or quality algorithm.

The final result receives the exact portable Phase 19:

```text
Source
ProcessingManifest
Elements
Segments
```

from the provenance projection.

This preserves one implementation of custody semantics.

---

## 4. Page projection

Pages are created from the completed normalized page graph.

For every physical page:

```text
DocumentIngestionPage
  PhysicalPageNumber
  ContentViewport
  OrderedElementIds
```

`ContentViewport` is copied from the authoritative hybrid source page.

Element IDs are resolved from the already-built provenance projection by:

```text
(PhysicalPageNumber, ReadingOrder)
```

The final `DocumentIngestionResult` constructor revalidates:

```text
page count
physical page order
ordered page membership
reading-order uniqueness
```

No element is nested/copied under a page.

---

## 5. Quality projection without duplicate truth

Phase 19B produces the richer analytical quality graph.

Phase 20A established that nearly all those facts are already authoritative on
the result element/segment graph.

Phase 20B therefore copies only:

```text
ElementId
OcrConfidenceSummary
```

for elements with an actual confidence summary.

No zero/default observation is invented when OCR evidence exists without
confidence observations.

Before dropping the duplicated analytical fields, Phase 20B cross-checks them
against the authoritative provenance graph.

Element-level checks include:

```text
segment membership
kind
text origin
authoritative-text presence
resolved state
exclusion state
reconciliation divergence
normalization change
preserved-visual presence
OCR-evidence presence
```

Segment-level checks include all deterministic Phase 19B counts plus:

```text
mixed text origin
unresolved evidence
```

The rich analytical quality object is therefore safely reduced, not silently
ignored.

---

## 6. Determinism

The projection profile identifier is:

```text
document-ingestion-result-projection-v1
```

For identical completed segmentation evidence and identical provenance context,
the projected result must be semantically deterministic:

```text
same source identity
same processing manifest
same page IDs/viewports/membership
same element IDs/content hashes
same segment IDs/content hashes
same retained OCR confidence summaries
```

Phase 20C will separately define stable JSON serialization semantics.

JSON bytes are still not documentary custody identity.

---

## 7. Failure model

The builder fails closed when required information is contradictory.

Examples include failures already enforced by the reused builders/result model:

```text
missing raster/layout identity for layout-backed evidence
missing reconciliation identity
OCR evidence without complete source provenance
source page count inconsistent with final pages
dangling segment membership
processing manifest inconsistent with represented evidence
```

Phase 20B additionally checks that the richer Phase 19B quality observations
correspond exactly to the provenance element/segment graph before reducing them
to the non-duplicating final quality payload.

No warning-only fallback is introduced.

---

## 8. Non-goals

Phase 20B does not add:

- JSON;
- XML;
- persistence;
- an HTTP API;
- source-file orchestration;
- ML service orchestration;
- retrieval chunks;
- embeddings;
- vector storage;
- consumer-specific semantics;
- optional diagnostics.

---

## 9. Roadmap

```text
20.0  result contract + serialization policy       DONE
20A   DocumentIngestionResult model                 DONE
20B   deterministic result projection              THIS INCREMENT
20C   JSON contract + round-trip tests              NEXT
20D   real-corpus serialized result proof

21    deterministic end-to-end ingestion orchestrator
22    ApologiaStudio consumer integration
```

After Phase 20B, `DocumentIngestionResult` is constructible from the completed
engine evidence graph without a consumer knowing any Phase 19 projection details.
