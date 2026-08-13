# Structural segmentation v1

## Scope

Increment 8.1 introduces the first neutral structural segmentation capability.

It is intentionally conservative and synthetic-test driven. It does not yet
attempt real-document parity with the historical ApologiaStudio segment counts.

The segmentation profile is:

```text
page-bounded-obvious-headings-v1
```

## Boundary

Structural segments are stable document units.

They are not retrieval chunks.

```text
DocumentTextNormalizationResult
            ↓
      IDocumentSegmenter
            ↓
   DocumentSegmentationResult
            ↓
      DocumentSegment[]
```

Retrieval chunking, embeddings, vector projection, and consumer-specific
classification remain outside this engine stage.

## Source preservation

Each `DocumentSegment` retains the exact normalized source block references from
which it was derived.

Excluded recurring margins are ignored by segmentation but remain present in
the normalization result and therefore remain auditable.

## Page-bounded fallback

V1 never creates a segment spanning multiple physical pages.

If a page contains no obvious heading evidence, all included blocks on that page
form one fallback segment.

This deliberately prevents cross-page mega-segments while the heading model is
still minimal.

## Obvious heading evidence

V1 uses only text evidence because font hierarchy metadata has not yet been
ported into the neutral Core model.

A block is considered an obvious heading when it is short enough and either:

- contains letters but no lowercase letters; or
- begins with an explicit structural marker such as `CHAPTER`, `PART`,
  `SECTION`, `BOOK`, a numeric hierarchy such as `1.` / `1.2`, or a Roman
  numeral.

Limits:

```text
maximum heading length     120 characters
maximum heading word count 14 words
minimum heading letters      3
```

This is deliberately narrower than a general heading classifier.

## Segment identity

`DocumentSegment.Id` is deterministic and document-local:

```text
p000005-s000001
```

It is derived from physical page number and segment ordinal.

It is not claimed to be globally unique across different documents. Global
source/document identity remains a separate provenance concern.

## Deferred

Increment 8.1 does not yet include:

- font hierarchy;
- heading font/style evidence;
- exact/compact heading hints;
- cross-page intellectual sections;
- semantic segment kinds;
- real-document segment-count parity;
- retrieval chunking.

Those capabilities should be added only when evaluation demonstrates the need.

## Next evaluation increment

Increment 8.2 should run this segmenter against the pinned real corpora and
compare its behavior with the historical baselines before tuning heuristics.

Known historical references include:

```text
De Decretis pages 512-561: 50 generic segments
Ehrman full document:       277 generic segments
```

Those numbers are evaluation targets, not assumptions baked into this
implementation.
