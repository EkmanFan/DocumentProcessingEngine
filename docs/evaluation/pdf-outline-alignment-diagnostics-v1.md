# PDF outline/content alignment diagnostics v1

## Purpose

Increment 8.5b explains why a correct native PDF bookmark destination may fail
a strict title-to-block equality check.

Increment 8.5a established:

- Ehrman: 48 internal outline entries with valid pages and coordinates, but no
  exact/normalized/compact same-page text matches;
- De Decretis: 471 outline entries globally and 7 exact block matches among
  the 8 entries targeting pages 512-561.

A manual PDF-reader check separately confirmed that the Ehrman bookmarks point
to the intended pages. Therefore 8.5b does not attempt to "repair" bookmark
page numbers.

## Scope

This increment remains evaluation-only.

It does not modify:

- Core;
- PDF extraction;
- normalization;
- heading detection;
- optional heading hints;
- production segmentation.

## Questions

For each internal outline entry in the selected evaluation range:

1. Does the exact target page expose native words and layout blocks?
2. Is that page textless and dominated by a raster image?
3. Which bookmark destination coordinates are usable after normalization?
4. Does one normalized block, or a cluster of up to three adjacent blocks,
   contain the same title evidence?
5. If the exact target page contains no useful native textual alignment, is
   the same title visible in native text on a nearby physical page?
6. When a leading numeric label exists, is it present on both sides, one side,
   or conflicting?

The nearby-page window is diagnostic only. A candidate on `P+1` does not mean
that a bookmark targeting `P` is wrong. For example, `P` may be a graphical or
raster chapter opener while native body text begins on the following page.

## Destination geometry

PdfPig reports PDF destination coordinates in source PDF space. Production
text blocks use normalized top-left geometry.

For comparison, 8.5b derives:

```text
normalizedLeft = left / sourceWidth
normalizedTop  = 1 - top / sourceHeight
```

when the relevant coordinate exists and is finite.

The report retains the raw coordinates as well.

For target-page candidates it records:

- vertical distance from the destination top to the candidate rectangle;
- point-to-rectangle distance when both left and top are available.

No coordinate threshold is used as a production decision.

## Adjacent block clusters

A printed structural heading can be split across several layout blocks while a
PDF bookmark stores one continuous title.

8.5b therefore generates deterministic clusters of:

```text
1 block
2 adjacent blocks
3 adjacent blocks
```

using existing reading order and source sequence.

This is diagnostic composition only. It does not alter normalized blocks.

## Lexical evidence

The evaluator reports separate, explainable observations instead of a single
opaque score:

```text
shared token count
outline token count
candidate token count
outline token coverage
candidate token coverage
containment relation
```

Containment is classified as:

```text
Equal
OutlineWithinCandidate
CandidateWithinOutline
None
```

Non-equal compact containment is deliberately hardened. The shorter compact
representation must contain at least 8 alphanumeric characters and represent
at least 20% of the longer compact representation.

This rejects accidental one-character substring matches such as `I` or `L`
while preserving useful damaged-text cases such as `I ntroduction` matching
`Introduction`.

Diagnostic alignment bands are then derived:

```text
ExactEquivalent
Containment
HighOverlap
ModerateOverlap
WeakOverlap
None
```

For reporting, the bands are grouped explicitly:

```text
plausible alignment:
  ExactEquivalent
  Containment
  HighOverlap

exploratory candidate:
  ModerateOverlap
  WeakOverlap
  None
```

"Exploratory candidate" means only that lexical evidence exists nearby. It is
not counted as a structural alignment.

The thresholds are documented evaluation categories, not production
confidence values or production decisions.

## Numeric labels

A leading Arabic numeric label is treated independently from title evidence.

Examples:

```text
outline:   "28. Some Title"
content:   "Chapter 28: Some Title"
relation:  Same

outline:   "Some Title"
content:   "Chapter 28: Some Title"
relation:  CandidateOnly
```

A missing number on one side does not invalidate strong title overlap.

This is important because bookmark labels and printed headings are editorial
representations of the same structure, not guaranteed byte-for-byte copies.

## Nearby pages

Candidates are inspected in:

```text
P-2 .. P+2
```

but the exact target page remains explicitly identified in every observation.

This window exists to answer a content-layer question:

> Where does equivalent native text appear relative to a correct bookmark
> destination?

It is not a bookmark correction heuristic.

## Decision rule

8.5b must still not change production.

The next architectural decision should be based on the observed combination of:

- native outline hierarchy;
- valid target page;
- destination geometry;
- target-page text/raster state;
- adjacent-block title evidence;
- lexical overlap;
- independent production typography.

Only after those observations are reviewed should native outline evidence be
introduced into a neutral production model or reconciliation step.
