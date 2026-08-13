# OCR-0G — PaddleOCR oracle-layout diagnostic on OCR-D `ocrd-04`

## Status

Evaluation-only.

OCR-0G does not add production OCR code, a layout analyzer, OCR routing, ROCm,
or a production backend abstraction.

## Why this experiment exists

OCR-0F diversified the corpus beyond Ehrman and exposed one severe outlier:

```text
ocrd-04
year:      1658
page type: table-of-contents
regions:   35
layout:    structurally multi-column

PaddleOCR full-page:
CER 78.571%
WER 96.269%
```

The same PAGE-XML provides:

- all 35 non-empty `TextRegion` elements;
- an explicit reading order covering those 35 regions;
- the exact source image;
- ground-truth text for each region.

The page also contains historical blackletter typography and historical
characters.

OCR-0G asks a narrow question:

> How much of the OCR-0F failure disappears if PaddleOCR is given the correct
> region layout and region reading order?

## Method

The pinned OCR-0F manifest remains the source of truth for:

- upstream repository;
- upstream commit;
- PAGE-XML path and SHA-256;
- source image path and SHA-256;
- image dimensions;
- specimen identity.

The experiment runs the unchanged PaddleOCR stack twice on the same source page
within one process.

### Full-page branch

```text
source TIFF
    ↓
PaddleOCR unchanged
    ↓
full-page OCR text
    ↓
OCR-0F normalization
    ↓
CER / WER
```

### Oracle-layout branch

```text
source TIFF
    ↓
35 PAGE-XML TextRegion polygons
    ↓
axis-aligned bounding rectangle per region
    ↓
crop, zero padding, no rescale
    ↓
PaddleOCR unchanged on each crop
    ↓
concatenate recognized region text
in explicit PAGE-XML RegionRefIndexed order
    ↓
OCR-0F normalization
    ↓
CER / WER
```

The oracle branch therefore gives PaddleOCR information that a production
system would normally need to infer:

- text-region boundaries;
- region reading order.

It does **not** give PaddleOCR ground-truth characters.

## Ground truth

For each ordered `TextRegion`:

1. use direct region `TextEquiv/Unicode` when present;
2. otherwise concatenate child `TextLine` Unicode;
3. normalize with the unchanged OCR-0F
   `unicode-nfc-whitespace-v1` profile.

The page reference is the concatenation of those region references in explicit
PAGE reading order.

## What this isolates

A large improvement in oracle-layout mode is evidence that the OCR-0F failure
was dominated by page segmentation / reading order.

A small improvement, with CER still very high, is evidence that historical
typography and character recognition remain a major problem even after correct
region layout is supplied.

An intermediate result means both effects matter.

## Important limitation

This is a region-level oracle, not a line-level oracle.

PaddleOCR still has to:

- detect text lines inside each region crop;
- recognize the historical typography;
- order text locally inside that region.

This is intentional. A line-level oracle would be a separate experiment only
if the region-level result leaves an important ambiguity.

## Engine

Unchanged from OCR-0B/OCR-0F:

```text
PaddleOCR 3.7.0
PaddlePaddle 3.2.2 CPU
PP-OCRv6_medium_det
PP-OCRv6_medium_rec
```

Orientation classification, document unwarping, and text-line orientation are
disabled.

## Decision boundary

### Oracle CER/WER drops strongly

Layout and reading order are the dominant problem on `ocrd-04`.

This supports keeping PaddleOCR as a recognition candidate while treating
layout analysis as a separate production capability.

### Oracle CER/WER remains very high

PaddleOCR's current recognition path is itself insufficient for this kind of
historical blackletter source.

Historical-document support would then require a separate model/profile or
backend decision.

### Partial improvement

Both layout and recognition materially contribute.

Do not hide that behind one aggregate OCR score.

## Run

```bash
bash scripts/evaluate-paddleocr-ocrd04-oracle-layout.sh
```

Runtime artifacts remain under `scripts/tmp/ocr-0g-oracle-layout/`.
