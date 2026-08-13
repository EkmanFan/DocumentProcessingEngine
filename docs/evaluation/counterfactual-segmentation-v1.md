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


## Increment 8.4e — strict heading quality gate

8.4d established that the automatic typography-only policy is much closer to
the historical structure than the current production fallback heuristics:

```text
A ProductionV2                  380
B TypographyOnly                278
C TypographyPlusStrongExplicit  315
D TypographyPlusHints           285

Historical comparison           277
```

8.4e does not change production. It adds two counterfactual policies:

### E — StrictTypographyOnly

E starts from B and restores the historical Stage 2 minimum textual-signal gate
for automatic font-based headings:

```text
minimum letters       4
alphanumeric ratio   >= 0.55
```

The existing structural constraints remain unchanged:

```text
maximum heading characters   180
maximum heading words         24
minimum font ratio           1.18
sentence-like rejection      below 1.30
```

The strict gate is intentionally evaluated independently before being proposed
for production.

### F — StrictTypographyPlusHints

F starts from E and adds the same caller-provided editorial hints used by D.

Hints remain independent of the automatic text-quality gate. This preserves the
historical separation:

```text
automatic evidence
    -> font hierarchy + text quality

explicit editorial evidence
    -> external heading hints
```

A legitimate explicit hint is therefore not rejected merely because extraction
noise causes the automatic text-quality gate to fail.

### Why these exact thresholds

The historical ApologiaStudio Stage 2 correction required:

```text
letterCount >= 4
letterOrDigitCount / nonWhitespaceCount >= 0.55
```

before an automatic font candidate could create a structural boundary. The
change was introduced specifically to reject decorative glyph fragments and
extraction noise. Editorial hints bypassed that automatic gate. This experiment
replays that generic rule against the current neutral Document Processing Engine
model.

### Strict-gate comparison diagnostics

The report schema is now:

```text
document-processing-counterfactual-segmentation-analysis-v2
```

In addition to A-F policy metrics, it records exact heading-set comparisons:

```text
B-TypographyOnly
    -> E-StrictTypographyOnly

D-TypographyPlusHints
    -> F-StrictTypographyPlusHints
```

For each comparison the evaluator reports:

- removed boundary count;
- added boundary count;
- bounded samples of removed/added boundaries;
- page and source sequence;
- font ratio;
- letter count;
- non-whitespace count;
- alphanumeric count and ratio;
- previous/next block context.

The strict gate is expected to be monotonic: it may remove automatic
typography-based boundaries, but it must not invent new ones.

### 8.4d regression freeze

While E/F remain observational, 8.4e freezes the previously measured A-D Ehrman
results and probe counts. This ensures that extending the evaluator cannot
silently change the experiment being compared.

De Decretis must remain:

```text
50 segments
0 headings
50 page-bounded fallbacks
0 cross-page segments
```

under all A-F policies.

### Decision rule after 8.4e

Do not choose a production policy from segment count alone.

The next decision should favor F only if the evidence shows that the strict
quality gate:

1. removes clearly noisy short typography boundaries;
2. preserves useful editorial probes through external hints;
3. does not regress De Decretis;
4. improves the segment-size distribution without creating pathological
   mega-segments;
5. remains generic and source-agnostic.

If those conditions hold, the following increment can simplify production by
removing generic numbered/weak-uppercase fallback heuristics and exposing
optional heading hints through a neutral contract.

## Production update — Increment 8.4f

The strict typography experiment E has been promoted to production as:

```text
strict-typography-cross-page-fallback-v3
```

The counterfactual report now labels policy A as
`A-ProductionStrictTypographyV3` and requires its production metrics and probes
to match the independently reconstructed `E-StrictTypographyOnly` policy.

The remaining comparison policies are retained to preserve evidence about why
the earlier generic explicit and weak-uppercase fallbacks were rejected.
