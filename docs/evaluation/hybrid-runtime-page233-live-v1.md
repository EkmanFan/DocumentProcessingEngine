# Phase 18B — live hybrid runtime integration, Ehrman page 233

## Status

**PASS** on two independent exact-baseline worktrees.

This integration proof executes the current production boundaries through
`HybridDocumentAssemblyResult` on the pinned Ehrman physical PDF page 233.

It is deliberately narrower than the final Phase 18 corpus regression.

## Validated baseline

```text
d0316f5de669bb261b664e0792ddb7389dfe82e7
```

## Real runtime path

```text
pinned PDF
  -> PdfPigDocumentExtractor
  -> exact 300-DPI page raster
  -> real PP-StructureV3 HTTP service
  -> PpStructureV3ServingClient
  -> LayoutAnalysisResult
  -> TargetedOcrPlanner / VisualPreservationPlanner
  -> exact raster crops
  -> real PaddleOCR HTTP service
  -> PaddleOcrServingClient
  -> NativeOcrTextReconciler (Missing -> OcrOnly)
  -> VisualAssetPreserver
  -> HybridDocumentElementFactory
  -> HybridDocumentAssembler
  -> HybridDocumentAssemblyResult
```

No generic ingestion orchestrator was introduced by this validation.

## Source and raster

- Physical PDF page: **233**
- Source SHA-256: `f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe`
- Raster: **2556 x 3305**
- Raster SHA-256: `654dd8186552c2727808c48b2e4376815693e1d845f489a66dbca8305e61d484`

## Assembly result

- Native words: **0**
- Native blocks: **0**
- Layout observations: **10**
- Real OCR HTTP requests: **7**
- Authoritative textual elements: **7**
- Preserved visual elements: **1**
- Deferred elements: **2**
- Unresolved text elements: **0**

The assembled page shape is:

```text
seq 0: Deferred / None / -
seq 1: Deferred / None / -
seq 2: Heading / Ocr / OcrOnly
seq 3: Text / Ocr / OcrOnly
seq 4: Visual / None / -
seq 5: Caption / Ocr / OcrOnly
seq 6: Text / Ocr / OcrOnly
seq 7: Text / Ocr / OcrOnly
seq 8: Text / Ocr / OcrOnly
seq 9: Text / Ocr / OcrOnly
```

Thus the page contains two Deferred regions, seven authoritative OCR-backed
textual regions, and one textless preserved Figure.

## Critical visual invariant

- Figure layout sequence: **4**
- Preserved bytes: **1534766**
- Preserved SHA-256: `aaf62775d525104c737d2d238df1840894b42572e52de3874705fa10c926009d`

The Figure has no narrative text, no OCR reconciliation, and no text origin.
The committed evidence contains no raw crop bytes.

## Narrative order

Considering narrative `Text` elements only (excluding Heading, Caption,
Visual and Deferred evidence), the live reading order is:

```text
3 -> 6 -> 7 -> 8 -> 9
```

This preserves the expected left-column-to-right-column narrative sequence.

## Acceptance gates

- `nativePage233RemainsMissing`: **true**
- `layoutStreamHasTenElementsInExpectedKinds`: **true**
- `exactlySevenAuthoritativeTextElements`: **true**
- `allAuthoritativeTextOriginatesFromOcr`: **true**
- `allTextualReconciliationsAreOcrOnly`: **true**
- `exactlySevenRealOcrRequestsIssued`: **true**
- `figureSequence4NeverHasOcrOrNarrativeText`: **true**
- `figureBytesMatchValidatedPhase16B`: **true**
- `twoDeferredRegionsRemainVisible`: **true**
- `documentSignalsDeferredEvidence`: **true**
- `noUnresolvedTextConflictOnMissingNativePage`: **true**
- `headingSentinelsRecovered`: **true**
- `leftNarrativeImagineRecovered`: **true**
- `captionSentinelsRecovered`: **true**
- `rightNarrativeForExampleRecovered`: **true**
- `crossColumnNarrativeOrderUsable`: **true**
- `assemblyProfileIsExpected`: **true**
- `assemblyContainsOnlyPhysicalPage233`: **true**
- `noNativeBlockWasInventedForOcrOnlyText`: **true**
- `layoutProvenanceIsUnique`: **true**

All gates passed in both exact-baseline runs.

## Scope conclusion

Phase 18B proves the real page-233 runtime path through hybrid assembly.
It does **not** yet prove:

- raster-only pages 14–20;
- hybrid pages 1–10;
- unified hybrid normalization;
- structural segmentation over the unified stream;
- cross-page Native/OCR transition behavior;
- De Decretis born-digital regression.

Those remain later Phase 18 increments.

## Next

Phase 18C — unified hybrid normalization.

Normalization must operate after native/OCR evidence has been unified and must
not discard visual, deferred, source-origin, or reconciliation provenance.
