# OCR-0D — Curated OCR textual-fidelity ground truth

## Status

Evaluation-only.

OCR-0D adds no production OCR abstraction and makes no production backend
selection.

## Question

OCR-0B and OCR-0C both recovered all selected raster pages and all seven
outline titles. docTR was materially faster and used less memory on CPU, but
character counts alone cannot establish textual accuracy.

OCR-0D therefore asks:

> Which current challenger has lower transcription error on a small,
> independently curated reference?

## Ground-truth corpus

The committed reference contains seven zones on three physical PDF pages:

```text
p20  two dense list/reference zones
p32  three prose zones
p35  chapter-title + What to Expect zones
```

After the committed normalization profile, the reference contains:

```text
4,132 characters
712 words
```

The zones deliberately cover:

- small list typography;
- figure numbers and page numbers;
- proper names;
- punctuation;
- prose;
- two-column material isolated into single-column zones;
- large chapter-title typography;
- a callout containing quotes and print line-break hyphenation.

The ground truth is manually transcribed from the exact pinned source identified
by SHA-256. It should remain reviewable and small enough for direct visual
inspection.

## Region selection

Each OCR backend already emits neutral normalized geometry.

For each ground-truth zone, OCR-0D selects regions whose normalized bounding-box
center lies inside the zone and preserves the engine-provided region sequence.

This avoids comparing unrelated material elsewhere on the same page.

## Normalization

CER uses deterministic normalization:

1. Unicode NFC;
2. CR/LF normalization;
3. dehyphenation only across OCR region/line boundaries;
4. curly apostrophe/quote folding;
5. Unicode dash folding to ASCII `-`;
6. lowercase;
7. whitespace collapse.

WER uses Unicode letter/digit tokens plus internal apostrophes after the same
normalization. Punctuation therefore affects CER but not WER.

## Metrics

For every zone and for the aggregate corpus:

```text
CER = Levenshtein character edits / reference characters
WER = Levenshtein word edits / reference words
```

Aggregate rates sum edit counts and reference lengths before division; they are
not unweighted averages of per-zone percentages.

## Important limits

This ground truth is intentionally small.

It can discriminate the two current challengers on the Ehrman raster style, but
it does not establish:

- multilingual quality;
- handwriting quality;
- historical-document quality;
- table OCR quality;
- general PDF coverage;
- production backend selection.

A close result should be reviewed per zone rather than interpreted through
false precision.

## Run

```bash
bash scripts/evaluate-ocr-ground-truth.sh \
  --source /absolute/path/ehrman.pdf
```

The script reruns the committed PaddleOCR and docTR CPU challengers before
computing ground-truth metrics, so both consume the frozen OCR-0A rendering
contract.

Generated reports remain under `scripts/tmp/`.
