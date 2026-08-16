# Phase 21E.1H.4B — Deterministic structural visual enrichment V1

## Status

Production structural-evidence acquisition plus frozen real-corpus parity.

This increment completes the production observation chain:

```text
VisualRasterObservation
        +
DocumentTextNormalizationResult
        +
native DocumentWord evidence
        ↓
DefaultVisualStructuralEvidenceEnricher
        ↓
VisualEvidenceObservation
```

It does **not**:

- assign `VisualDisposition`;
- change `PageProcessingRoute`;
- modify `GuardedDocumentPageExecutionPlanner`;
- modify `DocumentProcessor`;
- invoke PP-StructureV3;
- invoke OCR;
- tune any frozen Phase 21E.1F threshold.

Runtime execution therefore remains unchanged.

---

## 1. Why structural enrichment is separate from H.4A

H.4A promoted deterministic raster/pixel measurement into production.

A complete `VisualEvidenceObservation` also requires:

```text
heading association
native-text containment
caption association
```

Those signals depend on normalized native document structure, not only pixels.

H.4B therefore consumes the H.4A raster observation instead of decoding PDF
images again.

This keeps:

```text
source decoding / pixel measurement
!=
structural evidence
!=
evidence classification
!=
processing policy
```

---

## 2. Production heading truth is reused, not copied

The Phase 21E.1F diagnostic did not invent its own heading classifier. It invoked
the engine's production `HeadingEvidenceEvaluator` over the real
`DocumentTextNormalizationResult`.

H.4B does the same directly inside the Engine assembly.

The typography rules in `NativeHeadingEvidenceRules` remain the single source of
truth for automatic native heading inference.

Only the visual-to-heading relationship thresholds are promoted here.

---

## 3. Frozen heading-association thresholds

The following Phase 21E.1F values are promoted unchanged:

```text
StrongHeadingDistance              = 0.025
PossibleHeadingDistance            = 0.060
StrongVerticalOverlap              = 0.45
MinimumHeadingVisualHeightRatio    = 0.35
MaximumHeadingVisualHeightRatio    = 3.50
```

Strong association requires:

```text
nearest heading exists
AND rectangle distance <= 0.025
AND vertical overlap >= 0.45
AND visual/heading height in [0.35, 3.50]
AND low native-text pixel interaction
```

Low interaction is exactly:

```text
NoForegroundWordIntersection
OR LowForegroundWordInteraction
OR BlankCanvas
```

Otherwise a nearest heading within `0.060` yields `PossibleAdjacentVisual`.

---

## 4. Frozen native-text containment

For each included normalized structural block:

```text
intersection ratio = intersection area / block area
centerContained     = visual contains block center
fullyContained      = visual contains block with tolerance 0.003
```

A block counts as contained when:

```text
centerContained
OR fullyContained
OR intersection ratio >= 0.75
```

A body block is paragraph-like when:

```text
word count >= 12
OR character count >= 80
```

Evidence mapping is unchanged:

```text
0 contained words
    -> NoContainedNativeText

heading present
AND contained body words <= max(3, contained heading words / 2)
    -> HeadingDominatedContainedText

contained body blocks >= 2
OR paragraph-like body blocks >= 1
OR contained page word ratio >= 0.08
    -> TextRichContainer

otherwise
    -> SparseContainedText
```

Excluded normalized recurring-margin blocks do not participate.

---

## 5. Frozen caption association

Caption evidence keeps the Phase 21E.1F precedence.

### Native lexical lead word first

The first path searches native words for:

```text
Figure
Fig
Table
Plate
Exhibit
```

case-insensitively after trimming trailing `:` or `.`.

The word must be vertically above/below the visual, have gap `<= 0.08`, and have
its center-X inside the visual span plus/minus `0.03`.

Nearest candidates are ordered by:

```text
gap
word top
```

Evidence is:

```text
gap <= 0.06 -> StrongAssociation
otherwise   -> PossibleAssociation
```

### Normalized body block fallback

Heading blocks are excluded.

Candidates require gap `<= 0.08` and at least one of:

```text
horizontal overlap >= 0.10
center aligned within +/- 0.02
generic lexical caption hint
```

Ordering is:

```text
lexical hint descending
gap ascending
horizontal overlap descending
source sequence ascending
```

Caption-like text is:

```text
2..50 words
AND <= 320 characters
```

Strong:

```text
lexical hint
AND caption-like text
AND gap <= 0.06
AND (horizontal overlap >= 0.15 OR center aligned)
```

Possible:

```text
caption-like text
AND gap <= 0.025
AND horizontal overlap >= 0.50
```

