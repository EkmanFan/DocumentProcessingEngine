# OCR-0H — Ehrman mixed-content page benchmark

## Status

Evaluation-only.

OCR-0H is the final planned OCR spike before the smallest production OCR
integration, unless this benchmark exposes a new major failure mode.

It does not add:

- production OCR interfaces;
- a production rasterizer;
- layout analysis;
- image classification;
- manuscript OCR;
- RAG or retrieval behavior;
- ROCm/GPU support.

## Real target page

Source:

```text
The New Testament: A Historical Introduction to the Early Christian Writings
8th edition
physical PDF page: 233
printed page: 202
```

The exact pinned PDF identity is reused from the existing Ehrman OCR benchmark.

This page was chosen because it directly represents the product problem:

```text
modern printed heading
modern prose in two columns
ancient papyrus facsimile
modern figure caption
```

The modern body also contains a cross-column sentence boundary:

```text
left column ends:
"... reading other people's mail. Imagine,"

right column begins:
"for example, you stumble on a short message..."
```

Human reading therefore requires:

```text
Imagine, for example
```

The facsimile and its caption must not silently become intervening narrative
text.

## Region model used only by this benchmark

OCR-0H defines five manually verified regions:

```text
ModernHeading
ModernPrintedText - left body
ModernPrintedText - right opening paragraph
FacsimileImage
Caption
```

These names are evaluation vocabulary only. They are not production domain
types yet.

The bounds were derived from a verified render of the exact pinned physical PDF
page.

## Ground truth

CER/WER ground truth is manually transcribed only for:

- the modern section title;
- the complete left modern prose block;
- the first complete modern paragraph in the right column;
- the modern Figure 11.1 caption.

The papyrus facsimile is intentionally excluded from narrative ground truth.

Its OCR output is recorded as **untrusted informational evidence**.

OCR-0H does not require a general-purpose OCR model to correctly transcribe the
ancient manuscript in V1.

## Metrics

### Modern printed text

Aggregate CER/WER for:

```text
section title
left body
right opening paragraph
```

### Caption

Caption CER/WER is reported separately.

### Facsimile

OCR-0H reports:

```text
OCR region count
OCR character count
mean confidence when available
minimum confidence when available
short text sample
```

These values are descriptive, not truth.

### Narrative continuity

The neutral PaddleOCR region sequence is inspected for:

```text
"Imagine"
    ↓
"for example"
```

OCR-0H reports:

- whether both sentinels are found in their expected spatial regions;
- whether raw OCR sequence places `Imagine` before `for example`;
- how many OCR regions occur between them;
- whether any intervening region belongs to the facsimile or caption.

If either continuity sentinel is missing or their raw order is invalid, the
contamination outcome is `NotEvaluated`; it must never be reported as a clean
`NotDetected` result merely because no interval between sentinels could be
constructed.

A future production pipeline must not rely on a flat OCR sequence if that
sequence contaminates modern narrative text with embedded-image OCR.

## Interpretation

### Modern CER/WER is low, continuity is correct

The selected PaddleOCR candidate is adequate for this mixed page at the OCR
recognition level.

### Modern CER/WER is low, continuity is wrong or contaminated

The OCR backend is usable, but production must separate OCR evidence from
layout/read-order resolution.

### Modern CER/WER is poor

The recognition candidate still has a material problem even on modern printed
regions of a representative mixed page.

### Facsimile produces substantial OCR text

That is not automatically a failure.

It is evidence that production cannot treat every OCR observation on a page as
narrative truth.

The required V1 property is isolation/preservation, not perfect ancient
manuscript transcription.

## Run

```bash
bash scripts/evaluate-ehrman-mixed-content.sh \
  --source /absolute/path/ehrman.pdf
```

Runtime artifacts remain under:

```text
scripts/tmp/ocr-0h-ehrman-mixed-content/
```
