# EPUB publication export prototype V1

## Status

**Canonical Engine output generated — technically valid, editorial quality not
yet accepted**

This increment tests whether the portable Engine result is already sufficient
to produce a useful reflowable EPUB. It is deliberately separate from EPUB
source processing.

## Responsibility boundary

`DocumentProcessing.Epub` owns the physical EPUB package. The exporter consumes
the canonical `DocumentProcessingResult`; it does not assess a PDF, invoke OCR,
classify content or modify Engine policy.

The public inputs are:

- the completed portable result;
- title, language, optional creator, identifier and modification time;
- an optional `EpubVisualAssetReader` for the caller-owned bytes described by
  `DocumentVisualAsset`.

When visuals exist, the reader is mandatory. The exporter verifies exact byte
length and SHA-256 before adding each image. A mismatch rejects the export.

## Current output

The generated EPUB contains:

- the required uncompressed first `mimetype` entry;
- `META-INF/container.xml`;
- an EPUB 3 package document and navigation document;
- one reflowable XHTML document per contiguous structural-segment run;
- headings, paragraphs, captions and preserved visuals in portable element
  order;
- a small shared stylesheet;
- PNG, JPEG, GIF, SVG and WebP visual resources.

`UnresolvedText`, `Deferred` and visual elements without preserved bytes are not
invented or rendered. Their count is returned in
`EpubPublicationExportResult.OmittedElementCount`.

This prototype does not reconstruct source page geometry. The portable result
also does not currently retain rich inline typography, hyperlinks, tables,
footnote relationships or meaningful alternative text, so the exporter cannot
recreate those features faithfully.

## Deterministic verification

Focused tests prove:

- correct EPUB ZIP invariants for `mimetype`;
- metadata, navigation, XHTML and visual-manifest generation;
- XML escaping;
- exact visual-byte verification;
- rejection when the visual reader is absent or returns different bytes;
- byte-identical output when metadata time and inputs are fixed.

The generated publication was additionally checked with the pinned EPUBCheck
5.3.0 distribution under EPUB 3.3 rules: zero fatal errors, errors, warnings or
informational messages.

## First Ehrman experiment

The local copyrighted source and generated file remain under the ignored
`tests/document_corpus/` tree. The first complete artifact is:

```text
tests/document_corpus/epub/EpubConverter/
  Ehrman-native-only-prototype-v1.epub
```

The normal public Host route was attempted first. It exposed two pre-export
blockers:

- source-backed visual matching rejected sampled pages before portable
  projection;
- the standalone mixed-content page 233 reached portable projection but failed
  its contiguous reading-order invariant.

The first inspectable book therefore uses the existing deterministic native PDF
extraction, normalization and structural segmentation to construct an explicitly
labelled native-only portable result. It is an experiment, not a second
production processing route.

Observed full-book measurements:

| Measurement | Result |
|---|---:|
| Source pages | 617 |
| Exported textual elements | 2,648 |
| EPUB content documents | 267 |
| Preserved visuals | 0 |
| EPUB size | 669 KiB |
| Elapsed time | 19.06 s |
| Process peak working set | 5,189.2 MiB |
| Calibre PDF preview pages | 872 |

The result opens and renders as a readable reflowable book. Visual review also
shows why it is not yet an accepted conversion:

- some source words retain PDF extraction spacing such as `de voted`,
  `narra tive` and `op portunity`;
- some non-heading fragments are promoted into the table of contents;
- some paragraph and column transitions are not editorially natural;
- no images are present in this native-only experiment.

## Canonical Ehrman experiment

The two blockers found by the first experiment were corrected without changing
processing policy:

- source observations now resolve successfully to an empty set when the plan
  contains no source visual to preserve;
- portable projection accepts unique, non-contiguous page-local reading-order
  values because the authoritative page membership already stores the exact
  order and numeric gaps carry no additional ordering information.

Both corrections were first validated on the public Host route with pages 1–5
and the standalone page 233. The complete source was then processed through the
same public Host route, including PP-StructureV3, targeted PaddleOCR, visual
preservation and portable-result projection. The exporter consumed only the
returned `DocumentProcessingResult` and the visual bytes written through
`UserVisualAssetWriter`.

The resulting local, ignored artifact is:

```text
tests/document_corpus/epub/EpubConverter/
  Ehrman-engine-result-prototype-v2.epub
```

Observed full-book measurements:

| Measurement | Result |
|---|---:|
| Source pages | 617 |
| PP-StructureV3 calls | 617 |
| Targeted OCR calls | 5,617 |
| Portable elements | 7,319 |
| Structural segments | 650 |
| Preserved visuals | 295 |
| Omitted portable elements | 2,882 |
| EPUB content documents | 776 |
| EPUB size | 770,246,770 bytes (734.6 MiB) |
| Total processing and export time | 02:14:17.397 |
| Engine process peak working set | 7,182.5 MiB |
| Sampled PP-StructureV3 peak memory | 8.9 GiB |
| Sampled PaddleOCR peak memory | 2.3 GiB |
| EPUBCheck 5.3.0 messages | 0 |

EPUBCheck validates the artifact under EPUB 3.3 rules with zero fatal errors,
errors, warnings or informational messages.

The canonical result also corrects the first native-only prototype's most
visible opening-order defect. Copyright matter ends in `section-0003.xhtml`;
the book no longer jumps from that matter directly to the later sentence
`And more than that...` in the same section. The next preliminary content is
the brief contents. Editorial noise is still visible, for example the isolated
heading `WI` in `section-0004.xhtml`, and the 734.6 MiB package requires a
separate review of the 295 preserved visuals before any acceptance decision.

## Next recommended experiment

Review the canonical V2 artifact rather than changing the exporter
speculatively. The first review should classify the remaining structural noise
and inspect the preserved visuals that dominate package size. Any new exclusion,
compression or size policy is a product decision and must be agreed before
implementation.
