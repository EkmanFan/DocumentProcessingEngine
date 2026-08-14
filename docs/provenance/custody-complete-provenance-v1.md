# Custody-complete provenance V1

## Status

Phase 19A production boundary.

This increment implements the first code-level projection required by
`document-output-boundary-v1.md`.

It deliberately stops before `DocumentIngestionResult` and before quality
observations.

## Goal

The completed hybrid evidence graph already retains strong internal provenance.
Phase 19A projects that graph into a portable model that can survive after all
DPEngine runtime/internal objects are discarded.

The projection supports the custody path:

```text
StructuralSegment
        ↓ source element ids
DocumentElementProvenance
        ↓ source evidence references
physical page + normalized region
        ↓
DocumentSourceIdentity
        ↓
source SHA-256
```

and the processing path:

```text
derived content
        ↓
DocumentProcessingManifest
        ↓
engine/component/profile identities
```

## Source identity

`DocumentSourceIdentity` contains:

```text
format
SHA-256
byte length
physical page count
optional filename
optional declared media type
```

The source SHA-256 is repeated on portable element and segment provenance so
those records remain custody-identifiable when stored or transported in a
denormalized downstream system.

## Element custody

`DocumentElementProvenance` contains:

```text
source document SHA-256
stable document-local element id
physical page
reading order
normalized bounds
neutral hybrid kind
optional structural segment id

selected source text
selected source text SHA-256

final normalized text
final normalized text SHA-256

native/OCR/none selected origin
native block source sequence
neutral layout observation sequence/kind
OCR backend/profile when OCR evidence exists

reconciliation decision
equivalence/divergence facts

selected-text dehyphenation facts when retained
final-normalization dehyphenation facts when retained
whether final normalization changed text

exclusion reason
resolved/unresolved state

preserved visual integrity metadata when applicable
```

### Raw backend labels are excluded

The existing internal `LayoutObservation.RawLabel` remains useful evidence for
debugging/evaluation, but it is not copied to default provenance.

For example:

```text
internal/diagnostic
  rawLabel = "paragraph_title"

default portable
  LayoutKind = Heading
```

Individual OCR fragment/polygon dumps are likewise not copied to the default
model.

## Text hashes

Phase 19A hashes the **exact string value returned in the provenance model** as:

```text
UTF-8 bytes without BOM
        ↓
SHA-256
        ↓
lowercase hexadecimal
```

Hashes are emitted for:

```text
selected source text
final normalized element text
structural segment text
```

Existing preserved visual content hashes are retained unchanged.

This means downstream persistence can detect whether source-selected text,
normalized text or segment text changed independently.

## Selected source text vs normalized text

For an authoritative normalized hybrid element:

```text
SelectedSourceText = NormalizedHybridDocumentElement.SourceText
NormalizedText     = NormalizedHybridDocumentElement.Text
```

Therefore a deterministic normalization such as:

```text
"Native   body."
        ↓
"Native body."
```

remains custody-visible:

```text
selected source text/hash != normalized text/hash
NormalizationChangedText = true
```

The global normalization profile is recorded in the processing manifest.

## Reconciliation transformations

When reconciliation performed source-aware dehyphenation before selecting
authoritative text, Phase 19A retains the deterministic counts for the selected
origin.

When hybrid normalization performs its own OCR dehyphenation, those counts are
retained separately.

No internal normalizer object is exported.

## Structural segment custody

`DocumentSegmentProvenance` retains:

```text
source document SHA-256
existing deterministic segment id
ordinal
normalized segment text
segment text SHA-256
optional heading
first/last physical page
ordered source element ids
text origins
mixed-origin flag
unresolved-evidence flag
```

This is intentionally sufficient for a consumer to construct its own:

```text
RetrievalChunk
        ↓
embedding
        ↓
vector store
```

without DPEngine owning retrieval chunking.

## Processing manifest

`DocumentProcessingManifest` is compact default custody information, not an
execution log.

It contains:

```text
engine/library version

native extraction backend/profile

rasterization backend/profile if used
layout backend/profile if used

distinct OCR backend/profile identities represented by evidence

reconciliation backend/profile if used

distinct visual-preservation profile ids represented by evidence

assembly profile
normalization profile
segmentation profile
```

### Why some identities come from run context

Current mature objects do not all retain run-level identity:

```text
DocumentExtractionResult
  does not carry native extractor backend/profile

LayoutObservation
  does not carry the parent LayoutAnalysisResult backend/profile

layout-backed evidence
  does not carry the rasterizer identity
```

Phase 19A does not rewrite those mature evidence types merely for reporting.

Instead `DocumentProcessingProvenanceContext` supplies the missing run-level
identities. Phase 21's deterministic orchestrator will later own construction of
that context from the actual configured run.

OCR backend/profile is derived directly from `OcrRegionResult`.

Visual profile/content integrity is derived directly from
`PreservedVisualEvidence`.

Assembly/normalization/segmentation profiles are derived directly from their
completed results.

## Intrinsic portable-model integrity

Phase 19A does not rely on the builder alone to create trustworthy records.

The public provenance records validate their own custody invariants:

```text
selected source text
  ↔ selected-source-text SHA-256

normalized text
  ↔ normalized-text SHA-256

segment text
  ↔ segment-text SHA-256
```

The exact hashing contract is centralized in `ProvenanceTextHashing`, which is
also available to downstream consumers verifying persisted results.

`DocumentProcessingProvenance` additionally validates the portable graph:

```text
Element.SegmentId
  must reference an existing segment

Segment.SourceElementIds[]
  must reference existing elements

segment membership
  must agree with Element.SegmentId in both directions

one element
  cannot belong to multiple structural segments

one segment
  cannot repeat the same source element
```

`NormalizationChangedText` is checked against the exact selected-source and
normalized strings rather than accepted as an independent claim.

## Fail-closed custody checks

The builder rejects output when:

```text
layout-backed evidence exists
AND rasterization identity is absent

layout-backed evidence exists
AND layout identity is absent

reconciliation evidence exists
AND reconciliation identity is absent

OCR is authoritative
AND explicit OcrRegionResult provenance is absent

preserved visual source SHA-256
!= declared source document SHA-256

element physical page
> declared source physical page count
```

These checks prevent the portable projection from silently emitting incomplete
custody.

## Non-goals

Phase 19A does not add:

- `DocumentIngestionResult`;
- page-result DTOs;
- quality scores;
- quality observations;
- admissibility policy;
- persistence;
- embeddings;
- vector storage;
- retrieval chunks;
- runtime event logs;
- raw PP-Structure/PaddleX payloads;
- raw backend labels in default provenance;
- the end-to-end orchestrator;
- ApologiaStudio semantics.

## Next

```text
19A custody-complete provenance             THIS INCREMENT
19B deterministic quality observations     NEXT
19C real-corpus provenance/quality proof   TODO
20  DocumentIngestionResult                AFTER PHASE 19
```
