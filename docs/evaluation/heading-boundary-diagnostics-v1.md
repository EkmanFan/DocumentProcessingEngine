# Heading-boundary diagnostics v1

## Purpose

Increment 8.4c explains the boundaries produced by:

```text
typography-aware-cross-page-fallback-v2
```

without changing production extraction, normalization, heading decisions, or
segmentation.

The immediate question is why the pinned Ehrman corpus currently produces 380
segments while the historical ApologiaStudio generic baseline produced 277.

The historical count is a comparison reference, not a target that this
diagnostic attempts to force.

## Decision-origin categories

The evaluator reports the exact accepted decision path mirrored from the
production `HeadingEvidenceEvaluator`:

```text
ExplicitStructural
ExplicitStructuralNoTypography
TypographySubsection
TypographyStrong
UppercaseModest
UppercaseNoTypography
```

For the current PdfPig corpora typography coverage is expected to be complete,
so the `NoTypography` categories are mainly useful for future backends and
formats.

## Diagnostic parity guard

The diagnostic contains a local explanatory mirror of the production heading
rules. Duplicating production logic in evaluation code is normally undesirable,
so the diagnostic compensates with an explicit parity gate.

For every included normalized block, the diagnostic reconstructs the heading
decision and compares accepted block identities against the actual heading
blocks emitted by the production segmenter.

The runner fails when either set contains an unmatched block.

This prevents stale diagnostic explanations from silently diverging from
production behavior.

## Boundary evidence

Each accepted heading records:

- source page and source sequence;
- segment identifier and ordinal;
- first/last physical page;
- cross-page status;
- segment character count and source-block count;
- heading text;
- decision origin;
- dominant font;
- point size and body-font ratio;
- word and line counts;
- whether the heading matches the numbered structural grammar;
- exact-heading recurrence count;
- preceding and following included block context.

Context text is whitespace-normalized and truncated to 240 characters.

## Review slices

The report separately surfaces up to 40 examples of:

```text
numbered structural headings
accepted headings below font ratio 1.18
segments <= 120 characters
segments >= 4000 characters
cross-page segments >= 12000 characters
repeated exact heading groups
```

These are review slices, not automatic error classifications.

A small segment can be legitimate. A repeated heading can be an intentional
pedagogical callout. A large cross-page segment may instead indicate a missed
boundary. The purpose is to expose the evidence needed for the next design
decision.

## Frozen 8.4b regression baseline

The runner verifies that this evaluation-only increment has not changed the
production behavior established in 8.4b:

```text
Ehrman:
  segments       380
  headings       380
  fallback         0
  cross-page     204

De Decretis:
  segments        50
  headings         0
  fallback        50
  cross-page       0
```

The runner also verifies source hashes, normalization/segmentation profile IDs,
and diagnostic/production heading parity.

## Run

Build first, then:

```bash
bash scripts/evaluate-heading-boundary-diagnostics.sh
```

JSON reports are written under `scripts/tmp/`.

## Non-goals

Increment 8.4c does not:

- change heading thresholds;
- change structural regexes;
- merge or split segments;
- add document-specific rules;
- add an LLM classifier;
- force the Ehrman count toward 277.

Any production change must be justified from the observations produced by this
diagnostic.
