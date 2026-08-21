# Habermas EPUB reference V1

## Status

**EPUB-0 — Prepare the reference EPUB: PASS**

This reference records the exact local EPUB that will support the first EPUB
processing increments and later PDF/EPUB diagnostic comparisons. The
copyrighted publication remains local and is excluded from Git.

## Source identity

```text
local file   tests/document_corpus/epub/habermas-case-for-resurrection.epub
bytes        6,053,124
SHA-256      038c0e5a8ca13c93f4da0e0095ca73da5974e8d24aa9313f0781e110a641cbf5
title        The Case for the Resurrection of Jesus
identifier   urn:asin:B001QOGJY0
language     en-US
```

The source file name was corrected locally from `Habernas` to `Habermas`.
The EPUB bytes were not modified.

## Standards validation

The EPUB is validated with the official EPUBCheck 5.3.0 release against EPUB
3.3. The checker reports:

```text
fatal errors   0
errors         0
warnings       0
usage notices  0
```

Observed publication structure:

```text
package document   OEBPS/content.opf
navigation         OEBPS/nav.xhtml
layout             reflowable
spine items         36
scripted            no
encrypted           no
```

EPUBCheck owns standards conformance. The DPEngine reference workflow does not
reimplement the EPUB specification. It adds only exact source identity and
corpus-specific observations needed by later processing tests.

## PDF control page 18

The EPUB material corresponding to the existing PDF control is retained in:

```text
spine index       12
content document  OEBPS/part0012.xhtml
print marker      32
image             OEBPS/image_rsrc3U8.jpg
image dimensions  1551 x 261
```

The exact image byte length and SHA-256 are pinned in the JSON reference.

## PDF control page 28

The EPUB material corresponding to the existing PDF control is retained in:

```text
spine index       16
content document  OEBPS/part0016.xhtml
print marker      50
image             OEBPS/image_rsrc3U9.jpg
image dimensions  1297 x 1397
```

The XHTML sequence after that image is:

```text
image container
paragraph
paragraph used as a short section heading
```

There is no `figcaption` element in the content document. The exact normalized
text hashes are pinned without copying the long paragraph into the repository.
This is the useful EPUB-0 observation for the later PDF caption diagnostic: the
EPUB markup does not identify the long post-image paragraph as a
figure caption.

## Reproduction

The official checker archive belongs under:

```text
scripts/tmp/tool-cache/epubcheck-5.3.0.zip
```

Official distribution:

```text
https://github.com/w3c/epubcheck/releases/download/v5.3.0/epubcheck-5.3.0.zip
SHA-256  6c07e68584b2e2ce2f89fe06e1246dfead3eb36b46b340e7d93524f29dcff6c5
```

The workflow pins the complete distribution SHA-256, extracts it into temporary
storage, checks the version and JAR SHA-256, runs EPUBCheck, verifies the
package/spine controls, and then verifies the two real-corpus observations:

```bash
bash scripts/run-epub-reference-validation.sh
```

`HABERMAS_EPUB_FILE` and `EPUBCHECK_ZIP` may supply alternative local paths,
but their exact identities must still match the frozen reference.

## Scope boundary

EPUB-0 adds no EPUB format implementation and changes no production processing
result. The next increment may use this accepted reference to implement the
first native EPUB acquisition and projection path.
