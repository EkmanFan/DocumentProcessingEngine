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
portable text elements   2,204
portable structural units   32
heading elements            31
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

## Structural observation

The reference EPUB produces 31 heading elements. EPUB-4 promotes the blocks
targeted by the standardized EPUB navigation table even though the publisher
encoded them as ordinary XHTML `p` elements. It still produces no native
caption element. Heading promotion changes the element category, not its
retained text.

The EPUB does not falsely authorize treating the long paragraph after the
page-28 image as a caption. Native extraction profile V3 additionally retains
the publication's 477 standard XHTML `aside epub:type="footnote"` containers.
Nested block content remains present exactly once.

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

EPUB-1 remains the frozen textual baseline. EPUB-2 discovers and preserves EPUB
image resources, while EPUB-3 qualifies them from publication structure with
an optional user-controlled Paddle fallback. The later PDF/EPUB comparison
identified previously omitted standard footnote containers; native extraction
profile V3 therefore supersedes the incomplete V2 text fingerprint.
