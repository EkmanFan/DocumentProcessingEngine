# Structural segmentation v2

## Purpose

Increment 8.4b replaces the text-only page-bounded segmenter with deterministic
multi-signal heading evidence and cross-page heading-led structures.

The profile is:

```text
typography-aware-cross-page-fallback-v2
```

## Evidence model

A block is considered as a heading only after a minimal text-quality gate:

- non-empty;
- at most 180 characters;
- at least three letters;
- no Unicode replacement character;
- no control characters;
- at least half of non-whitespace characters are letters or digits;
- at most 24 extracted words.

Heading evidence then follows three deterministic paths.

### Explicit structural heading

Examples:

```text
Chapter 3: ...
Part II ...
Section 4 ...
1. Introduction
1.2 Background
IV. Discussion
```

When typography exists, an explicit heading must not be materially smaller than
the document body-font baseline:

```text
font ratio >= 0.95
```

This deliberately rejects small running labels such as Ehrman's repeated
8-point `Chapter ...` lines against a 9.5-point body baseline.

A bare leading number without structural punctuation is not explicit heading
evidence.

### Typographic heading

Historical generic thresholds remain useful evidence:

```text
heading candidate ratio >= 1.18
section-strength ratio >= 1.30
```

Below `1.30`, sentence-like blocks ending in `.`, `;`, or `,` are rejected.

### Uppercase heading

A short all-uppercase label may be a heading. When typography exists it requires
a modest lift:

```text
font ratio >= 1.10
```

When typography is unavailable, explicit and uppercase rules remain available
as a conservative fallback for other formats/backends.

## Body-font baseline

The body baseline is the word-count-weighted median point size of included
normalized blocks with usable typography.

This is deterministic and document-local.

## Segmentation flow

The structural flow is no longer globally page-bounded.

```text
unheaded content
    -> page-bounded fallback

recognized heading
    -> start structured segment

following body blocks
    -> remain in that structured segment across physical pages

next recognized heading
    -> close previous structured segment and start the next one
```

This restores an important distinction that v1 intentionally deferred:
uncertain fallback stays bounded, while recognized intellectual structure may
span pages.

## Provenance

`DocumentSegment.SourceBlocks` retains the exact normalized block references in
processing order.

`FirstPhysicalPageNumber` and `LastPhysicalPageNumber` describe the pages that
actually contributed blocks to the segment.

Segment identifiers remain deterministic and document-local, based on first
physical page plus segment ordinal.

## Deliberate non-goals

This increment does not add:

- semantic segment kinds;
- document-specific heading lists;
- LLM classification;
- retrieval chunks;
- font-name-specific publisher rules;
- tuning constants merely to force an Ehrman segment count.

The real-corpus diagnostics remain the arbiter of whether this profile is
better than v1.
