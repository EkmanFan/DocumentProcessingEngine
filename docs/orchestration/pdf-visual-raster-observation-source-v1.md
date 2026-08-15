# Phase 21E.1H.4A — PDF visual raster observation source V1

## Status

Production low-level evidence acquisition only.

This increment promotes the raster/pixel measurement portion of the frozen
Phase 21E.1F diagnostic algorithm into production code.

It deliberately does **not**:

- create a complete `VisualEvidenceObservation`;
- infer heading association;
- infer native-text containment;
- infer caption association;
- assign `VisualEvidenceKind`;
- assign `VisualDisposition`;
- invoke PP-StructureV3;
- invoke PaddleOCR;
- modify `GuardedDocumentPageExecutionPlanner`;
- modify `DocumentProcessor`;
- change runtime routing.

Current document-processing execution therefore remains unchanged.

---

## 1. Why H.4 is split

`VisualEvidenceObservation` is a complete evidence contract.

Its classifier can legitimately interpret combinations of:

```text
foreground
pixel/native-word interaction
effective visual extent
heading association
native-text containment
caption association
```

Creating that final object with only the raster fields populated would be
unsafe.

For example, absence of measured heading/caption structure must not silently
mean that no such relationship exists.

H.4A therefore introduces an intermediate low-level contract:

```text
PDF source image occurrence
        ↓
VisualRasterObservation
```

H.4B will later enrich it with structural evidence and only then construct the
complete `VisualEvidenceObservation`.

---

## 2. Generic source boundary

The Core contract is format-extensible:

```text
IVisualRasterObservationSource
```

with:

```text
CanObserve(DocumentFormatId)

ObserveAsync(
    DocumentSource,
    DocumentFormatId,
    DocumentExtractionResult,
    CancellationToken)
```

The first implementation is:

```text
PdfPigVisualRasterObservationSource
```

No PDF-specific type leaks through the interface.

---

## 3. Source alignment invariants

The PDF observer reopens the same document source and validates:

```text
requested format == extraction format == PDF
PDF page count == extraction page count
physical page identity is sequentially aligned
page.GetImages().Count == extractionPage.RasterImageCount
```

Each source image occurrence retains the same zero-based source index generated
by the deterministic `page.GetImages()` enumeration.

A count mismatch is an integration error. The observer does not guess which
visual disappeared or appeared.

---

## 4. Canonical coordinates

The observer reuses the production `PdfPageCoordinateSpace`.

Therefore source image placement and native word bounds share the same
canonical MediaBox-normalized, top-left coordinate system already used by PDF
native extraction.

No second PDF coordinate transform is introduced.

The declared image rectangle remains source geometry evidence. It is not treated
as the effective visible or semantic visual extent.

---

## 5. Decode strategy

The V1 decode sequence is the one validated in Phase 21E.1F:

```text
IPdfImage.RawBytes
    ↓
try direct RGBA decode
    ↓ if unsupported
IPdfImage.TryGetPng
    ↓
RGBA decode
    ↓ if unavailable
fail closed
```

`StbImageSharp 2.30.15` is promoted from the disposable diagnostic harness into
`DocumentProcessing.Pdf`.

The decoder is used only to obtain deterministic RGBA evidence.

A decode failure creates an unavailable raster observation. It does not imply
presentation-only content.

---

## 6. Operational pixel ceiling

Untrusted documents must not be allowed to request arbitrary image allocations.

The default V1 ceiling is:

```text
16,000,000 decoded pixels per source image
```

At RGBA output size alone this is approximately:

```text
64 MiB
```

The ceiling is an operational safety limit, **not semantic evidence**.

It is constructor-configurable.

An image exceeding the ceiling fails closed to unavailable evidence rather than
being classified as decorative or meaningful.

The observer checks PdfPig sample dimensions before decode when available and
checks decoded dimensions again after decode.

---

## 7. Frozen foreground measurement

