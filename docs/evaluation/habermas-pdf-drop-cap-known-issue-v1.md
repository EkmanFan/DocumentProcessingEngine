# Habermas PDF drop-cap reconciliation — known issue V1

## Status

**Deferred — recorded 2026-08-28**

The full Manager-produced result for *The Case for the Resurrection of Jesus*
is custody-consistent, but exposes a systematic PDF text-quality defect around
decorative drop caps.

Examples include separate `I` + `n July 2000…`, `O` + `ne Sunday morning…` and
`B` + `efore we approach…` elements. The single-letter element may remain in
the preceding structural segment together with the following `Chapter N`
marker, while the paragraph begins in the next segment without its initial
letter.

This is an Engine PDF reconciliation/segmentation concern, not a Manager,
PostgreSQL or visual-custody defect. Any remediation must be generic, based on
neutral typographic/location evidence, and validated against multiple existing
PDF corpora. A title-specific or filename-specific rule is forbidden.

## Acceptance direction

- merge a genuine decorative initial with the adjacent paragraph without
  merging ordinary one-character content;
- retain exact provenance for both source fragments;
- keep chapter boundaries with the new chapter rather than the preceding
  segment;
- add focused Habermas evidence plus cross-corpus negative regression cases;
- do not run an unqualified full-corpus sweep as part of diagnosis.
