# Habermas p28 `figure_title` review V1

## Status

**DEFERRED EXPECTED — no product correction justified**

The qualified fixture SHA-256 is
`bdea4b71616b8f1fb016742f68e884de51121a3dfad64b4b2a288212b5b67176`.
One live PP-StructureV3 call at 300 DPI reproduced these observations:

1. narrative `text` before the diagram;
2. the diagram as `image`;
3. the large narrative paragraph below it as `figure_title`;
4. `They claimed it.` as `footer`.

The `figure_title` label is semantically wrong as provider output. It is not,
by itself, an authoritative documentary fact.

## Product impact review

The existing p18/p28 public-path reference already establishes the accepted
result behavior:

- native PDF text remains authoritative;
- `They claimed it.` retains native source block sequence `2` and follows the
  preserved diagram;
- no Figure OCR is executed;
- exactly one meaningful visual is preserved;
- unknown/footer layout output is not promoted to authoritative text.

The provider mislabel therefore does not currently cause silent text loss,
duplicate narrative content, incorrect OCR authority, or loss of the diagram.
Changing the global `figure_title` mapping from this single example would be a
title-specific over-correction and could weaken valid caption associations.

The observation remains frozen as **Deferred expected**. It becomes FAIL only
if a result-level regression demonstrates missing, duplicated, reordered, or
incorrectly authoritative content.

No Engine or adapter code is changed by this review.
