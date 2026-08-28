# PDF drop-cap reconciliation — validation V1

## Status

**Resolved — validated 2026-08-29**

The Manager-produced result for *The Case for the Resurrection of Jesus* was
custody-consistent, but exposed a systematic PDF text-quality defect around
decorative drop caps.

Examples include separate `I` + `n July 2000…`, `O` + `ne Sunday morning…` and
`B` + `efore we approach…` elements. The single-letter element may remain in
the preceding structural segment together with the following `Chapter N`
marker, while the paragraph begins in the next segment without its initial
letter.

This was an Engine PDF reconciliation/segmentation concern, not a Manager,
PostgreSQL or visual-custody defect.

## Implemented contract

The PDF-native repair now attaches an isolated uppercase initial only when one
and only one multiline paragraph provides the complete evidence set:

- the paragraph begins with a lowercase alphabetic suffix;
- the initial is materially larger than the paragraph typography;
- horizontal gap, top alignment, vertical overlap and descent all match a
  decorative drop cap;
- the relationship is unambiguous in both directions.

The rule contains no title, filename, language or book-specific vocabulary.
The repaired block retains every original `DocumentWord` and its
`SourceSequence`; its block provenance anchor remains the earliest contributing
source sequence. Its reading order remains that of the paragraph so a drop cap
observed before a chapter heading cannot pull the paragraph into the preceding
segment. Numeric superscript repair runs first, then drop-cap reconciliation,
which prevents the former from reordering the reconstructed initial.

## Qualified validation

- Habermas page 70: `E` + `ven if all…` becomes `Even if all…` under
  `Chapter 7`;
- Habermas page 78: `N` + `aturalism views…` becomes `Naturalism views…`
  under `Chapter 8`;
- focused segmentation assertions keep each repaired paragraph in a structured
  segment after its chapter boundary, never in the preceding fallback segment;
- Ehrman page 79 and De Decretis page 512 are qualified negative corpus checks
  and produce no invented drop-cap reconstruction;
- synthetic negatives cover uppercase/non-suffix text, one-line content and
  ambiguous geometry;
- the existing numeric-superscript repair suite plus a combined
  superscript/drop-cap case protect note-marker behavior.

No unqualified full-PDF-corpus run was used for this validation.
