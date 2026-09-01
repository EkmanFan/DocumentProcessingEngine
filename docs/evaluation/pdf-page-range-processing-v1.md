# PDF page-range processing V1

Status: current validation.

## Purpose

This increment closes the gap between Manager page-range queue support and a
complete `DocumentProcessingResult`. A range is a processing selection over one
immutable source PDF; it is not a new shorter source document.

## Required semantics

For a 170-page source processed as pages 51–100:

- source custody still identifies the original 170-page PDF;
- extraction, observations, elements and page descriptors retain physical page
  numbers 51–100;
- the result records `sourcePhysicalPageCount = 170` and contains 50 processed
  page descriptors;
- no component renumbers the selection to pages 1–50;
- an element outside the processed selection is rejected;
- visual observation and materialization reopen only physical pages retained by
  the extraction while still verifying the complete source page count.

The portable schema carrying this distinction is
`document-processing-result-v4`.

## Regression coverage

Automated coverage includes:

- PDF extraction and visual observation for a range against a larger source;
- Engine-owned processing for a selection beginning at page 1;
- Engine-owned processing for a selection beginning after page 1;
- source-size versus processed-selection invariants;
- the renamed engine-facing `PagedDocumentProcessingModel` projection;
- Manager JSON publication of `sourcePhysicalPageCount`.

The legacy `DocumentIngestionResult` name is no longer used by active source,
tests or tools. `DocumentProcessingResult` remains the only consumer-facing
result.

## Validation boundary

This increment uses generated and qualified targeted fixtures. It does not run
the complete PDF corpus or treat corpus traversal as a semantic oracle.
