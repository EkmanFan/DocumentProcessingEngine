# OCR-0C — docTR fast_base + CRNN CPU challenger

## Status
Evaluation-only. No production OCR abstraction or backend selection.

## Pinned stack
```text
python-doctr  1.0.1
PyTorch       2.8.0 CPU
torchvision   0.23.0 CPU
detection     fast_base
recognition   crnn_vgg16_bn
```

The experiment uses docTR's end-to-end `ocr_predictor` with orientation,
straightening and language detection disabled. The stable 1.0.x OCR predictor
does not expose `detect_layout` or `detect_tables`; those flags are therefore
not passed. The same OCR-0A 300-DPI RGB PNG bytes and SHA-256 checks are used
as in OCR-0B.

## Stable API note
The first OCR-0C attempt used `detect_layout=False` and `detect_tables=False`
after consulting docTR's moving "latest" documentation. That was incorrect for
the pinned stable release: in docTR 1.0.x, extra keyword arguments are forwarded
to `DocumentBuilder`, whose supported options are `resolve_lines`,
`resolve_blocks`, `paragraph_break`, and `export_as_straight_boxes`.

OCR-0C v2 therefore uses only arguments supported by the stable tagged API.

## Neutral adaptation
docTR exposes words inside resolved lines. OCR-0C adapts each resolved line to
one neutral OCR benchmark region. Geometry is already relative to page size and
is normalized/clamped to `[0..1]`. Region confidence is `null`: docTR exposes
recognition confidence per word and OCR-0A defines no universal line-confidence
aggregation rule.

Region count is therefore not an apples-to-apples quality metric versus
PaddleOCR. Character recovery, page recovery and outline-title recovery are the
primary comparison signals.

## Current challenger reference
PaddleOCR OCR-0B established:
```text
19/19 completed
7/7 raster pages recovered
12,644 raster-reference characters
7/7 outline titles plausible
```

## Gate
The first gate remains raster-reference page recovery `7/7`. The seven raster
outline targets independently test whether chapter titles are recovered on the
correct target page.

## Performance
The benchmark records startup time, per-page inference time, total inference
time and peak process working set. CPU is evaluated before ROCm so OCR-engine
quality and execution backend remain separate variables.

## Run
```bash
bash scripts/evaluate-doctr-fast-crnn-cpu.sh --source /absolute/path/ehrman.pdf
```

Generated artifacts remain under `scripts/tmp/`.

## Decision boundary
OCR-0C compares docTR against the fixed Ehrman benchmark only. It does not
select a production OCR engine, prove multilingual/historical quality, or prove
ROCm viability. Different docTR model combinations are also outside this
increment.
