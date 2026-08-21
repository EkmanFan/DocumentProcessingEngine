# Local test-document layout

## Status

**Current — operational guidance**

The PDF and EPUB test documents share one local-only corpus excluded from Git.
The repository commits their expected identities and processing results, but
not the copyrighted source files themselves.

```text
tests/document_corpus/
├── epub/
└── pdf/
    ├── full/
    ├── pages/
    └── supplemental/
```

## Complete PDF source documents

The `tests/document_corpus/pdf/full/` directory contains the complete works
from which the one-page PDF controls were selected:

```text
tests/document_corpus/pdf/full/
├── Nicene and Post Nicene Fathers Series II Vol 4.pdf
├── The Case for the Resurrection of Jesus - Gary R. Habernas.pdf
└── the-new-testament-a-historical-introduction-to-the-early-christian-writings.pdf
```

These files support full-document processing and the reproducible regeneration
of page-level controls. They are not scanned implicitly by the frozen
67-document regression.

## Main 67-document test set

The `tests/document_corpus/pdf/pages/` directory contains exactly the 67
one-page PDFs used by the frozen native/provenance regression:

```text
tests/document_corpus/pdf/pages/
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
tests/document_corpus/pdf/supplemental/
├── habermas-p0018.pdf
└── habermas-p0028.pdf
```

The main regression scans only the root directory. Supplementary documents
remain available for their targeted evaluations without changing the frozen
67-document set.

## EPUB test documents

The EPUB corpus is local-only:

```text
tests/document_corpus/epub/
├── habermas-case-for-resurrection.epub
├── Institution de la Religion Chretienne.epub
├── Jesus and the Eyewitnesses - The Gospels as Eyewitness Testimony - Richard Bauckham.epub
├── Logic and Philosophy - William H. Brenner.epub
└── La Septante Grec-Francais - Ouvrage Collectif.epub
```

Their exact identities and selected structural observations are committed in
the EPUB evaluation references. The EPUB files remain excluded from Git. Run
`scripts/run-epub-reference-validation.sh` and
`scripts/run-epub-multi-corpus-regression.sh` to validate them with the pinned
official EPUBCheck version and the corpus-specific controls.

## Exact PNG reference

The p233 visual reference was encoded by `pdftoppm` 26.01.0. Later Poppler
versions can produce a PNG with identical dimensions and pixels but different
compression bytes and therefore a different file SHA-256.

`scripts/run-semantic-ocr-regression.sh` selects `/usr/bin/pdftoppm` by default
and requires version 26.01.0 before running the exact-byte oracle. A different
absolute installation path may be supplied through `PDFTOPPM_EXECUTABLE`, but
the required version remains unchanged.
