# PDF outline diagnostics v1

## Purpose

Increment 8.5a evaluates native PDF outline/bookmark evidence before any
production reconciliation design is introduced.

The question is not whether bookmarks should replace automatic heading
detection. The question is what structural evidence the PDFs already provide,
how reliably PdfPig exposes it, and how that evidence overlaps with current
normalized blocks and production headings.

## Scope

This increment is evaluation-only.

It does not change:

- native extraction;
- normalization;
- recurring-margin exclusion;
- production heading detection;
- optional heading hints;
- segmentation;
- Core document models.

## Native PDF evidence

The evaluator opens the complete source PDF with PdfPig and requests its
bookmark/outline tree with container nodes enabled.

For each outline entry it records:

- pre-order ordinal;
- parent ordinal;
- hierarchy level;
- title;
- PdfPig node type;
- whether it targets the current document;
- target physical page when available;
- destination display type;
- raw destination left/top/right/bottom coordinates when available.

The outline is read globally even when content comparison is restricted to a
smaller regression range.

For De Decretis this means the complete 1,479-page outline is observed while
block comparison remains restricted to pages 512-561.

## Deterministic block comparison

Internal outline destinations inside the selected comparison range are matched
against included normalized blocks on the destination page.

Accepted diagnostic match classes are intentionally conservative:

1. exact text, ignoring case and outer whitespace;
2. normalized text, using collapsed whitespace and outer punctuation trimming;
3. compact text, retaining only alphanumeric characters.

These are observations only. None of these matches creates a production
heading.

For an unmatched outline entry, the report also lists up to three same-page
lexical candidates ranked by title-token coverage. Candidate ranking is
diagnostic assistance, not an accepted match and not a production confidence
score.

## Structural overlap

The report distinguishes:

```text
outline entry -> matched production heading
outline entry -> matched non-heading block
outline entry -> unmatched
production heading -> supported by matched outline entry
production heading -> unsupported by outline
```

A matched non-heading block is especially interesting because it is a concrete
outline-only structural candidate that current automatic typography does not
promote.

An unsupported production heading is equally important: it demonstrates why
native outline evidence cannot simply replace automatic detection.

## Decision rule

8.5a must not directly change production behavior.

After reviewing the two real-corpus reports:

- if outline evidence is absent or unusable, no outline production model is
  justified;
- if it is useful but incomplete, the next design should treat it as an
  independent structural evidence source;
- if hierarchy and destinations are reliable, a later neutral model may retain
  title, level, ordering, destination page, and destination coordinates;
- reconciliation rules must be evaluated separately before they can affect
  segmentation.

The desired architecture remains:

```text
automatic typography/layout evidence
             +
native PDF outline evidence
             +
optional caller editorial evidence
             |
             v
future deterministic structural reconciliation
```

No one source is assumed to be universal structural truth.
