# OCR-0B — PaddleOCR PP-OCRv6 medium CPU challenger

## Status

Evaluation-only.

OCR-0B does not add a production OCR abstraction or select PaddleOCR as the
production engine.

## Pinned stack

```text
PaddlePaddle CPU container: 3.2.2
PaddleOCR:                 3.7.0
Detection:                 PP-OCRv6_medium_det
Recognition:               PP-OCRv6_medium_rec
Device:                    CPU
Document orientation:      disabled
Document unwarping:        disabled
Text-line orientation:     disabled
```

The optional preprocessing modules are disabled deliberately so the first
comparison measures the core detection + recognition path on exactly the same
OCR-0A input images.

## Rejected PaddlePaddle 3.3.0 CPU runtime

The first OCR-0B execution used PaddlePaddle 3.3.0 and produced 19/19 runtime
failures before any OCR quality could be measured.

That result is not treated as a PaddleOCR quality failure.

PaddlePaddle upstream issue #77340 documents a 3.3.0 CPU oneDNN/PIR inference
regression (`ConvertPirAttribute2RuntimeAttribute`) and identifies 3.2.2 as a
workaround. PaddleOCR issue #18162 reports the same failure class with
PaddleOCR 3.7.0 on CPU.

OCR-0B therefore pins PaddlePaddle 3.2.2 while leaving PaddleOCR 3.7.0, the
PP-OCRv6 medium models, the benchmark images, and all evaluator settings
unchanged.

The benchmark runner also prints the first full inference traceback so a
future runtime failure cannot be mistaken for poor OCR recognition.

## Why Docker

The benchmark runs in an isolated container instead of installing PaddlePaddle
into the developer workstation.

On SELinux-enabled Fedora hosts, bind mounts use Docker's `:Z` private relabel
option so the container can read the temporary Git worktree and write benchmark
artifacts without disabling SELinux enforcement.

This has three benefits:

1. the Fedora host is not modified;
2. Python/Paddle dependency versions are reproducible;
3. CPU correctness can be established before investigating AMD/ROCm.

The benchmark container is not a production deployment decision.

## Input integrity

OCR-0B consumes the exact OCR-0A 300-DPI RGB PNG files and validates each
image SHA-256 before inference.

PaddleOCR output is translated to the neutral
`document-processing-ocr-engine-result-v1` contract.

Vendor-specific result structures remain inside the benchmark adapter.

## Geometry

PaddleOCR `rec_boxes` use pixel coordinates:

```text
x_min
y_min
x_max
y_max
```

with a top-left origin.

OCR-0B normalizes these values to `[0..1]` using the committed OCR-0A image
dimensions.

## Initial quality gate

The historical Docling + EasyOCR challenger recovered text from all seven
Ehrman raster-reference pages.

Therefore:

```text
raster-reference page recovery >= 7/7
```

is the first minimum gate for PaddleOCR to remain a serious candidate.

The historical `12,393` characters are reported for context, not used as an
equality threshold.

## Structural evidence

The seven raster chapter-opening pages are evaluated against independent PDF
outline titles.

The existing neutral evaluator reports:

```text
plausible
exploratory
none
```

and the underlying deterministic title-match bands.

No model-specific scoring is converted into a universal confidence score.

## Performance

OCR-0B records:

```text
pipeline startup milliseconds
per-page inference milliseconds
total inference milliseconds
peak process working set
```

GPU/VRAM are deliberately absent in this CPU baseline.

## Run

```bash
bash scripts/evaluate-paddleocr-ppocrv6-cpu.sh \
  --source /absolute/path/ehrman.pdf
```

Generated outputs remain under `scripts/tmp/`.

## Decision boundary

OCR-0B answers only:

> How does PP-OCRv6 medium perform on the fixed OCR-0A Ehrman corpus using a
> reproducible CPU baseline?

It does not answer:

- whether PaddleOCR should be adopted for production;
- whether ROCm works acceptably;
- whether preprocessing/orientation should later be enabled;
- whether docTR or EasyOCR is better;
- whether the current 19-page corpus is sufficient for final engine selection.

Those questions require subsequent challengers using the same harness.
