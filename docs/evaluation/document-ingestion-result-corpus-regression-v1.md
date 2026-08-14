# Phase 20D — real-corpus serialized DocumentIngestionResult proof

**PASS**

## Purpose

This evidence closes Phase 20 by proving that the canonical strongly typed `DocumentIngestionResult` can be built from real corpus evidence, serialized through the official V1 JSON boundary, deserialized with full invariant reconstruction, and consumed again without source PDFs, ML services, rasters or crops.

It is a transport/custody proof, not the future Phase 21 end-to-end ingestion orchestrator.

## Exact baseline

`3a29a38094fa54ec20f818317194d2a732810d2d`

## Derived fixture lineage

The public result deliberately requires a complete physical-page set. Phase 20D therefore derives complete temporary PDFs instead of weakening that invariant.

### Ehrman

- original SHA-256: `f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe`
- derived fixture SHA-256 / result custody root: `4fbe78eb1d4a6b723ab4af9397fdee2d35c8a2005635f2f4fd059b6313ecc373`
- page map: fixture `1 <- 233`, `2 <- 380`, `3 <- 405`
- fixture page 1 executes the full pinned page-233 layout/OCR/visual/deferred path
- fixture page 2 carries only the pinned page-380 suspicious reconciliation region
- fixture page 3 carries only the pinned page-405 healthy reconciliation region
- no claim is made that the 3-page fixture is a complete semantic ingestion of the 617-page book

### De Decretis

- original SHA-256: `de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75`
- derived fixture SHA-256 / result custody root: `4dcd46d067ba5d3b5fbc8d4c09d9bbc9d2558fd0dea0d8441ca6b1c272c616d2`
- original pages `512-561` become a complete derived `1-50` document

## Ehrman portable result

```text
pages                         3
elements                      12
segments                      1
OCR confidence summaries      9
page 1 OCR/visual/deferred    7/1/2
page 2 reconciliation         Conflict / None
page 3 reconciliation         Agreement / NativePdf
serialized JSON bytes         25103
serialized JSON SHA-256       422e9b9d941924471434a19dcaea27ad629cd2a0e6615ba200975baab5f9ceca
```

The JSON SHA above is a transport-regression artifact hash only. It is not documentary custody identity.

## De Decretis portable result

```text
pages                         50
native words                  29044
native blocks/elements        269/269
segments                      50
OCR confidence summaries      0
serialized JSON bytes         758096
serialized JSON SHA-256       88d3b7ea0269eef276314dfaf9c93234ceb14581d18c9c03bc8aa5fd0178a406
```

The born-digital control retains no rasterization, layout, OCR or reconciliation manifest identity.

## Offline deserialization proof

After PP-StructureV3 and PaddleOCR were stopped, a fresh process read only the two raw portable JSON result files and sanitized summaries.

The V2 resume re-ran this offline verification from a fresh exact-baseline build without starting either ML service.

It reconstructs both results through the official `DocumentIngestionResultJson.Deserialize(...)` boundary and proves:

- source custody roots survive;
- page/element/segment graphs survive;
- page-380 Conflict / unresolved divergence survives;
- page-405 Agreement / NativePdf survives;
- page-233 preserved-visual metadata survives;
- OCR confidence summaries survive;
- De Decretis remains native-only;
- reserialization is byte-stable;
- no source PDF, layout service, OCR service, raster or crop is needed after deserialization.

## Operational memory isolation

- the original live Phase 20D harness was compiled before any ML model was loaded;
- PP-StructureV3 and PaddleOCR were never resident concurrently;
- each model container was capped at 12 GiB;
- at least 12 GiB `MemAvailable` was required before model startup;
- De Decretis and the final offline verifier ran with no ML service loaded;
- this V2 resume reused the retained successful live artifacts and did not rerun either ML model.

This is an evaluation safety boundary, not a production throughput claim.

## Commit boundary

Only this sanitized Markdown report and its sanitized JSON companion are committed.

The derived PDFs, raw `DocumentIngestionResult` JSON (which contains document text), OCR text, layout snapshots, rasters and crops remain under `scripts/tmp/` and are not committed.

## Phase 20 closeout

```text
20.0  result contract + serialization policy      DONE
20A   DocumentIngestionResult model                DONE
20B   deterministic result projection              DONE
20C   JSON contract + round-trip                    DONE
20D   real-corpus serialized result proof           PASS
```

Next: Phase 21 — deterministic end-to-end ingestion orchestrator.
