# Structural segmentation v3 — strict automatic typography

## Status

Increment 8.4f promotes the strict automatic typography policy validated by the
8.4d/8.4e counterfactual experiments into production.

Production profile:

```text
strict-typography-cross-page-fallback-v3
```

## Automatic heading evidence

A normalized block is an automatic heading only when all applicable conditions
hold:

```text
text length                   1..180 characters
word count                    <= 24
letter count                  >= 4
alphanumeric / non-whitespace >= 0.55
replacement character U+FFFD absent
control characters            absent
typography available
font ratio                    >= 1.18
```

For headings whose font ratio is below `1.30`, sentence-like terminal
punctuation (`.`, `;`, `,`) rejects the candidate.

The body font size remains the word-count-weighted median point size across
included normalized blocks that expose usable typography.

## Removed automatic fallbacks

v3 deliberately removes text-only structural inference from the production
heading evaluator:

```text
generic numbered headings
generic Roman-numeral headings
Chapter / Part / Section / Book bypass
weak ALL-CAPS fallback
heading inference without typography
```

These rules produced false structural boundaries on real corpora, especially
numbered list items and weak uppercase pedagogical labels.

## Why strict typography

The pinned Ehrman counterfactual evaluation measured:

```text
Production v2                       380 segments
Typography only                     278 segments
Strict typography only              267 segments
Strict typography + external hints 274 segments
Historical comparison               277 segments
```

The strict text-quality gate removed 11 automatic typography boundaries, all
without adding any:

```text
minimum letters       4
alphanumeric ratio   >= 0.55
```

Observed rejected samples included short extraction artifacts such as `eox
6.2`, `rgi)`, and a heavily punctuated corrupted fragment.

The historical segment count is a comparison reference, not a target.

## Segmentation flow

The structural flow itself is unchanged from v2:

```text
recognized automatic heading
    -> heading-led segment may span physical pages

unstructured content before any heading
    -> page-bounded fallback

next recognized heading
    -> closes the previous structured segment

textless / excluded-only page
    -> no segment
```

Source-block order, first/last physical page provenance, and deterministic local
segment IDs remain unchanged.

## Production regression corpus

8.4f gates the current production behavior on the pinned corpora:

```text
Ehrman:
  segments       267
  headings       267
  fallback         0
  cross-page     166
  small <=120     50

De Decretis:
  segments        50
  headings         0
  fallback        50
  cross-page       0
```

These counts validate the implementation against the previously measured strict
policy. They are regression evidence for the pinned corpus, not universal
document-quality targets.

## Editorial hints

Editorial heading hints are intentionally out of scope for v3 / Increment 8.4f.

The 8.4e experiment showed that external hints can recover useful weakly styled
editorial labels without reintroducing broad text-only heuristics. They should be
introduced separately through a neutral optional segmentation contract so their
behavior can be reviewed and tested independently.

## Superseded by v4

Increment 8.4g retains this strict automatic typography policy unchanged and
adds optional caller-provided editorial heading hints through the neutral
`DocumentSegmentationOptions` contract.

See `structural-segmentation-v4.md`.
