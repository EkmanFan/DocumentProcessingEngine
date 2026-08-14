# Figure preservation production boundary V1

## Status

Phase B / 16A production-boundary increment.

This increment establishes the neutral visual-evidence model, deterministic
visual-preservation planning, shared raster crop geometry, integrity hashing,
and a caller-owned binary destination boundary.

It intentionally stops before claiming real figure preservation on the pinned
corpus.

```text
16 Figure preservation
   16A neutral model + preservation boundary   THIS INCREMENT
   16B real figure preservation integration    NEXT
```

## Architectural rule

A visual asset is preserved only after neutral layout evidence has been
translated by deterministic application policy.

```text
LayoutObservation
        ↓
LayoutTreatmentPolicy
        ↓
PreserveVisualWithoutOcr
        ↓
VisualPreservationPlanner
        ↓
PixelRectangle
        ↓
materialized crop stream
        ↓
VisualAssetPreserver
        ↓
caller-owned destination stream
        +
PreservedVisualEvidence
```

For the current V1 policy this means:

```text
Figure -> PreserveVisualWithoutOcr -> preserve visual bytes
```

Text, Heading and Caption remain OCR candidates. Table and Unknown remain
Deferred.

`VisualAssetPreserver` independently re-checks the deterministic policy and
refuses a region that is not `PreserveVisualWithoutOcr`.

## Shared raster geometry

Phase 15 introduced raster crop geometry inside the OCR namespace because OCR
was its only consumer.

Figure preservation is now a real second consumer. Keeping raster geometry
under `Engine.Ocr` would incorrectly make visual preservation depend on OCR.

The crop model is therefore promoted to a shared raster boundary:

```text
Core.Raster.PixelRectangle
Engine.Raster.RasterCropGeometry
```

This is a responsibility move, not a policy change.

`RasterCropGeometry.FromNormalized(...)` keeps the established behavior:

- source `NormalizedRectangle` evidence remains unclamped;
- left/top use floor;
- right/bottom use ceiling;
- clamping occurs only at the physical raster boundary;
- an empty intersection fails closed.

Targeted OCR now consumes the same shared raster geometry.

## Neutral preserved-visual evidence

`PreservedVisualEvidence` contains no binary payload.

It records:

- source document SHA-256;
- versioned visual/raster profile ID;
- media type;
- exact source `LayoutObservation`;
- source raster pixel dimensions;
- exact `PixelRectangle` crop;
- preserved byte length;
- preserved content SHA-256.

The binary bytes are written separately to a caller-owned destination stream.

This makes integrity and provenance neutral while avoiding a large `byte[]` in
Core and avoiding a storage-backend decision in the engine.

## Binary destination boundary

`VisualAssetPreserver` accepts:

```text
visual crop Stream
        ↓
caller-owned destination Stream
```

During the copy it computes SHA-256 and returns the corresponding
`PreservedVisualEvidence`.

The engine therefore does not choose:

- filesystem paths;
- PostgreSQL byte storage;
- MinIO/S3;
- object-store keys;
- ApologiaStudio persistence;
- a generic storage-provider abstraction.

A future orchestrator can supply the appropriate destination.

For seekable destinations, V1 requires an empty stream positioned at zero. This
prevents accidental append/overwrite behavior and allows the preserver to clear
partial output if processing fails.

## Operational safeguards

The V1 preserver:

- requires readable source and writable destination streams;
- rejects use of the same stream as both source and destination;
- re-checks deterministic visual-preservation authorization;
- verifies the supplied crop against deterministic raster geometry;
- rejects empty content;
- enforces a bounded input size;
- supports caller cancellation;
- restores the position of seekable source streams;
- clears a seekable destination on failure;
- validates SHA-256 provenance fields.

## Source document identity

Phase 16A uses source-document SHA-256 as a deterministic, consumer-neutral
identity for the preserved asset.

This does not replace the broader provenance model planned for Phase 19.
Document-level provenance may later wrap or enrich this identity without
changing the preserved-content hash.

## Deliberate non-decisions

This increment does **not** add:

- image interpretation by an LLM;
- visual semantic taxonomies;
- caption-to-figure semantic inference;
- figure storage in PostgreSQL, MinIO or another backend;
- a generic storage-provider/plugin system;
- native/OCR reconciliation;
- cross-page hybrid continuity;
- ApologiaStudio semantics;
- end-to-end ingestion orchestration.

## Next step — Phase 16B

Validate the production boundary on the pinned Ehrman physical page 233:

```text
real PP-StructureV3
        ↓
Figure observation sequence 4
        ↓
VisualPreservationPlanner
        ↓
exact papyrus PixelRectangle
        ↓
real raster crop
        ↓
VisualAssetPreserver
        ↓
preserved PNG bytes
        +
PreservedVisualEvidence
```

Acceptance should prove:

- exactly one visual-preservation target for the papyrus Figure;
- no Text/Heading/Caption region enters the preservation plan;
- deterministic crop coordinates and dimensions;
- preserved PNG dimensions match the planned crop;
- preserved byte length is non-zero;
- preserved SHA-256 is reproducible;
- source document SHA-256, page, source layout sequence and bbox remain
  traceable;
- the preserved Figure still produces no OCR evidence.

Only after that live proof should Phase 16 be marked DONE.
