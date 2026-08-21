# Local test-document layout

## Status

**Current — operational guidance**

The PDF test documents are local-only and excluded from Git. The repository
commits their expected identities and semantic results, but not the copyrighted
source files themselves.

## Main 67-document test set

The root of `tests/pdf_pages_test/` contains exactly the 67 one-page PDFs used
by the frozen native/provenance regression:

```text
tests/pdf_pages_test/
├── fixtures-manifest.tsv
├── ehrman-*.pdf
├── habermas-*.pdf
└── decretis-*.pdf
```

Every root-level PDF must have one row in `fixtures-manifest.tsv`, and every row
must identify one existing root-level PDF. The regression deliberately checks
both directions so that a missing, replaced or accidentally added document
cannot silently change the corpus.

Do not add another PDF to this root directory unless the 67-document baseline
is intentionally being replaced and reviewed.

## Additional targeted documents

Documents used by later, narrower investigations belong in a subdirectory:

```text
tests/pdf_pages_test/supplemental/
├── habermas-p0018.pdf
└── habermas-p0028.pdf
```

The main regression scans only the root directory. Supplementary documents
remain available for their targeted evaluations without changing the frozen
67-document set.

## EPUB reference document

The copyrighted Habermas EPUB is also local-only:

```text
tests/epub_test/
└── habermas-case-for-resurrection.epub
```

Its exact identity and selected structural observations are committed in
`habermas-epub-reference-v1.json`. The EPUB itself remains excluded from Git.
Run `scripts/run-epub-reference-validation.sh` to validate it with the pinned
official EPUBCheck version and the corpus-specific controls.

## Exact PNG reference

The p233 visual reference was encoded by `pdftoppm` 26.01.0. Later Poppler
versions can produce a PNG with identical dimensions and pixels but different
compression bytes and therefore a different file SHA-256.

`scripts/run-semantic-ocr-regression.sh` selects `/usr/bin/pdftoppm` by default
and requires version 26.01.0 before running the exact-byte oracle. A different
absolute installation path may be supplied through `PDFTOPPM_EXECUTABLE`, but
the required version remains unchanged.
