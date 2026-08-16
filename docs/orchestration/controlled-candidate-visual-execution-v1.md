# H.4D.3B — Controlled candidate visual execution

## Status

```text
H.4D.2B    DONE
H.4D.3A    DONE
H.4D.3B    ACCEPTED
H.4D.4     NEXT
```

H.4D.3B executes the candidate visual axis for the first time while preserving
the existing legacy `DocumentIngestionResult` as the sole authority.

The candidate result remains shadow/evaluation evidence.

## Position in the orchestration

```text
H.4C shadow plan
      ↓
legacy authoritative execution
      ↓
DocumentIngestionResult BUILT
      ↓
H.4D.1 / H.4D.2B controlled text execution
      ↓
H.4D.3B controlled visual execution
      ↓
observer / comparison evidence only
      ↓
return already-built authoritative result
```

No candidate visual output is consumed by `DocumentProcessor` to construct or
modify the authoritative result.

## Independent visual actions

### NoAdditionalSemanticProcessing

```text
NoAdditionalSemanticProcessing
    -> no source visual materialization
    -> no rasterization
    -> no layout analysis
    -> no OCR
```

This means no additional semantic work. It does not mean that source fidelity
bytes are deleted.

### PreserveMeaningfulVisual

```text
PreserveMeaningfulVisual
    -> exact (physical page, sourceVisualIndex)
    -> H.4D.3A ISourceVisualAssetMaterializer
    -> validate/decode/hash exact standalone source asset
    -> Stream.Null shadow sink
    -> retain SourceVisualAssetMaterialization metadata in the report
    -> no rasterization merely for preservation
    -> no layout merely for preservation
    -> no OCR
```

`Stream.Null` is deliberate. Controlled execution proves that the exact asset
can be materialized without creating a second persistence path or publishing a
non-authoritative asset.

Authoritative storage/caller destination semantics remain a cutover concern.

### AnalyzeVisual

```text
AnalyzeVisual
    -> open controlled document-scoped raster session
    -> render full physical page once
    -> run neutral IPageLayoutAnalyzer once for that page
    -> retain RasterRenderResult + LayoutAnalysisResult as shadow evidence
    -> no source-asset persistence
    -> no OCR capability
```

Multiple `AnalyzeVisual` source occurrences on the same physical page share the
same page-level raster/layout observation pass.

H.4D.3B deliberately does not introduce a VLM or an LLM. The existing layout
analyzer is the current neutral visual-structure execution capability.

## Exact visual coverage

Completed H.4D.3B execution requires:

```text
shadow page count == extraction page count
physical page identity exact
candidate visual plan count == extraction RasterImageCount
candidate source visual indexes == exact set 0..N-1
source SHA == H.4C source SHA
format == extraction format == H.4C format
```

Incomplete or non-contiguous source-visual planning fails closed inside the
candidate report.

## Failure semantics

```text
ordinary candidate visual failure
    -> Failed report
    -> discard partial candidate page evidence
    -> observer receives failure evidence
    -> authoritative legacy result unchanged

observer ordinary failure
    -> best effort
    -> authoritative result unchanged

caller cancellation
    -> propagate

OutOfMemoryException
    -> propagate
```

The `DocumentProcessor` resets the prepared source before and after controlled
visual execution so candidate source access cannot leak stream-position state
into caller-visible custody.

## Text / visual isolation

The H.4D.3B dependency set is intentionally:

```text
IDocumentControlledCandidateVisualExecutionObserver
ISourceVisualAssetMaterializer
IDocumentRasterizer
IPageLayoutAnalyzer
```

It contains no text recognizer and no OCR contract.

Therefore:

```text
visual analysis != OCR authorization
figure/raster presence != OCR authorization
meaningful visual preservation != OCR authorization
```

H.4D.2B continues to own controlled text execution.

## Deterministic acceptance gates for this increment

The implementation tests must prove:

```text
mixed no-op / preserve / analyze branching
preserve-only performs no raster/layout work
multiple AnalyzeVisual elements share one page raster/layout pass
no-op performs no visual I/O
ordinary preservation failure is fail-open
ordinary analysis failure is fail-open
OutOfMemoryException propagates
caller cancellation propagates
observer ordinary failure is best-effort
DocumentProcessor returns the already-built authoritative result after
  ordinary controlled visual failure
```

## Deferred real-corpus validation

The implementation increment intentionally stops before live external ML.

After deterministic acceptance, a separate H.4D.3B real-corpus validation must
exercise:

```text
Habermas p40 / p43 / p44
    PreserveMeaningfulVisual
    exact H.4D.3A source materialization
    no layout/OCR merely for preservation

Ehrman p148
    AnalyzeVisual
    live PP-StructureV3 layout execution
    OCR service/calls remain zero

Ehrman p233
    figure/raster safety control
    Figure never enters OCR
    preservation semantics remain intact
```

## Acceptance evidence

H.4D.3B is accepted as an explicit **controlled shadow/evaluation capability**.
It is not an authoritative production cutover and carries no performance
acceptance claim.

Deterministic evidence:

```text
Release -warnaserror                        PASS
focused H.4D.3B tests                       10 / 10
complete regression                         534 / 534
ordinary candidate visual failure           fail-open
caller cancellation                         propagates
OutOfMemoryException                        propagates
ordinary observer failure                   best-effort
authority transfer                          no
```

Live real-corpus evidence:

| Corpus | Physical page | H.4C candidate visual action | Legacy route | H.4D.3B result |
|---|---:|---|---|---|
| Habermas | 40 | `PreserveMeaningfulVisual` | `NativeOnly` | exact source JPEG preserved |
| Habermas | 43 | `PreserveMeaningfulVisual` | `NativeOnly` | exact source JPEG preserved |
| Habermas | 44 | `PreserveMeaningfulVisual` | `NativeOnly` | exact source JPEG preserved |
| Ehrman | 148 | `AnalyzeVisual` | `LayoutWithTargetedOcrReconciliation` | live PP-StructureV3, 11 observations |
| Ehrman | 233 | `AnalyzeVisual` | `LayoutWithTargetedOcrRecovery` | live PP-StructureV3, 10 observations including 1 Figure |

The three Habermas preservation controls ran before the PP-StructureV3 service
was started. Their exact embedded JPEG bytes and historical SHA-256 values were
retained, and preservation performed no rasterization or layout analysis.

The Ehrman controls executed one full-page 300-DPI raster and one live
PP-StructureV3 layout-analysis call per page. Ehrman page 233 produced one live
`Figure`, and deterministic layout treatment remained:

```text
Figure
    -> PreserveVisualWithoutOcr
```

The DPEngine targeted OCR boundary was not constructed and received zero calls
throughout H.4D.3B live validation. PP-StructureV3 may internally use OCR as
part of its own layout pipeline; that is distinct from DPEngine targeted OCR
authorization.

The validated live service versions were:

```text
PaddlePaddle    3.2.2
PaddleOCR       3.7.0
PaddleX         3.7.2
container limit 12 GiB
```

H.4D.3B therefore establishes that the candidate visual axis can execute
independently while the legacy result remains the sole authority.

No optimization or cutover inference is made from this acceptance. Full
candidate comparison, provenance/output parity, remaining failure evidence,
and any guarded authority transition belong to H.4D.4.

## Next increment

```text
H.4D.4
    full candidate execution comparison
    output/provenance comparison
    guarded cutover evidence
    legacy authority retained until explicit acceptance
```
