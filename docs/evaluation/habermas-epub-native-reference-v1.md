# Habermas native EPUB processing reference V1

## Status

**EPUB-1 — First native EPUB processing: PASS**

The production Host recognizes the exact EPUB-0 Habermas source, validates it
with the pinned official EPUBCheck 5.3.0 distribution, acquires its package,
spine and XHTML text, and returns a non-paged `DocumentProcessingResult`.

## Accepted result

```text
source bytes                 6,053,124
spine items                 36
linear spine items          36
portable text elements   1,729
portable structural units   32
heading elements             0
caption elements             0
paged element locations      0
```

Every retained element uses `EpubDocumentSourceLocation`, containing its spine
index, XHTML resource path, block index and optional source fragment ID. The
result retains `EpubDocumentSourceStructure` with package metadata and exact
spine order. No EPUB spine resource is represented as a physical page.

The SHA-256 of the complete authoritative element-text sequence is frozen in
`habermas-epub-native-reference-v1.json` without copying the copyrighted text
into Git.

## Semantic observation

The reference EPUB produces no native heading or caption element in this first
pass because its relevant short headings and post-image text are encoded as
ordinary XHTML `p` elements. The extractor preserves that source fact instead
of guessing from typography or proximity.

This is now useful diagnostic evidence: later EPUB semantic enrichment can be
measured explicitly, and the EPUB does not falsely authorize treating the long
paragraph after the page-28 image as a caption.

## Reproduction

The copyrighted EPUB and official EPUBCheck archive remain local. Run:

```bash
./scripts/run-epub-native-regression.sh
```

The workflow verifies both pinned source identities, extracts the official
EPUBCheck distribution into temporary storage, builds the evaluation command
in Release with warnings as errors, processes the EPUB through the production
Host, and compares the complete report with the frozen JSON reference.

## EPUB-2 continuation

EPUB-1 remains the frozen textual baseline. EPUB-2 now discovers and preserves
EPUB image resources through `UserVisualAssetWriter` while the EPUB-1 text
report remains byte-for-byte identical. Its independent visual evidence is
documented in `docs/epub/epub-2-visual-preservation-v1.md`.