The following Phase 21E.1F constants are promoted unchanged:

```text
BackgroundDistance                  = 18.0
BackgroundUniformityRequired        = 0.95
WordBoxExpansionPixels              = 2
SignificantComponentMinimumPixels   = 16
```

These are measurement parameters.

They are not the H.2 semantic classifier thresholds.

### Boundary background estimate

The raster boundary is sampled with:

```text
stride = max(1, min(width, height) / 128)
```

The inferred background RGB uses the same upper-median rule as the frozen
diagnostic harness.

A boundary sample is background-compatible when:

```text
alpha <= 16
OR
Euclidean RGB distance <= 18
```

If fewer than 95% of boundary samples are compatible, foreground extraction is
indeterminate and fails closed.

### Foreground mask

With a sufficiently uniform boundary, a pixel is foreground when:

```text
alpha > 16
AND
RGB distance from inferred background > 18
```

No foreground pixels:

```text
VisualForegroundState.BlankCanvas
```

Otherwise:

```text
VisualForegroundState.Measured
```

---

## 8. Native-word pixel interaction

Native words are mapped from canonical page coordinates into the declared image
pixel space.

Intersecting word boxes are expanded by exactly two pixels, matching the frozen
diagnostic algorithm.

The produced neutral interaction kinds are:

```text
NoNativeWords
BlankCanvas
NoForegroundWordIntersection
LowForegroundWordInteraction
ForegroundWordInteraction
```

H.4A does not interpret these as semantic importance.

---

## 9. Connected components and effective extent

Foreground connected components use 8-connectivity.

A component is significant when:

```text
pixel count >= 16
```

The effective visual pixel bounds are:

```text
union(all significant components)
```

or, if no component reaches the significance threshold:

```text
largest foreground component
```

Those bounds are mapped back into canonical page coordinates.

This explicitly preserves the Phase 21E distinction:

```text
declared image geometry
!=
effective visible foreground extent
!=
semantic visual
```

---

## 10. Cancellation and resource behavior

Cancellation is checked:

- before opening/processing;
- per PDF page;
- per source image;
- throughout large pixel/component loops.

The source restores the original position of seekable caller-owned streams.

Non-seekable input is buffered, following the existing PDF extractor pattern.

`OutOfMemoryException` is intentionally not converted into ordinary
decode-unavailable evidence.

---

## 11. Tests

The focused tests cover:

```text
blank white raster
exact 16-pixel significant component boundary
effective foreground bounds
native-word foreground interaction
no native-word intersection
non-uniform boundary -> unavailable
pre-cancelled analysis
generated PDF + embedded PNG end-to-end
caller stream position restoration
PDF/extraction image-count drift rejection
unsupported format rejection
pixel-budget validation
collection snapshot/duplicate-index invariants
absence of structural/policy fields from low-level contract
```

The generated-PDF test exercises:

```text
PdfDocumentBuilder
    -> embedded PNG
    -> PdfPigDocumentExtractor
    -> PdfPigVisualRasterObservationSource
    -> StbImageSharp decode
    -> raster measurement
```

without requiring external corpus files.

---

## 12. What H.4A still does not prove

H.4A does not yet prove the complete frozen 21E.1F classifications on real
corpora.

The missing production structural layer is:

```text
VisualRasterObservation
+
normalized text blocks / headings
        ↓
heading association
native-text containment
caption association
        ↓
complete VisualEvidenceObservation
```

That is H.4B.

H.4B must promote the frozen structural algorithms without retuning them, then
compare complete production observations against the frozen real-corpus
evidence before the shadow planner is activated in `DocumentProcessor`.

---

## 13. Next boundary

After H.4A is committed:

```text
21E.1H.4B
    deterministic structural visual enrichment
    + complete VisualEvidenceObservation factory
    + frozen real-corpus parity proof
```

Only after H.4B parity should production orchestration run the candidate planner
in true shadow mode.
