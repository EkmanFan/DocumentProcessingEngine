# Manager structural-heading partitioning V1

Status: implemented on 2026-09-02 as `MGR-BAT-03`; the PDF heuristic remains
to be consolidated against a representative corpus of documents without a
usable outline.

## Purpose

`MGR-BAT-02` proposes partitions from publisher-supplied PDF outlines and EPUB
navigation. Some otherwise structured documents do not contain usable native
navigation. `MGR-BAT-03` adds a deterministic structural-heading fallback while
preserving the same neutral proposal, human approval and atomic queue
replacement workflow.

The ordered policy is:

```text
qualified native navigation
        |
        | no safe proposal
        v
deterministic structural headings
        |
        | no safe proposal
        v
manual editor only
```

Mechanical size-based splitting is not selected automatically by this policy.

## Contracts and ownership

Formats implement the optional `IStructuralHeadingDocumentFormat` capability.
They expose a recognized format, its complete native coordinate axis and
ordered heading observations. The shared `DocumentStructureAxis` and
`DocumentStructurePosition` contracts carry either physical pages or stable
ordered content units without inventing one universal page model.

The format adapter owns only native evidence acquisition. Manager Core maps
those observations to `DocumentPartitionEvidence` with origin
`StructuralHeading`. `StructuralHeadingPartitionStrategy` owns the pure,
synchronous and deterministic proposal policy. It performs no I/O and never
mutates the queue.

`DocumentProcessingSplitPreviewProvider` always evaluates native navigation
first. Structural inspection is requested only when native navigation cannot
produce a complete qualified proposal. An automatic structural proposal has
categorical reliability `Fallback` and still requires explicit human approval.

## PDF evidence policy

The PDF adapter uses only PdfPig native words, point-size and word geometry. It
does not enumerate source images, rasterize pages, inspect note links, invoke
layout ML or run OCR.

The V1 detector:

- reconstructs native text lines from word geometry;
- establishes the dominant body size through a word-weighted median;
- retains short lines of at most 20 words and 160 characters whose point size
  is at least 1.25 times the body size;
- removes identical running headers observed on at least three pages;
- groups candidate point sizes into hierarchy levels with an eight-percent
  tolerance;
- preserves physical source page numbers.

The complete text layer must be inspected to discover document-wide
boundaries. This remains substantially lighter than processing because it uses
no raster, external provider, OCR, reconciliation, assembly or publication.

## EPUB evidence policy

The EPUB adapter reads the package and authoritative spine, then projects
native XHTML `h1` through `h6` elements. Their element number becomes the
structural hierarchy level and their text becomes the suggested title.

V1 boundaries remain aligned to complete spine resources. Multiple headings
inside one XHTML file are retained as evidence, but the strategy refuses a
hierarchy level containing two peer headings on the same content unit. It does
not fabricate fragment-level execution support.

## Fail-closed rules

The structural strategy requires at least two boundaries at one hierarchy
level. It creates ordered, contiguous, non-overlapping segments covering the
complete source axis, including leading matter before the first heading.

It returns no proposal when:

- fewer than two usable peer headings exist;
- peer headings resolve to the same source coordinate;
- heading order moves backwards on the source axis;
- the inspection axis does not match the preview axis;
- format evidence is missing or unsupported.

An ambiguous usable level terminates structural fallback instead of silently
selecting a deeper hierarchy. The manual split editor remains available.

## Known limits

- PDF headings using only bold, color or whitespace at body point size are not
  detected in V1.
- Scanned PDFs without a native text layer receive no structural proposal.
- Conservative duplicate-coordinate rejection may decline multi-line PDF
  titles or multiple peer headings within one EPUB spine resource.
- The qualified Habermas EPUB relies on publisher navigation and styled
  paragraphs rather than native `h1` through `h6`; it correctly stays on the
  preferred MGR-BAT-02 path instead of manufacturing fallback headings.
- OCR- or LLM-derived boundaries are deliberately outside this increment and
  require a separately qualified evaluation policy.

## Acceptance evidence

- the same structural strategy builds complete physical-page and content-unit
  proposals;
- ambiguous and non-monotonic evidence fails closed;
- EPUB without navigation exposes native `h1`/`h2` hierarchy on its spine axis;
- PDF native typography produces headings while repeated running headers are
  excluded;
- the Manager produces a structural fallback proposal for a PDF without an
  outline;
- a document with qualified native navigation keeps the
  `native-navigation-v1` proposal;
- the complete solution builds with warnings treated as errors and the unit and
  targeted integration suites pass.
