# Structural segmentation v4 — optional external heading hints

## Status

Increment 8.4g adds optional caller-provided editorial heading hints while
leaving the strict automatic typography policy from v3 unchanged.

Production profile:

```text
strict-typography-optional-hints-cross-page-fallback-v4
```

## Separation of evidence

A block starts a structural segment when either evidence path succeeds:

```text
automatic evidence
    -> strict typography v3

explicit external evidence
    -> caller-provided heading hint
```

The two paths are deliberately separate.

Automatic inference still requires:

```text
text length                   <= 180
word count                    <= 24
letter count                  >= 4
alphanumeric / non-whitespace >= 0.55
font ratio                    >= 1.18
sentence rejection            below ratio 1.30
```

Hints do not weaken or modify these automatic rules. A hint is explicit
editorial evidence supplied by the caller.

## Neutral options contract

`DocumentSegmentationOptions` carries optional `HeadingHints`.

The engine contains no Ehrman-, Apologia-, theology-, or publisher-specific
heading strings.

Empty/default options preserve the v3 structural result.

## Hint matching

For each supplied hint the engine evaluates deterministic text-only matching:

1. normalized exact match;
2. short decorated-prefix suffix match;
3. compact first-source-line match.

Normalization collapses whitespace, trims outer punctuation, and compares
case-insensitively through uppercase invariant keys.

Compact matching removes non-alphanumeric characters. It exists specifically
for extraction artifacts such as split letters:

```text
WHAT DO YOU THI NK?
```

matching the explicit hint:

```text
WHAT DO YOU THINK?
```

## Validation

Options reject:

- null/blank hint values;
- hints containing no letter or digit.

Duplicate hints are removed case-insensitively while preserving first-seen
order.

## Segmentation flow

The grouping algorithm is unchanged:

```text
recognized heading
    -> heading-led segment may span physical pages

unstructured content before recognized structure
    -> page-bounded fallback

next recognized heading
    -> closes the previous structured segment
```

## Pinned production regression

Without hints:

```text
Ehrman:
  267 segments
  267 headings
  166 cross-page
   50 small <=120

De Decretis:
   50 segments
    0 headings
   50 fallback
```

With the three external Ehrman evaluation hints:

```text
TAKE A STAND
WHAT DO YOU THINK?
SUGGESTIONS FOR FURTHER READING
```

production must reproduce the independently measured strict+hints policy:

```text
Ehrman:
  274 segments
  274 headings
  168 cross-page
   53 small <=120
```

The seven additional boundaries are evaluation evidence only. The hint strings
live in the evaluation runner, not in Core or Engine.

De Decretis remains at 50 page-bounded fallback segments under the same
evaluation hint set.

## Scope

This increment does not add:

- a segmentation rule framework;
- plugins;
- source-specific profiles in the engine;
- semantic classification of headings;
- retrieval chunking;
- RAG concerns.

The contract is intentionally limited to the evidence required by the validated
use case.
