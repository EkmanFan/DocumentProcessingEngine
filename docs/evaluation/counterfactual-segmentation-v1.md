# Counterfactual segmentation evaluation v1

## Purpose

Increment 8.4d compares alternative heading policies against the exact same
normalized block stream without changing production segmentation.

The immediate evidence from Increment 8.4c is:

```text
Ehrman production boundaries

TypographySubsection   142
TypographyStrong       135
ExplicitStructural      52
UppercaseModest         51
                      ----
                       380
```

The two typography categories total 277, which is numerically equal to the
historical ApologiaStudio generic segment count. That coincidence is not treated
as proof. This evaluation tests the consequences of alternative policies.

## Policies

### A — ProductionV2

Uses the real production:

```text
HeuristicDocumentSegmenter
typography-aware-cross-page-fallback-v2
```

This is the only policy whose counts are hard regression gates.

### B — TypographyOnly

Uses the current text-quality gate and automatic font hierarchy only:

```text
font ratio >= 1.18
ratio < 1.30 rejects sentence-like blocks
```

It deliberately excludes current generic explicit-number and weak-uppercase
fallback rules.

### C — TypographyPlusStrongExplicit

Starts from B and adds only explicit textual markers:

```text
Chapter
Part
Section
Book
```

It does not treat `4. sentence`, `c. sentence`, or generic numbered list items
as structural headings.

When typography exists, the explicit marker must have:

```text
font ratio >= 0.95
```

### D — TypographyPlusHints

Starts from B and adds caller-provided editorial heading hints.

The CLI is generic. Source-specific hint strings remain in the external
evaluation runner.

Hint matching mirrors the historical generic idea:

- normalized exact match;
- decorated/short-prefix suffix match;
- compact first-source-line match for split-letter extraction.

For the pinned Ehrman evaluation the runner supplies:

```text
TAKE A STAND
WHAT DO YOU THINK?
SUGGESTIONS FOR FURTHER READING
```

These are evaluation data, not production knowledge.

## Segmentation semantics

All B/C/D policies use the same structural flow as production:

```text
recognized heading
    -> heading-led segment may span pages

unheaded content before structure
    -> page-bounded fallback

next recognized heading
    -> closes previous structured segment
```

Every policy must cover every included normalized block exactly once. The CLI
fails if a block is omitted or duplicated.

## Metrics

Each policy reports:

- total/heading/fallback segments;
- cross-page segments;
- small segments (`<= 120` characters);
- large segments (`>= 4000` characters);
- min/median/average/max characters;
- decision-origin counts;
- probe matches in headings and segment text;
- boundaries removed relative to production;
- boundaries added relative to production;
- bounded samples of removed/added boundaries;
- smallest and largest segment samples;
- delta from the historical comparison count.

## Regression versus observation

Production A is frozen at the current 8.4b baseline:

```text
Ehrman:
  380 segments
  380 headings
    0 fallback
  204 cross-page

De Decretis:
   50 segments
    0 headings
   50 fallback
    0 cross-page
```

B/C/D are observations. The runner does not assert that any counterfactual must
equal the historical count.

A lower count is not automatically better. The next production decision must
consider boundary quality, retained editorial probes, pathological small/large
segments, and whether the policy remains generic.

## Historical context

The earlier ApologiaStudio generic segmenter separated automatic font hierarchy
from optional heading hints. Its Stage 2B runner supplied source-profile hints
outside generic production code for pedagogical and bibliography labels.

This evaluation recreates that separation as an experiment, not as a production
API decision.

## Run

Build first, then:

```bash
bash scripts/evaluate-counterfactual-segmentation.sh
```

JSON reports are written under `scripts/tmp/`.
