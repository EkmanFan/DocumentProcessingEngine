# OCR-0F — PaddleOCR diversified real-scan validation

## Status

Evaluation-only.

OCR-0F does not add `IOcrEngine`, a production rasterizer, OCR routing, GPU
support, or any other production integration.

## Why this increment exists

The earlier OCR challenger work established strong evidence on the pinned
Ehrman source:

- PaddleOCR PP-OCRv6 recovered the selected raster pages;
- curated Ehrman ground truth favored PaddleOCR end-to-end;
- docTR was materially faster on CPU but failed on the full multi-column page;
- isolated crops showed that local OCR quality was much closer than the
  full-page comparison suggested.

That evidence is still narrow because it is dominated by one modern textbook.

OCR-0F therefore asks:

> Does the current PaddleOCR candidate remain usable on real scanned historical
> pages drawn from an independent ground-truth corpus?

## Independent corpus

The corpus source is `OCR-D/gt_structure_text`.

The benchmark manifest pins:

- the upstream Git repository;
- one exact upstream commit;
- PAGE-XML SHA-256 values;
- source-image SHA-256 values;
- four deterministically selected specimens.

The upstream images and PAGE-XML are **not copied into this repository**.
The evaluation runner sparse-checks out the exact pinned upstream revision.

The upstream corpus declares `CC-BY-SA-4.0`.

## Selection policy

Only PAGE-XML pages are eligible when:

1. the page has a paired raster image in the upstream corpus;
2. normalized ground-truth text has at least 600 characters;
3. an explicit PAGE reading order exists;
4. that reading order covers every non-empty `TextRegion`.

This avoids inventing missing reading-order semantics in the benchmark.

Four distinct pages are selected:

```text
early print (<= 1550)
18th century
19th century
multi-column page when structurally established
  OR an explicitly labelled complex-layout fallback
```

The fourth specimen is never silently called multi-column if the deterministic
geometry heuristic cannot establish that property.

## Ground truth

PAGE-XML region reading order is authoritative for this evaluation.

For each ordered text region:

1. use direct region `TextEquiv/Unicode` when present;
2. otherwise concatenate ordered child `TextLine` Unicode;
3. concatenate regions in explicit PAGE reading order.

No LLM, OCR engine, or inferred semantic ordering creates the reference.

## Normalization

Profile:

```text
unicode-nfc-whitespace-v1
```

It performs:

- Unicode NFC;
- line-ending normalization;
- whitespace canonicalization.

It deliberately preserves:

- case;
- historical characters;
- punctuation;
- printed hyphens.

No dehyphenation is performed.

Therefore OCR-0F CER/WER must not be compared numerically as though they were
the same corpus as OCR-0D. The purpose is robustness evidence and failure-mode
discovery.

## Engine

The current candidate remains unchanged:

```text
PaddleOCR 3.7.0
PaddlePaddle 3.2.2 CPU
PP-OCRv6_medium_det
PP-OCRv6_medium_rec
```

No orientation, unwarping, or text-line orientation preprocessing is enabled.

## Metrics

OCR-0F reports per specimen and aggregate:

```text
CER
WER
reference characters
reference words
recognized characters
recognized words
OCR elapsed time
startup time
peak process working set
```

A poor result is evidence, not a bootstrap failure.

No production-quality threshold is encoded yet because the new corpus is meant
to expose whether the candidate's current model/configuration generalizes.

## Decision boundary

OCR-0F can support one of three next decisions:

1. **PaddleOCR generalizes acceptably**
   Stop backend spikes and begin the smallest production OCR integration.

2. **PaddleOCR is good on recent/clean scans but weak on historical typography**
   Keep PaddleOCR as default candidate but make profile/model selection an
   explicit later concern.

3. **PaddleOCR is broadly weak outside Ehrman**
   Do not integrate it yet; broaden/tune the backend evaluation first.

OCR-0F does not evaluate ROCm.

## Run

```bash
bash scripts/evaluate-paddleocr-diversified-corpus.sh
```

Generated runtime artifacts remain under `scripts/tmp/ocr-0f-diversified/`.
