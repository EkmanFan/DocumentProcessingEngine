# OCR benchmark v1

## Status

OCR-0A defines evaluation infrastructure only.

It does **not** select or integrate an OCR engine.

The production engine still has no `IOcrEngine` implementation.

## Goal

The benchmark answers:

> Which local OCR backend produces the most useful textual evidence from the
> same rendered page images, with acceptable quality, geometry, performance,
> deployment cost, and licensing?

Candidate backends are evaluated after this harness exists. Initial candidates
are expected to include PP-OCRv6, docTR, and EasyOCR.

## Why input rendering is fixed

An OCR comparison is invalid if each backend receives a differently rendered
page.

OCR-0A therefore fixes the benchmark input pipeline to:

```text
pinned source PDF
    ↓
pdftoppm
    ↓
RGB PNG
300 DPI
no preprocessing
    ↓
same image bytes for every backend
```

`pdftoppm` is benchmark infrastructure only. It is not the production
`IPdfRasterizer` decision.

Every rendered image receives a SHA-256 in `input-index.json`. OCR engine
results must repeat that hash, so the evaluator can reject results produced
from different input bytes.

## Reference source

The corpus is pinned to the exact Ehrman PDF already used by the native
extraction regression suite:

```text
SHA-256
f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe

bytes
233369762

pages
617
```

The committed manifest is:

```text
docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json
```

## Corpus

The first benchmark deliberately uses 19 discriminating pages rather than a
large manually annotated dataset.

### A. Raster reference

```text
14-20
```

Seven known textless/dominant-raster pages.

Historical challenger reference:

```text
Docling + EasyOCR
7/7 pages recovered
12,393 characters
```

The hard first gate for a serious challenger is page recovery `7/7`.
Character count remains observational because formatting and normalization can
differ between engines.

### B. Raster outline targets

```text
32
35
51
72
92
113
134
```

These are known textless/dominant-raster target pages for PDF outline entries.

The outline provides independent expected structural titles such as:

```text
2. Do We Have the Original New Testament?
```

The OCR evaluator tests whether one region or up to three adjacent OCR regions
recover the expected title on the actual target page.

The title matcher is deterministic and reports:

```text
ExactEquivalent
Containment
HighOverlap
ModerateOverlap
WeakOverlap
None
```

Only the first three are counted as plausible title recovery.

This is evaluation evidence, not a production confidence score.

### C. Born-digital controls

```text
33
34
398
400
405
```

These pages have healthy native text in the current baseline.

They are included as controls for OCR backend comparison, but the production
routing policy remains:

> Prefer trustworthy native PDF text. Do not OCR healthy pages by default.

## Neutral OCR result contract

External OCR runners produce:

```text
document-processing-ocr-engine-result-v1
```

The JSON Schema is:

```text
docs/evaluation/ocr-engine-result-v1.schema.json
```

A result identifies:

```text
engine id
engine version
model
backend/runtime
device
optional engine metadata
optional process/GPU memory observations
```

Each page contains:

```text
physical page number
input image SHA-256
Completed / Failed
elapsed milliseconds
image width / height
ordered OCR regions
```

Each region contains:

```text
sequence
text
confidence?  // nullable: engines do not expose identical confidence semantics
normalized bounds [0..1]
```

No vendor-specific PaddleOCR/docTR/EasyOCR type crosses this boundary.

## Metrics

OCR-0A evaluates independent dimensions rather than one synthetic score.

### Coverage

```text
completed pages
failed pages
pages producing text
region count
character count
elapsed milliseconds
```

### Raster reference

```text
pages producing text out of 7
character count
historical EasyOCR reference
```

### Structural title recovery

For the seven outline target pages:

```text
plausible title matches
exploratory matches
no candidate
match-band distribution
```

### Born-digital controls

```text
control pages producing OCR text
character count
```

This is useful comparison data; it does not imply that production should OCR
those pages.

## Metrics deliberately deferred

OCR-0A does not claim CER or WER.

Those require trustworthy page-level ground-truth transcriptions. They should
be added only after the initial candidates have been reduced and manual
annotation is worth the cost.

Reading-order quality beyond local title clustering is also evaluated later,
after real OCR outputs exist.

## Reproducible input preparation

```bash
bash scripts/prepare-ocr-benchmark-inputs.sh \
  --source /absolute/path/ehrman.pdf
```

The generated files live under `scripts/tmp/` by default and are not committed.

The output directory contains:

```text
page-0014.png
...
page-0405.png
input-index.json
```

## Corpus verification

The committed manifest records the expected current native state of every
selected page:

```text
TextlessDominantRaster
NativeText
```

The EvaluationCli can re-check those assumptions against the current PdfPig
baseline:

```bash
dotnet run \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  verify-ocr-benchmark-corpus \
  --manifest docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json \
  --source /absolute/path/ehrman.pdf \
  --report scripts/tmp/ocr-corpus-verification.json
```

## Evaluating an OCR backend result

After a future runner produces a neutral result JSON:

```bash
dotnet run \
  --project tools/DocumentProcessing.EvaluationCli \
  -- \
  evaluate-ocr-benchmark \
  --manifest docs/evaluation/corpora/ehrman-ocr-benchmark-v1.json \
  --input-index scripts/tmp/ocr-benchmark-inputs/input-index.json \
  --result scripts/tmp/paddleocr-result.json \
  --report scripts/tmp/paddleocr-evaluation.json
```

## OCR-0A success criteria

OCR-0A is complete when:

1. the 19-page corpus manifest is deterministic and source-pinned;
2. all 19 native/raster expectations match the current extraction baseline;
3. the same 300-DPI PNG bytes can be prepared for every OCR backend;
4. the neutral result contract rejects wrong pages, image hashes, dimensions,
   invalid bounds, invalid confidence, and duplicate sequences;
5. the evaluator reports coverage, raster recovery, title recovery, and basic
   runtime observations without invoking an OCR engine;
6. a synthetic contract result passes end-to-end as a harness self-test;
7. no production source file is modified.

## Next increment

OCR-0B should add the first **external benchmark runner** for PP-OCRv6.

That runner must adapt its native output into
`document-processing-ocr-engine-result-v1`.

It must not introduce PaddleOCR types or runtime dependencies into Core,
Engine, or Pdf projects.

CPU is the first reproducible baseline. AMD/ROCm acceleration is evaluated
after correctness is established.
