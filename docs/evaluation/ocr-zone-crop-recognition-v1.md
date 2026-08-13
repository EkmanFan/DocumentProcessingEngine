# OCR-0E v2 — isolated zone-crop OCR comparison

## Status

Evaluation-only.

This experiment replaces the rejected uncommitted OCR-0E word-granularity
diagnostic.

No production OCR abstraction or backend is selected here.

## Question

OCR-0D measured a large full-page difference:

```text
PaddleOCR: CER 2.953%, WER 4.354%
docTR:     CER 37.972%, WER 39.185%
```

Human inspection then confirmed the physical structure of page 32:

- two text columns;
- a large drop-cap `T`;
- a paragraph that starts in the left column and continues at the top of the
  right column;
- the three OCR-0D page-32 ground-truth zones correspond to the intended human
  reading areas.

The rejected word-level experiment changed region granularity and ordering at
the same time, so its CER/WER was not a controlled test of recognition quality.

OCR-0E v2 asks a narrower question:

> What happens when both OCR engines receive the exact same isolated
> ground-truth image rectangles instead of the full multi-column page?

## Method

The experiment uses the committed OCR-0A rendering path:

```text
pinned Ehrman PDF
    ↓
pdftoppm 300 DPI RGB PNG
    ↓
exact committed OCR-0D normalized zone bounds
    ↓
crop once
    ↓
SHA-256 each crop
    ↓
same crop bytes ──────────────┐
                              │
                  ┌───────────┴───────────┐
                  ↓                       ↓
             PaddleOCR                  docTR
                  ↓                       ↓
          complete crop text      complete crop text
                  └───────────┬───────────┘
                              ↓
                   OCR-0D normalization
                              ↓
                         CER / WER
```

Crop conversion is deterministic:

```text
left   = floor(normalizedLeft   * pageWidth)
top    = floor(normalizedTop    * pageHeight)
right  = ceil (normalizedRight  * pageWidth)
bottom = ceil (normalizedBottom * pageHeight)
padding = 0
rescale = none
```

The crop PNGs are generated exactly once per run. Both engines verify and OCR
those same files.

## Engines

PaddleOCR is unchanged from OCR-0B:

```text
PaddleOCR 3.7.0
PaddlePaddle 3.2.2 CPU
PP-OCRv6_medium_det
PP-OCRv6_medium_rec
```

docTR is unchanged from OCR-0C:

```text
docTR 1.0.1
PyTorch 2.8.0 CPU
fast_base
crnn_vgg16_bn
resolve_lines=True inside each isolated crop
```

## Evaluation adapter

For each ground-truth crop, the complete OCR text produced by the engine is
adapted as one synthetic evaluation region whose normalized bounds are the
original OCR-0D zone bounds.

This is deliberately limited to the evaluation layer. It lets the committed
OCR-0D normalization and Levenshtein CER/WER implementation remain unchanged.

The synthetic result is not a claim that the engine processed the full page.
Its engine id explicitly contains `zone-crop`.

## What this isolates

OCR-0E v2 removes:

- neighboring full-page columns;
- full-page reading-order interleaving;
- region-center selection of many OCR regions into a ground-truth zone;
- cross-column line reconstruction.

It still includes:

- text detection inside the crop;
- text recognition inside the crop;
- each engine's local ordering/reconstruction inside the crop.

Therefore this is an **isolated crop end-to-end OCR comparison**, not a pure
recognizer-only benchmark.

## Interpretation

If docTR improves strongly on the page-32 crops, the OCR-0D failure was largely
caused by full-page layout/reading-order behavior.

If docTR remains materially worse on the isolated crops, the remaining
difference is inside docTR's crop-local detection/recognition/reconstruction
path rather than full-page column mixing.

If both engines degrade similarly, the OCR-0D rectangles may be suitable for
region selection but too tight for direct image cropping; the crop index and
per-zone outputs make that visible rather than hiding it.

## Run

```bash
bash scripts/evaluate-ocr-zone-crops.sh \
  --source /absolute/path/ehrman.pdf
```

Generated artifacts remain under `scripts/tmp/ocr-0e-v2-zone-crops/`.
