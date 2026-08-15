# Native/layout text pairing v1

## Status

Phase 21C.2B.3 production pairing model.

This increment freezes the pairing model only. It does **not** yet execute the
native-present hybrid reconciliation route.

## Evidence behind the decision

Phase 21C.2B.2 evaluated the selected native/layout diagnostic corpus:

```text
native words:                 5,756
OCR-authorized layout targets:   68

direct word ownership
  0 targets:    59
  1 target:  5,697
  >1 targets:    0

current projector-span membership
  0 targets:    59
  1 target:  5,697
  >1 targets:    0
```

The diagnostic also showed one legitimate multi-block target:

```text
Ehrman p36
layoutSeq=8
Heading
source blocks=[7,8]

eox 1 .1 The Canon of Scripture
```

Therefore source-block count is not an ambiguity criterion.

The earlier corpus evidence also showed that one coarse PdfPig source block can
be partitioned across multiple finer layout targets. That is likewise not an
error when the projected word evidence is disjoint.

## Production model

Pairing is target-centric:

```text
OCR-authorized layout target
        |
        +--> native source block A --project--> extent A
        |
        +--> native source block B --project--> extent B
        |
        +--> ...
        |
        v
ComparableNativeTextEvidence
```

`ComparableNativeTextExtent` remains the provenance-preserving projection of
one source block onto one layout target.

`ComparableNativeTextEvidence` is the deterministic aggregate of one or more
such extents for the same target.

Source blocks are provenance parts, not mutually exclusive candidates.

## Pairing statuses

```text
NoNativeEvidence
Comparable
AmbiguousWordOwnership
```

### NoNativeEvidence

No native word extent projects onto the target.

No native text is silently promoted.

### Comparable

One deterministic target-centric aggregate exists.

It may contain evidence from one or more source blocks.

### AmbiguousWordOwnership

At least one projected native word is claimed by more than one OCR-authorized
layout target.

This fails closed:

```text
ComparableNativeEvidence = null
```

The conflicting words remain available as diagnostic evidence.

## What is deliberately absent

V1 pairing introduces no:

- overlap threshold;
- IoU threshold;
- max-overlap winner;
- OCR confidence threshold;
- fuzzy text similarity;
- language model arbitration;
- authority selection;
- reconciliation decision.

Those concerns do not belong in pairing.

## Unmatched native evidence

Native words outside OCR-authorized layout targets are not automatically
attached to any target.

On `Unverified` or `Suspicious` pages they therefore cannot silently become
authoritative merely because the native PDF layer contains them.

This is particularly important for recurring headers, page numbers, corrupted
glyphs, and native material excluded by the layout policy.

## Next step

Phase 21C.2B.4 will consume this pairing model in the native-present hybrid page
execution route.

Pinned route controls remain:

```text
De Decretis
  Healthy -> NativeOnly

Ehrman p405
  Unverified -> targeted OCR reconciliation
  Agreement / NativePdf

Ehrman p380
  Unverified -> targeted OCR reconciliation
  Conflict / None

Ehrman p233
  Missing -> OCR recovery
  Figure -> PreserveVisualWithoutOcr
```

The pairing model must not weaken those semantic controls.
