# Typographic evidence diagnostics v1

## Purpose

Increment 8.4a measures the neutral typography evidence added in Increment 8.3
before any production segmentation change.

This is an evaluation-only increment.

## Questions answered

The diagnostic measures:

- word font-name coverage;
- word point-size coverage;
- raw and included block typography coverage;
- line-count coverage;
- word-count-weighted median body font size;
- point-size distribution;
- dominant-font distribution;
- historical font-ratio bands;
- historical font-hierarchy heading candidates;
- overlap between those candidates and the current text-only heading heuristic;
- text-only heading samples;
- font-only candidate samples.

## Historical font hierarchy

The evaluation reproduces only the heading-candidate evidence rules from the
earlier generic ApologiaStudio segmenter:

```text
maximum heading characters  180
maximum heading words        24
minimum heading ratio        1.18
section ratio                1.30
chapter ratio                1.55
```

The body-font baseline is the word-count-weighted median block point size.

For a ratio below `1.30`, a block ending in `.`, `;`, or `,` is rejected as a
sentence-like heading candidate.

These values are diagnostic references. Increment 8.4a does not change the
production segmenter and does not assume that historical thresholds are
automatically optimal.

## Why compare with the current text heuristic

Increment 8.2 observed:

```text
Ehrman       662 current segments vs 277 historical
De Decretis   52 current segments vs  50 historical
```

The current segmenter reported 423 headings on Ehrman and three obvious false
heading detections on De Decretis.

The typography diagnostic therefore reports:

```text
current text headings
historical font candidates
overlap
text-only headings
font-only candidates
```

This allows the next design step to distinguish false-positive text headings
from missed typography-supported headings.

## No production behavior change

Increment 8.4a must not modify:

- `DocumentWord`;
- `DocumentTextBlock`;
- `PdfPigDocumentExtractor`;
- normalization;
- recurring-margin detection;
- `HeuristicDocumentSegmenter`.

The only tracked additions are evaluation CLI/reporting, a runner, and this
document.

## Run

Build first, then:

```bash
bash scripts/evaluate-typographic-evidence-diagnostics.sh
```

JSON reports are written under `scripts/tmp/`.
