# Typographic evidence v1

## Purpose

Increment 8.3 restores optional typography observations in the neutral
Document Processing Engine extraction model.

This is evidence restoration only. The structural segmenter is deliberately not
changed in this increment.

## Neutral model

`DocumentWord` now exposes:

```text
FontName?
MedianPointSize?
```

`DocumentTextBlock` now exposes:

```text
DominantFontName?
MedianPointSize?
LineCount
WordCount
```

`WordCount` is derived from the retained source-word collection rather than
stored as a second count.

No PdfPig type crosses the `DocumentProcessing.Core` boundary.

## Optional evidence

Typography is optional because not every document format or extraction backend
can supply it.

A missing value is represented as `null`, not as a fabricated font or point
size.

When point size is present it must be finite and greater than zero.

A `LineCount` of zero means the producer did not supply line-count evidence.

## PdfPig mapping

The PdfPig adapter uses the same evidence strategy as the historical
ApologiaStudio pipeline:

- word font name from the PdfPig word observation;
- word median point size from its source letters;
- block dominant font name from source-letter frequency;
- block median point size from all source letters in the block;
- block line count from PdfPig layout lines.

Dominant-font ties are resolved deterministically by font name.

## Source preservation

Adding typography does not alter:

- source sequence;
- word text;
- word geometry;
- block text;
- block geometry;
- block source sequence;
- reading order;
- recurring-margin behavior;
- structural segmentation behavior.

Normalized blocks already retain their exact `SourceBlock`, so typography
remains available downstream without duplicating it into the normalized model.

## Regression policy

Increment 8.3 must preserve the established real-document baselines:

- De Decretis native PDF parity;
- Ehrman native PDF parity;
- Ehrman and De Decretis recurring-margin parity.

The current structural segmenter does not consume typography yet.

A later increment should first measure typography coverage/distributions on the
pinned corpora and then introduce font-hierarchy segmentation using evidence
rather than tuning text regexes toward historical segment counts.
