# Phase 19C — real-corpus provenance and quality proof

**PASS**

## Operational memory isolation

The evaluation deliberately executes native probing, PP-StructureV3, PaddleOCR/Ehrman processing, and De Decretis processing in separate processes/stages.

PP-StructureV3 and PaddleOCR are never resident concurrently. Each model container is capped at 12 GiB. This is an evaluation safety boundary discovered after the earlier combined run triggered a host OOM.

This is not a production throughput claim.

## Ehrman scenarios

### ehrman-front-matter-pages-1-10

Status: **PASS**

```text
pages                    10
native pages             2
OCR/layout pages         8
layout requests           8
targeted OCR requests     42
visual elements           3
deferred elements         8
unresolved elements       0
segments                  10
cross-page segments       6
mixed cross-page          1
table-provenance text     0
provenance elements       63
quality native/OCR text   10/42
quality OCR evidence      42
quality divergence        0
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `tenPhysicalPagesPreserved`: **true**
- `containsNativeAndOcrPages`: **true**
- `copyrightPage5UsesNativeEvidence`: **true**
- `contentsPages6To10RecoverText`: **true**
- `noUnresolvedText`: **true**
- `figuresNeverBecomeOcrTargets`: **true**
- `segmentationProduced`: **true**

### ehrman-raster-toc-pages-14-20

Status: **PASS**

```text
pages                    7
native pages             0
OCR/layout pages         7
layout requests           7
targeted OCR requests     13
visual elements           0
deferred elements         6
unresolved elements       0
segments                  8
cross-page segments       0
mixed cross-page          0
table-provenance text     6
provenance elements       19
quality native/OCR text   0/13
quality OCR evidence      13
quality divergence        0
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `sevenPhysicalPagesPreserved`: **true**
- `allPagesUseOcr`: **true**
- `everyTocPageRecoversText`: **true**
- `tableFallbackRecoversPreviouslyEmptyPages14_16_18`: **true**
- `allAuthoritativeOriginsAreOcr`: **true**
- `noUnresolvedText`: **true**
- `figuresNeverBecomeOcrTargets`: **true**
- `ocrToOcrPageTransitionObserved`: **true**
- `segmentationProduced`: **true**

### ehrman-mixed-narrative-pages-33-40

Status: **PASS**

```text
pages                    8
native pages             6
OCR/layout pages         2
layout requests           2
targeted OCR requests     15
visual elements           1
deferred elements         4
unresolved elements       0
segments                  9
cross-page segments       4
mixed cross-page          2
table-provenance text     1
provenance elements       66
quality native/OCR text   46/15
quality OCR evidence      15
quality divergence        0
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `eightPhysicalPagesPreserved`: **true**
- `observedNativeOcrPatternMatchesCorpus`: **true**
- `everyNarrativeRangePageHasTextFlow`: **true**
- `noUnresolvedText`: **true**
- `nativeToNativeObserved`: **true**
- `nativeToOcrObserved`: **true**
- `ocrToNativeObserved`: **true**
- `realMixedOriginCrossPageSegmentObserved`: **true**
- `figuresNeverBecomeOcrTargets`: **true**

### ehrman-mixed-content-page-233

Status: **PASS**

```text
pages                    1
native pages             0
OCR/layout pages         1
layout requests           1
targeted OCR requests     7
visual elements           1
deferred elements         2
unresolved elements       0
segments                  1
cross-page segments       0
mixed cross-page          0
table-provenance text     0
provenance elements       10
quality native/OCR text   0/7
quality OCR evidence      7
quality divergence        0
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `physicalPageIs233`: **true**
- `nativeTextRemainsMissing`: **true**
- `tenLayoutObservations`: **true**
- `sevenRealOcrRequests`: **true**
- `sevenAuthoritativeTextElements`: **true**
- `onePreservedVisual`: **true**
- `twoDeferredElements`: **true**
- `noUnresolvedText`: **true**
- `allAuthoritativeTextOriginsAreOcr`: **true**
- `figureNeverBecomesOcrTarget`: **true**
- `normalizationRetainsTextFlow`: **true**
- `segmentationProduced`: **true**

## Real reconciliation controls

### ehrman-suspicious-reconciliation-page-380

Status: **PASS**

```text
page                     380
native status            Suspicious
decision                 Conflict
selected origin          None
resolved/divergent       False/True
comparable words         299
native/OCR joins         5/5
quality OCR evidence     1
quality unresolved       1
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `physicalPageMatchesPinnedControl`: **true**
- `ocrEvidenceIsRetained`: **true**
- `comparableExtentIsRetained`: **true**
- `custodyQualityProjectionPass`: **true**
- `expectedResolutionState`: **true**
- `page380HistoricalConflictParity`: **true**

### ehrman-healthy-reconciliation-page-405

Status: **PASS**

```text
page                     405
native status            Healthy
decision                 Agreement
selected origin          NativePdf
resolved/divergent       True/False
comparable words         132
native/OCR joins         4/4
quality OCR evidence     1
quality unresolved       0
```

- `custody.sourceShaIsCustodyRoot`: **true**
- `custody.allNormalizedElementsHaveProvenance`: **true**
- `custody.segmentCountsMatch`: **true**
- `custody.selectedSourceTextHashesVerify`: **true**
- `custody.normalizedTextHashesVerify`: **true**
- `custody.segmentTextHashesVerify`: **true**
- `custody.segmentMembershipRoundTrips`: **true**
- `custody.qualityElementCountsBalance`: **true**
- `custody.ocrConfidenceAccountingBalances`: **true**
- `custody.processingManifestMatchesObservedBoundaries`: **true**
- `physicalPageMatchesPinnedControl`: **true**
- `ocrEvidenceIsRetained`: **true**
- `comparableExtentIsRetained`: **true**
- `custodyQualityProjectionPass`: **true**
- `expectedResolutionState`: **true**
- `page405HistoricalAgreementParity`: **true**

## De Decretis

Status: **PASS**

Hybrid segments: `50`.

Provenance elements: `269`; OCR evidence: `0`.

## Transition coverage

- `nativeToNative`: **true**
- `ocrToOcr`: **true**
- `nativeToOcr`: **true**
- `ocrToNative`: **true**

## Corpus interpretation

- Pages 1–10: mixed front matter.
- Pages 14–20: raster-only table of contents/front matter, not narrative-body prose.
- Pages 33–40: mixed chapter/narrative range used for real Native/OCR cross-page transitions.
- Page 233: mixed-content Figure exclusion + visual custody control.
- Pages 380 and 405: retained real conflict/agreement reconciliation controls.
- De Decretis pages 512–561: born-digital native control.

## Claim boundary

This evidence validates custody-complete provenance and deterministic quality observations on real corpus paths. It does not introduce or claim the future generic ingestion orchestrator or DocumentIngestionResult.

Raw OCR text, page rasters, crops and intermediate layout snapshots remain local under scripts/tmp and are not committed.

## Next

Phase 20: DocumentIngestionResult.
