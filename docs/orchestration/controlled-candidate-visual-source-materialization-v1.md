# H.4D.3A — Neutral source-visual asset materialization

## Status

```text
H.4D.2B    DONE
H.4D.3A    ACCEPTED
H.4D.3B    NEXT
H.4D.4     DEFERRED
```

H.4D.3A introduces only the missing source-visual materialization boundary
needed before independent controlled candidate visual execution can be
implemented safely.

It does **not** execute candidate visual actions and does not change
`DocumentProcessor`.

## Why this prerequisite exists

Candidate visual actions are keyed by source visual occurrence:

```text
physical page
+
source visual index
```

The current legacy preservation runtime is layout-region based. It preserves an
already-materialized page crop selected from a `LayoutObservation`.

That is appropriate for the legacy hybrid path, but it is the wrong primitive
for this H.3B rule:

```text
PreserveMeaningfulVisual
    -> preserve already-identified source visual
    -> no rasterization merely for preservation
    -> no layout merely for preservation
    -> no OCR
```

H.4A already establishes deterministic source visual ordering and geometry, but
its observation result intentionally carries evidence rather than retained
binary source-visual content.

Therefore H.4D.3 needs a narrow source-visual materialization capability before
it needs a visual runner.

## New neutral boundary

Core receives:

```text
ISourceVisualAssetMaterializer
SourceVisualAssetMaterialization
```

The interface is format-extensible and materializes one exact source visual
occurrence into a caller-owned destination stream.

The result carries neutral integrity/provenance metadata:

```text
physical page number
source visual index
declared normalized page bounds
materialization profile ID
image media type
content length
content SHA-256
```

It does not contain semantic visual classification, execution policy, layout
observations, OCR evidence, or persistence technology.

## PDF V1 implementation

`PdfPigSourceVisualAssetMaterializer` uses the same source occurrence ordering
as H.4A:

```text
page.GetImages().ToArray()
index 0..N-1
```

It requires exact agreement with the already-produced extraction page:

```text
PDF page count == extraction page count
physical page identity exact
GetImages count == RasterImageCount
source visual index in range
```

Geometry reuses `PdfPageCoordinateSpace`, preserving the current canonical
MediaBox-normalized coordinate semantics.

The V1 materialization strategy is evidence-based and raw-JPEG-first:

```text
source visual occurrence
    ↓
RawBytes

validated standalone JPEG?
  signature + decode + exact sample dimensions
    YES -> preserve exact embedded JPEG bytes
    NO  -> PdfPig TryGetPng fallback
           + PNG signature
           + decode
           + exact sample dimensions
```

Profiles are explicit provenance:

```text
pdfpig-0.1.15-source-visual-raw-jpeg-v1
  -> image/jpeg
  -> exact validated embedded JPEG bytes

pdfpig-0.1.15-source-visual-png-fallback-v1
  -> image/png
  -> PdfPig-converted standalone PNG
```

Opaque PDF bitmap/filter streams are never emitted while pretending to be a
standalone image file.

## Safety and custody

The materializer:

```text
restores seekable source position
buffers non-seekable source input
requires distinct source/destination streams
requires an empty seekable destination
enforces source-pixel ceiling
enforces output-byte ceiling
validates embedded JPEG signature + standalone decode + exact dimensions
validates PNG fallback signature + standalone decode + exact dimensions
computes SHA-256 over exactly the written bytes
truncates seekable destination on failure
propagates cancellation
never swallows OutOfMemoryException
```

Opaque PDF image stream bytes are never emitted while pretending to be a
standalone image file.

## Real-corpus correction before acceptance

The first H.4D.3A real-corpus validation failed on Habermas physical page 40:
`TryGetPng()` could not convert source visual `i0`.

That failure invalidated the initial PNG-only assumption; it did not invalidate
the source-occurrence materialization boundary itself.

Earlier H.4A evidence had already established that Habermas p40/p43/p44 are
directly decodable `RawEmbeddedImage` occurrences whose decoded hashes equal
their raw embedded hashes. For JPEG-backed PDF images, preserving the validated
raw standalone JPEG is both narrower and more faithful than forcing a PNG
conversion.

The repaired order therefore matches the evidence:

```text
validated embedded JPEG
    first

PdfPig PNG conversion
    fallback
```

No new production dependency is introduced; `DocumentProcessing.Pdf` already
uses StbImageSharp for raster evidence.

## Explicit non-goals

H.4D.3A does not:

```text
modify DocumentProcessor
modify the H.4D.2B text runner
execute PreserveMeaningfulVisual
execute AnalyzeVisual
invoke PP-StructureV3
invoke PaddleOCR
invoke any vision/LLM model
transfer candidate authority
persist visual bytes itself
introduce a plugin registry
```

The engine remains .NET-first and format boundaries remain explicit.

## Acceptance evidence

H.4D.3A is accepted on the exact H.4D.2B baseline after deterministic and
real-corpus validation.

Deterministic regression:

```text
focused source-visual materialization tests    11 / 11
complete regression                            524 / 524
Release -warnaserror                           PASS
```

Real-corpus controls:

| Corpus | Physical page | Materialization | Dimensions |
|---|---:|---|---:|
| Habermas | 40 | exact embedded JPEG | 1506x1575 |
| Habermas | 43 | exact embedded JPEG | 1466x981 |
| Habermas | 44 | exact embedded JPEG | 1428x1394 |
| Ehrman | 148 | PdfPig PNG fallback | 506x651 |
| Ehrman | 233 | PdfPig PNG fallback | 998x1294 |

Every control was materialized twice with exact byte and metadata
repeatability. Caller-owned seekable source position was restored.

The validation also confirms the architectural boundary:

```text
layout invoked               no
OCR invoked                  no
candidate visual execution   no
authority transfer           no
```

The first real-corpus attempt usefully rejected the original PNG-only
assumption on Habermas p40. The accepted implementation therefore preserves a
validated standalone embedded JPEG byte-for-byte when available and otherwise
uses the validated PdfPig PNG fallback. Opaque PDF image streams are never
misrepresented as standalone assets.

This completes the materialization prerequisite only. It does not transfer
visual authority and does not execute H.3B visual actions.

## Next increment

Only after H.4D.3A passes deterministic regression:

```text
H.4D.3B
  controlled candidate visual execution

  NoAdditionalSemanticProcessing
      -> no visual work

  PreserveMeaningfulVisual
      -> source-visual materializer
      -> no layout/OCR merely for preservation

  AnalyzeVisual
      -> raster + layout observation
      -> no OCR
      -> comparison evidence only
```

Real-corpus validation for H.4D.3B must include:

```text
Habermas p40 / p43 / p44
  meaningful visual preservation
  including legacy-native pages

Ehrman p148
  independent AnalyzeVisual
  OCR remains text-axis only

Ehrman p233
  Figure never enters OCR
  preservation semantics remain intact
```