No threshold is retuned in H.4B.

---

## 6. Alignment and fail-closed rules

H.4B requires:

```text
normalization.SourceExtraction is the supplied extraction
normalization page count == extraction page count
raster page count == extraction page count
physical page identities align
normalized page source reference == extraction page
raster visual count == RasterImageCount
source visual order == 0..N-1
```

Missing or reordered evidence is an integration error.

If a measured H.4A effective visual geometry cannot be represented safely by
the final bounded `effectiveVisualAreaRatio` contract, H.4B converts that visual
to an Unknown-equivalent final observation:

```text
ForegroundState = Unavailable
structural evidence = NotMeasured
```

It does not guess a semantic class.

---

## 7. Frozen real-corpus parity gate

The delivery script requires the exact Phase 21E.1F raw CSV:

```text
SHA-256
50fc29337bd827278a3853d9f811b42b1ca05adb545961d9257c8a43705a5a9e
```

It also requires the exact p380 geometry diagnostic artifact:

```text
SHA-256
6fd15aea37bbb33bc2975a4061c12ea39ed801ca77e914eeef27872a0aaa35b2
```

That diagnostic proved, on Ehrman physical page 380 / source visual 0, that the
three historical numeric differences:

```text
effectiveVisualTop
effectiveVisualBottom
effectiveVisualAreaRatio
```

are caused solely by PDF rectangle canonicalization.

The frozen H.1F harness mapped raw `BoundingBox` edge properties. Production
H.4A reuses `PdfPageCoordinateSpace`, which canonicalizes the `PdfRectangle`
before mapping.

The diagnostic proved all of the following:

```text
same PDF image occurrence
same 506x651 decoded raster
same foreground ratio
same ForegroundWordInteraction
same zero significant components
same inferred effective pixel rectangle: L=134 T=56 R=136 B=61
legacy raw-box remap == frozen H.1F effective bounds/area
normalized-box remap == production H.4A effective bounds/area
```

Therefore H.4B does **not** weaken numeric tolerance and does **not** restore the
old non-canonical geometry.

It runs the current production chain over the pinned Ehrman, De Decretis and
Habermas PDFs:

```text
PdfPigDocumentExtractor
    ↓
DocumentTextNormalizer
    ↓
PdfPigVisualRasterObservationSource
    ↓
DefaultVisualStructuralEvidenceEnricher
    ↓
DefaultVisualEvidenceAssessor
```

For every H.1F row that was actually analyzed, it compares:

```text
foreground state
foreground ratio
pixel/native-word interaction
native words touched ratio
significant component count
effective visual bounds
effective visual area
heading association
native-text containment
caption association
final VisualEvidenceKind
```

Numeric comparisons still use a strict absolute tolerance of `1e-12`.

The only accepted historical divergences are the exact three proven p380/i0
geometry fields above, and only when:

```text
the exact diagnostic JSON is present and SHA-pinned
diagnostic schema/baseline/page/image are exact
causeProven == true
frozen values match the diagnostic's frozen values
candidate values match the diagnostic's production values
all three expected exception fields occur exactly once
no other mismatch exists
```

The parity report distinguishes:

```text
strictHistoricalNumericParity
acceptedCanonicalGeometryDivergenceCount
unexpectedMismatchCount
semanticParityExact
productionParityAccepted
```

For the pinned baseline, successful parity is expected to mean:

```text
strictHistoricalNumericParity = false
acceptedCanonicalGeometryDivergenceCount = 3
unexpectedMismatchCount = 0
semanticParityExact = true
productionParityAccepted = true
```

Any additional or altered mismatch fails the increment.

The script does not alter production thresholds or classifier policy to make a
mismatch disappear.

---

## 8. Representative safety semantics

The frozen evidence precedence remains:

```text
strong caption before text-rich container
```

so a labelled meaningful figure such as Ehrman p79 cannot be collapsed into
presentation-only frame evidence merely because native text lies inside its
effective visual bounds.

Likewise:

```text
semantic title text
!=
decorative pixels around the title
```

for cases such as Ehrman p2.

And Missing/Suspicious text authority remains outside this component entirely.

---

## 9. Next boundary

After H.4B passes and is committed, the production system will finally be able
to construct complete deterministic `VisualEvidenceObservation` values from a
real PDF without the disposable diagnostic harness.

The next step is then:

```text
21E.1H.4C
    wire the complete observation chain into DocumentProcessor
    in true shadow mode only
    compare legacy vs candidate plans
    execute legacy path unchanged
```

Cutover remains later and requires explicit real-corpus safety and performance
evidence.
