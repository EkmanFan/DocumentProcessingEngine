# Raster execution boundary V1

## Status

Phase 21C.1 production capability increment.

## Problem

The hybrid-processing capabilities already consume raster streams:

```text
PpStructureV3ServingClient.AnalyzeAsync(pageRaster, ...)
PaddleOcrServingClient.RecognizeAsync(regionRaster, ...)
VisualAssetPreserver.PreserveAsync(visualCrop, ...)
```

Until this increment, real-corpus harnesses created those raster bytes outside
the production engine with direct `pdftoppm` commands.

That meant the production orchestrator still lacked the concrete bridge:

```text
DocumentSource
    -> page PNG
    -> exact region PNG
```

## Decision

Introduce one narrow, format-capable raster boundary:

```text
IDocumentRasterizer
    -> IDocumentRasterizationSession
```

and one concrete PDF implementation:

```text
PdftoppmDocumentRasterizer
```

No raster framework, registry, graph or plugin system is introduced.

## Why a document-scoped session exists

The source corpus can be large. Rendering every region by reopening or copying
the complete source would be wasteful.

The concrete PDF rasterizer therefore:

1. materializes the input source once into an internal temporary file;
2. preserves the caller's original seekable stream position;
3. reuses the same immutable source for all page/region renders;
4. deletes internal temporary files when the session is disposed.

Temporary paths are runtime implementation details and never become document
provenance.

## V1 raster profile

The production profile intentionally matches the live hybrid evaluations:

```text
backend: pdftoppm
DPI: 300
color: RGB/default color output
encoding: PNG
page render: direct pdftoppm page render
region render: direct pdftoppm -x/-y/-W/-H crop
profile ID: pdftoppm-300dpi-rgb-png-direct-crop-v1
```

The profile ID describes deterministic configuration. Deployment should
separately control the installed Poppler version.

## Output ownership

Raster content is written to a caller-owned destination stream.

The returned `RasterRenderResult` contains only neutral execution evidence:

```text
physical page
source page pixel dimensions
optional exact PixelRectangle crop
output dimensions
media type
raster profile
content length
content SHA-256
```

It does not retain:

- file paths;
- process objects;
- temporary directories;
- output streams;
- Poppler stderr/stdout.

This keeps the default document boundary independent of runtime storage.

## Geometry invariant

A full-page raster must satisfy:

```text
output dimensions == source page dimensions
```

A region raster must satisfy:

```text
crop inside source page
output width  == crop.Width
output height == crop.Height
```

`RasterCropGeometry.FromNormalized(...)` remains the deterministic conversion
from normalized layout geometry to pixel crop coordinates.

## Security / operational constraints

`PdftoppmDocumentRasterizer`:

- invokes `pdftoppm` without a shell;
- uses `ProcessStartInfo.ArgumentList`, not command-string interpolation;
- bounds source bytes and output bytes;
- bounds captured diagnostics;
- enforces a finite render timeout;
- propagates cancellation;
- kills the child process tree on timeout/cancellation;
- rolls back seekable caller-owned destinations after failed rendering;
- never owns or disposes caller destination streams.

The host is responsible for installing and controlling the Poppler executable.

## Phase 21C.1 real-evidence gate

The increment is accepted only if the production implementation reproduces the
existing pinned Ehrman page-233 raster evidence at the pinned host profile:

```text
full page 233:
  2556 x 3305
  SHA-256 654dd8186552c2727808c48b2e4376815693e1d845f489a66dbca8305e61d484

direct Figure region:
  crop 620,1442 -> 1461,2840
  841 x 1398
  SHA-256 c4170e36da6d0bfdec419f8db199ba972baf3075887a264aa2e9e4d46e6e4e77
```

This promotes the already-proven harness behavior into a production capability
instead of inventing a different raster path.

## Non-goals

Phase 21C.1 does not:

- call PP-StructureV3;
- call PaddleOCR;
- reconcile text;
- persist visual assets;
- modify `DocumentProcessor`;
- start Docker/model processes;
- introduce document-level concurrency;
- introduce generic retries;
- modify RAG/persistence concerns.

The next increment can compose this raster session with existing layout/OCR/
visual/reconciliation capabilities at the page-execution boundary.
