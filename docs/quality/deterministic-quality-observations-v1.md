# Deterministic quality observations V1

## Status

Phase 19B production boundary.

Phase 19A established custody-complete provenance. Phase 19B adds neutral quality
facts without introducing an application policy.

## Principle

Quality observations answer:

```text
what evidence/state was observed?
```

They do **not** answer:

```text
is this document good?
is this element trustworthy enough?
should this content be indexed?
should this source be admitted?
```

Those decisions belong to the consumer.

Therefore V1 contains:

- no global quality score;
- no severity;
- no grade/rating;
- no accept/reject boolean;
- no confidence threshold;
- no vector/RAG policy.

## Public layers

Phase 19B exposes three scopes:

```text
DocumentQualityObservations
  ├── DocumentElementQualityObservations[]
  └── DocumentSegmentQualityObservations[]
```

This lets a consumer reason at the same granularity at which it may later
persist, cite, chunk or vectorize content.

## Element observations

Each element quality record retains:

```text
source document SHA-256
element id
optional structural segment id
neutral element kind
selected text origin

has authoritative text
resolved/unresolved
excluded/not excluded
reconciliation divergence
normalization changed text
preserved visual present

OCR evidence present
optional OCR confidence summary
```

No raw OCR fragments or polygons are copied into the default quality model.

## OCR confidence semantics

`OcrTextObservation.Confidence` is backend evidence in `[0,1]`.

Phase 19B summarizes the fragment confidence values for one OCR region as:

```text
observation count
minimum
arithmetic mean
maximum
```

Important:

- this is not a calibrated probability that the text is correct;
- the arithmetic mean is a transparent mathematical summary only;
- V1 does not compare confidence to a threshold;
- V1 does not aggregate confidence across different OCR regions/profiles into a
  single document score;
- an OCR region may exist without confidence observations, and that absence is
  reported explicitly rather than treated as zero.

This avoids inventing false comparability across OCR profiles.

## Segment observations

For each structural segment, Phase 19B derives counts from exact provenance
membership:

```text
source elements
authoritative text elements
native authoritative text elements
OCR authoritative text elements
visual elements
unresolved text elements
deferred elements
excluded elements
reconciliation-divergent elements
normalization-changed elements
elements with OCR evidence
elements with OCR evidence but no confidence observations

mixed text origin
has unresolved evidence
```

Because current structural segmentation excludes text-flow exclusions from
segments, `ExcludedElementCount` is expected to be zero for current segments.
The field remains neutral and future-safe rather than being hard-coded away.

## Document observations

Document-level counters are derived from the element and segment observation
collections, not stored as independent mutable claims.

Examples:

```text
element count
native/OCR authoritative counts
visual/unresolved/deferred counts
excluded count
reconciliation divergence count
normalization change count
OCR evidence count
OCR evidence without confidence count

segment count
mixed-origin segment count
segments with unresolved evidence count
```

## Relationship to provenance

Quality is derived from:

```text
HybridDocumentSegmentationResult
        +
DocumentProcessingProvenance
        ↓
DocumentQualityObservationsBuilder
        ↓
DocumentQualityObservations
```

The builder validates that provenance and the normalized hybrid evidence refer
to the same page-local elements before projecting quality.

The quality result references stable provenance IDs rather than copying backend
payloads.

## Why quality is separate from provenance

Provenance answers:

```text
where did this information come from?
how was it produced?
```

Quality observations answer:

```text
what deterministic evidence/state should a downstream policy know about?
```

Keeping them separate avoids turning lineage into policy and avoids hiding
important facts behind a single score.

## Non-goals

Phase 19B does not add:

- `DocumentIngestionResult`;
- persistence;
- embeddings;
- retrieval chunks;
- vector storage;
- quality thresholds;
- admissibility rules;
- user-facing severity;
- LLM-based judgment;
- raw OCR payloads;
- raw layout backend labels;
- the end-to-end orchestrator.

## Next

```text
19A custody-complete provenance             DONE
19B deterministic quality observations     THIS INCREMENT
19C real-corpus provenance/quality proof   NEXT
20  DocumentIngestionResult                AFTER PHASE 19
```

Phase 19C must prove these observations on the pinned real corpora rather than
adding more quality policy.
