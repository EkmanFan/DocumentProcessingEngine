# Document Processing Engine — Phase 15.3 Independent Visual Remediation

**Date:** 2026-08-18
**Baseline entering remediation:** `d6591f0`
**Phase:** 15.3 — remediate demonstrated semantic gaps
**Status:** ACCEPTED — uncommitted

## 1. Demonstrated gap

Phase 15.1 established independent human ground truth that these pages contain meaningful visual structures that must be preserved:

```text
Habermas p34
Habermas p36
Habermas p38
Habermas p44
```

The Phase-15.2 live layout regression reproduced the defect:

```text
PP Figure
→ no strong caption
→ VisualEvidenceKind.Unknown
→ RequiresVisualAnalysis
→ no positive preservation
```

The known green captioned controls remained:

```text
Ehrman p233
Habermas p40
Habermas p43
```

## 2. Phase 15.3A diagnostic

The read-only PP-StructureV3 diagnostic measured every selected real-corpus fixture that currently emits a Figure:

| Control | Ground-truth role | Current evidence | Figure visible-area ratio | Text-like intersections |
|---|---|---:|---:|---:|
| Ehrman p36 | decorative negative | Unknown | 0.004494 | 0 |
| Ehrman p79 | captioned positive | CaptionedMeaningfulVisual | 0.040231 | 0 |
| Ehrman p233 | captioned positive | CaptionedMeaningfulVisual | 0.139178 | 0 |
| Ehrman p405 | unknown/deferred negative | Unknown | 0.000824 | 0 |
| Habermas p34 | meaningful independent | Unknown | 0.478297 | 0 |
| Habermas p36 | meaningful independent | Unknown | 0.417730 | 0 |
| Habermas p38 | meaningful independent | Unknown | 0.536621 | 0 |
| Habermas p40 | captioned positive | CaptionedMeaningfulVisual | 0.371717 | 0 |
| Habermas p43 | captioned positive | CaptionedMeaningfulVisual | 0.229388 | 0 |
| Habermas p44 | meaningful independent | Unknown | 0.354482 | 0 |

Two candidate signals were rejected:

- `LargestRasterImageAreaRatio`: the negative Ehrman controls are around 0.67 and therefore this source-image metric does not separate semantic visual meaning.
- native word count: p34/p36/p38 have no native words, while p44 has 315; native extraction state is not a coherent visual-meaning classifier.

The useful layout-local evidence is:

```text
substantial visible Figure area
+
no caption evidence
+
no spatial intersection with Text / Heading / Table observations
```

## 3. Deterministic rule

`DefaultLayoutVisualEvidenceAssessor` now preserves its existing caption rule first.

When there is no caption evidence, a Figure becomes:

```text
LargeIndependentVisual
```

only when:

```text
visible Figure area >= 0.25 of page
AND
Figure does not intersect Text / Heading / Table
```

Otherwise the Figure remains:

```text
Unknown
```

Any caption evidence that does not produce exactly one strong caption association remains fail-closed to `Unknown`.

The `0.25` boundary is a deliberately simple V1 definition of “substantial”: one quarter of visible page. It is not learned from the old unit-test geometry and does not treat current tests as ground truth. On the measured real corpus, the smallest independent meaningful visual is p44 at `0.354482`, while the two real unsupported negative Figures are `0.004494` and `0.000824`.

Visible area is clipped to the canonical page rectangle before measuring coverage so out-of-page coordinates cannot inflate the evidence.

## 4. Architectural boundary

The change remains inside the existing evidence vocabulary:

```text
LayoutObservationKind.Figure
→ DefaultLayoutVisualEvidenceAssessor
→ LargeIndependentVisual
→ VisualEvidenceDispositionPolicy
→ PreserveMeaningfulVisual
```

No new evidence kind, policy layer, model backend, OCR behavior, source-visual identity rule, or special-case corpus/page logic was added.

`Figure OCR = 0` remains unchanged.

## 5. Unit-test protection

The unit suite now distinguishes:

```text
small no-caption Figure
→ Unknown

large no-caption Figure
→ LargeIndependentVisual

large Figure separated from body text
→ LargeIndependentVisual

large Figure intersecting body text
→ Unknown

large Figure with ambiguous/distant caption evidence
→ Unknown

out-of-page bounds whose visible area is small
→ Unknown
```

The controlled candidate runtime also verifies that a large independent Figure:

```text
→ remains excluded from OCR
→ produces LargeIndependentVisual evidence
→ is materialized as preserved visual evidence
```

The existing controlled-runtime test for a neutral Figure now uses a genuinely small synthetic Figure (`0.16` visible page area) so it continues to test the `Unknown` branch. The prior synthetic geometry occupied `0.32` of the page and therefore satisfied the new independently established semantic rule; retaining `Unknown` for that fixture would have made the old test shape override the Phase-15 ground truth.

The previous blanket test “FigureWithoutCaption -> Unknown” was intentionally narrowed to a small Figure. Its old premise is the behavior being remediated and therefore is not treated as independent ground truth.

## 6. Real-corpus acceptance

Acceptance requires:

```text
normal build/tests
→ PASS

full semantic regression --layout-mode all-pass
→ PASS

layout controls
→ 7 semantic PASS
→ 0 semantic FAIL

historical baseline mismatches
→ exactly 4
→ Habermas p34/p36/p38/p44

native/provenance
→ PASS

real PP + PaddleOCR
→ p233/p380/p405 PASS
→ Figure OCR = 0

evaluation mutation check
→ permanent candidate unchanged
```

## 7. Remaining uncertainty

This is a deterministic V1 heuristic, not a universal semantic-vision solution.

The current selected real corpus has a wide separation between the four meaningful independent Figures and the two unsupported negative Figures, but future corpora may contain:

- large decorative images with no text overlap;
- meaningful independent visuals smaller than one quarter page;
- layout boxes whose overlap geometry is noisy.

Those cases must remain subject to regression evidence. `Unknown` remains the fail-closed outcome when the deterministic evidence is insufficient.

## 8. Phase transition

After commit:

```text
Phase 15 — Semantic regression
DONE

Phase 16 — Performance / memory acceptance
NEXT
```
