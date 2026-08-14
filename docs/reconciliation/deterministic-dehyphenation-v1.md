# Deterministic reconciliation dehyphenation V1

## Status

Phase B / 17D production-boundary increment.

```text
17 Native/OCR reconciliation
   17A deterministic reconciliation boundary      DONE
   17B real evidence + human diagnosis            DONE
   17C comparable native text extent              DONE
   17D deterministic dehyphenation                THIS INCREMENT
   17E real reconciliation regression             NEXT
```

## Why 17D exists

Phase 17B human review and Phase 17C spatial correction separated two different
causes of apparent disagreement:

1. native and OCR evidence were not always the same textual extent;
2. line-break hyphenation produced different strings for the same logical word.

Phase 17C resolved the first issue. On the real pages 405 and 380, the comparable
native extents reached approximately 0.995 diagnostic edit similarity with OCR
before any new comparison policy was introduced.

17D addresses only the second issue.

## Evidence-specific rules

`ReconciliationTextDehyphenator` deliberately uses source evidence rather than a
generic `"- "` replacement.

### Native comparable extent

PdfPig native words may retain discretionary U+00AD soft hyphens.

V1:

- removes U+00AD characters;
- when a native word ends with U+00AD and the next native word begins with a
  lowercase Unicode letter, joins the two word fragments without a space;
- preserves ordinary ASCII hard hyphens between native words because a word
  boundary alone does not prove that the hyphen is discretionary.

Example:

```text
compan<U+00AD> + ions
        ->
companions
```

### OCR region

PaddleOCR observations retain region-local fragment boundaries.

V1:

- orders OCR fragments by `ObservationSequence`;
- when one OCR observation ends in ASCII `-` and the next observation begins
  with a lowercase Unicode letter, removes the boundary hyphen and joins the
  fragments;
- preserves hard hyphens that occur inside a single OCR observation;
- preserves a boundary hyphen when the next observation begins with uppercase.

Example:

```text
observation 0: compan-
observation 1: ions
        ->
companions
```

The OCR rule is intentionally limited to an observation boundary. It does not
remove arbitrary `"- "` sequences inside a recognized fragment.

## Explicit non-rules

17D does not normalize or correct:

- case;
- punctuation;
- spelling;
- OCR substitutions;
- native extraction substitutions;
- missing clauses;
- semantic equivalence.

Therefore examples such as these remain real divergences:

```text
conversion / conversior
hLstorian / historian
So:ne / Some
```

The implementation uses no:

- edit-distance threshold;
- dictionary;
- spell checker;
- OCR confidence threshold;
- language model;
- cross-source fuzzy alignment.

## Result evidence

`TextDehyphenationResult` records:

- dehyphenated text;
- U+00AD removal count;
- joined-boundary count;
- whether any transformation occurred.

The source `ComparableNativeTextExtent` and `OcrRegionResult` remain unchanged.

## Known limitation

The OCR rule is a deterministic heuristic. A legitimate lexical hyphen that
happens to occur at an OCR observation boundary before a lowercase continuation
may be joined incorrectly.

V1 accepts this bounded risk because:

- the rule is restricted to OCR observation boundaries;
- it matches the existing document-normalization direction of treating
  lowercase line continuations as discretionary hyphenation;
- real Phase 17B evidence contains exactly this OCR line-break artifact;
- 17E will evaluate reconciliation again before any broader policy change.

Do not broaden this rule without additional corpus evidence.

## Phase 17D live acceptance

The live evaluator reuses the real Phase 17B OCR observations and the corrected
Phase 17C native coordinate space. It does not call Paddle services.

For real pages 405 and 380 it requires:

- non-empty comparable native extents;
- at least one deterministic native boundary join;
- at least one deterministic OCR boundary join;
- dehyphenated diagnostic similarity to improve relative to the already
  comparable raw extent;
- page 405 to become conservatively equivalent after dehyphenation;
- page 380 to remain explicitly non-equivalent after dehyphenation, proving that
  real character-level differences are not hidden by the rule.

No similarity threshold is used as an authority decision.

## Deliberate non-decisions

17D does not:

- modify `NativeOcrTextReconciler`;
- change `TextReconciliationDecision`;
- introduce `MinorCharacterDivergence`;
- choose OCR over native;
- merge native and OCR text;
- change `DocumentTextNormalizer`;
- perform cross-page reconciliation.

## Next

Phase 17E should integrate the comparable extent and deterministic dehyphenation
into a real reconciliation regression, then decide from evidence whether the
remaining character-level differences justify any new explicit reconciliation
state or comparison rule.
