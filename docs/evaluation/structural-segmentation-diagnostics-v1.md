# Structural segmentation diagnostics v1

## Purpose

Increment 8.2 measures the first generic structural segmenter against the pinned
real-document corpora before any tuning.

This is deliberately an observational evaluation, not a segment-count parity
gate.

The current segmentation profile is:

```text
typography-aware-cross-page-fallback-v2
```

## Historical references

Historical ApologiaStudio generic segment counts are:

```text
De Decretis pages 512-561: 50 segments
Ehrman full document:       277 segments
```

The evaluation reports current count, delta, and ratio. It does not fail merely
because the current count differs.

## Upstream regression guards

The runner still fails if already-established extraction or normalization
behavior changes.

Ehrman must retain:

```text
617 selected pages
233595 native words
3179 raw blocks
2648 included normalized blocks
531 recurring headers excluded
0 recurring footers excluded
```

De Decretis pages 512-561 must retain:

```text
50 selected pages
29044 native words
269 raw blocks
269 included normalized blocks
0 recurring headers excluded
0 recurring footers excluded
```

## Diagnostics captured

For each corpus the JSON report contains:

- total segment count;
- heading-triggered and fallback segment counts;
- pages with no source block represented by a segment;
- pages with multiple segments;
- maximum segments on one page;
- cross-page segment count and exact source-page coverage;
- min/median/average/max character count;
- min/median/average/max source-block count;
- small segments (`<= 120` characters);
- large segments (`>= 4000` characters);
- page-number lists for zero/multiple segment cases;
- all detected headings;
- ten smallest segments;
- ten largest segments;
- known textual probe matches by segment.

The small/large thresholds are diagnostic only.

## Interpretation

Do not tune constants merely to force 50 or 277.

Inspect first:

1. false-positive headings;
2. missed headings;
3. oversized page fallback segments;
4. over-fragmented pages;
5. missing typography/font evidence;
6. whether the historical count is itself the desirable target.

Only then should the production segmenter change.

## Run

Build first, then:

```bash
bash scripts/evaluate-structural-segmentation-diagnostics.sh
```

Pinned PDFs are identified by SHA-256 and are not stored in the repository.
JSON reports are written under `scripts/tmp/`.
