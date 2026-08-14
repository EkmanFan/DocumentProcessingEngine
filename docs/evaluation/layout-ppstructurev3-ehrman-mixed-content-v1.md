# LAYOUT-0A — PP-StructureV3 on Ehrman mixed-content page

## Status

Evaluation-only.

This spike follows OCR-0H and evaluates one layout/document-parsing challenger:
PP-StructureV3.

No alternative layout engine is benchmarked unless PP-StructureV3 materially
fails the real product requirements.

## Product question

The exact target remains Ehrman physical PDF page 233 / printed page 202.

The page contains:

```text
modern heading
modern two-column prose
ancient papyrus facsimile
modern figure caption
```

The production requirement is not "OCR every visible glyph".

The desired policy is conceptually:

```text
text / heading / caption
    -> textual extraction / OCR

image / figure
    -> preserve as visual asset
    -> do not OCR as narrative text
```

LAYOUT-0A asks whether PP-StructureV3 supplies enough structural evidence for
our software to enforce that policy.

## Engine

Pinned evaluation stack:

```text
PaddlePaddle 3.2.2 CPU
PaddleOCR 3.7.0 + doc-parser dependencies
PP-StructureV3
```

The experiment explicitly disables capabilities not needed for this page:

```text
document orientation classification
document unwarping
text-line orientation classification
seal recognition
table recognition
formula recognition
chart recognition
```

Document region detection remains enabled because multi-column parsing and
reading-order evidence are part of the question.

## Evidence consumed

The benchmark reuses the already committed OCR-0H human structure:

```text
section-title
left-body
right-opening
facsimile
caption
```

Those names are evaluation vocabulary, not production domain types.

### Spatial-oracle correction

The first LAYOUT-0A execution exposed that the original OCR-0H normalized
bounds had been derived from a differently scaled/cropped page representation.
The content labels and transcriptions were correct, but the spatial oracle was
not in the same coordinate system as the exact 300-DPI benchmark render.

Before the final LAYOUT-0A decision, the five regions were therefore manually
re-annotated on the exact `2556x3305` RGB render of physical PDF page 233.

The corrected pixel envelopes are deliberately rounded human annotations:

```text
section-title  [610,  800, 1420, 1000]
left-body      [610, 1025, 1490, 1400]
right-opening  [1510, 760, 2410, 1360]
facsimile      [605, 1420, 1490, 2860]
caption        [600, 2860, 1490, 3140]
```

They were not copied from PP-StructureV3 prediction boxes.

PP-StructureV3 `parsing_res_list` is adapted into neutral diagnostic blocks:

```text
sequence
provided block order
block label
pixel bbox
normalized bbox
block content
```

The list sequence is preserved as the parser's observed reading order.

## Decision gates

The automated assessment is intentionally narrow and page-specific.

A PASS requires:

```text
figureDetectedAsNonNarrative
noNarrativeTextBlockCenteredInsideFacsimile
captionSeparated
sectionTitleSeparated
leftModernTextDetected
rightModernTextDetected
modernTextReadingOrderUsable
figureCaptionSpatialRelationPlausible
```

The overlap thresholds are diagnostic, not production quality thresholds.

The important product failures are:

```text
papyrus emitted as narrative text
papyrus not separated as figure/image
caption not separated
modern text regions not detected
unusable modern-text reading order
```

## Reading-order interpretation

This page deliberately contains the narrative continuation:

```text
left:
  "... Imagine,"

right:
  "for example, ..."
```

The benchmark reports both:

1. sequence order of the text blocks matched spatially to the human left and
   right regions;
2. content sentinels `Imagine` and `for example` when OCR content is sufficient
   to locate them.

Figure/caption blocks may appear in the parser sequence, but production will be
able to exclude them from narrative text if they are classified separately.
Therefore separation is more important than forcing one universal flat order.

## Human review artifact

LAYOUT-0A also produces:

```text
annotated.png
```

It overlays PP-StructureV3 blocks and the OCR-0H human reference regions.

The automatic PASS/FAIL is not a substitute for checking that image before a
production design decision.

## Decision after this spike

### PASS and visual inspection is satisfactory

Stop layout-engine comparison.

Proceed to the smallest production increment that adapts PP-StructureV3 layout
observations and applies deterministic treatment policy.

### Material FAIL

Identify the exact failed capability first.

Only then decide whether configuration/model tuning or a different challenger
is justified.

Do not benchmark another engine merely for completeness.

## Run

```bash
bash scripts/evaluate-ppstructurev3-ehrman-mixed-content.sh \
  --source /absolute/path/ehrman.pdf
```

Artifacts are written under:

```text
scripts/tmp/layout-0a-ppstructurev3-ehrman/
```
