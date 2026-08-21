# EPUB publication export prototype V1

## Status

**Current prototype — technically valid output, editorial quality not accepted**

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

## Next recommended experiment

First unblock the canonical PDF result on the representative Ehrman pages. Then
run the same exporter with the real result and preserved visuals. Improvements
to heading selection, paragraph continuity, inline semantics and alternative
text should be driven by inspection of that canonical output rather than added
speculatively to the EPUB packager.
