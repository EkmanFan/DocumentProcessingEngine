# Layout observations V1

## Status

Production boundary series.

This document records the production code derived from the OCR/layout
evaluation series. Live PP-StructureV3 execution has now been validated against
the pinned Ehrman mixed-content page. Targeted OCR and figure persistence remain
outside the current boundary.

## Established evidence

LAYOUT-0A on Ehrman physical PDF page 233 / printed page 202 demonstrated that
PP-StructureV3 can separate the representative mixed-content page into useful
structural roles:

```text
paragraph_title
text
image
figure_title
text
```

The papyrus facsimile was emitted as `image`, no text-like parsing block was
centered inside the facsimile, the caption was distinct, and the modern
left-to-right narrative order was usable.

## Neutral Core model

The engine now owns a backend-independent layout observation:

```text
LayoutObservation
  PhysicalPageNumber
  ObservationSequence
  ReadingOrder?
  Kind
  Bounds
  RawLabel?
```

V1 neutral kinds are intentionally small:

```text
Unknown
Text
Heading
Caption
Figure
Table
```

Backend vocabularies must not leak into consumers except through the optional
diagnostic `RawLabel`.

## Deliberate absence of recognized text

`LayoutObservation` has no text/content property.

This is a correctness boundary, not an omission.

PP-StructureV3 may internally run OCR and may populate `block_content` even for
an `image` block. LAYOUT-0A demonstrated that the papyrus image could therefore
carry OCR-like noise.

The production adapter discards `block_content` completely.

Consequently:

```text
layout evidence != OCR evidence
```

A later OCR increment must introduce OCR text explicitly rather than receiving
it accidentally through layout analysis.

## PP-StructureV3 adapter

`PpStructureV3LayoutAdapter` accepts the JSON representation of one
PP-StructureV3 page and maps `parsing_res_list` to the neutral Core model.

Supported JSON shapes:

```text
{ "res": { "parsing_res_list": [...] } }
```

and:

```text
{ "parsing_res_list": [...] }
```

This matches the PP-StructureV3 result shape used by the evaluated backend while
keeping all Paddle-specific parsing outside Core.

The adapter uses array position as both:

```text
ObservationSequence
ReadingOrder
```

for this backend because PP-StructureV3 defines `parsing_res_list` itself as
being in parsed reading order. The two concepts remain separate properties in
Core and are not assumed to be equal for other backends.

## V1 label mapping

```text
PP-StructureV3                 Core
---------------------------------------------
text                           Text
paragraph_title                Heading
doc_title                      Heading
figure_title                   Caption
figure_caption                 Caption
image                          Figure
figure                         Figure
header_image                   Figure
footer_image                   Figure
table                          Table
anything else                  Unknown
```

The mapping is intentionally conservative. For example, `header`, `number`,
`footnote`, and other labels are not promoted to narrative `Text` until a real
policy requirement is implemented and evaluated.

## Scope intentionally not included

This increment does not add:

```text
ILayoutAnalyzer
Python process launching
Docker orchestration
PP model lifecycle
OCR execution
OCR routing
Figure asset persistence
Figure-caption association
native/OCR reconciliation
quality gates
GPU execution
```

At that increment there was no live analyzer implementation. The later concrete
PP-StructureV3 serving client remains the only selected backend, so introducing
a generic `ILayoutAnalyzer` abstraction is still premature.

## Deterministic treatment policy

Production Layout Increment 2 adds the first engine-owned processing policy for
layout observations.

The policy is deliberately small:

```text
LayoutObservationKind          LayoutTreatment
-------------------------------------------------------------
Text                           RecognizeText
Heading                        RecognizeText
Caption                        RecognizeText
Figure                         PreserveVisualWithoutOcr
Table                          RecognizeText
Unknown                        Deferred
undefined enum value           Deferred
```

This makes the mixed-content safety rule explicit in deterministic code:

```text
figure/image -> preserve visual evidence -> no OCR
```

The decision is not delegated to PP-StructureV3, PaddleOCR, an LLM, a prompt, or
backend-provided `block_content`.

`Unknown` remains deliberately deferred and fail-closed.

Phase 18E provided the missing representative evidence for `Table`: the pinned
Ehrman raster table-of-contents pages 14–20 were classified predominantly as
`Table`. Deferring those regions caused physical pages 14, 16, and 18 to emit no
authoritative text even though the page visibly contains document text.

`Table` is therefore now authorized for OCR text recovery. This is deliberately
a **text fallback**, not table-structure extraction: the original
`LayoutObservationKind.Table` remains attached as provenance while the recovered
text participates in neutral hybrid text flow. Row/column/cell reconstruction
remains deferred.

The representative Ehrman page 233 sequence therefore becomes:

```text
Heading -> RecognizeText
Text    -> RecognizeText
Figure  -> PreserveVisualWithoutOcr
Caption -> RecognizeText
Text    -> RecognizeText
```

This increment still does not execute OCR or persist the figure. It only makes
the treatment decision explicit and testable.

## Live PP-StructureV3 execution boundary

Production Layout Increment 3 adds a concrete client for the official
self-hosted PP-StructureV3 serving contract.

The engine deliberately does not launch Python, Paddle, Docker, or a model
process. The architecture remains .NET-first and model hosting is an external
infrastructure concern. The selected boundary is therefore:

```text
raster image stream
        ↓
PpStructureV3ServingClient
        ↓ HTTP POST /layout-parsing
self-hosted PP-StructureV3 service
        ↓ prunedResult
PpStructureV3LayoutAdapter
        ↓
LayoutAnalysisResult
```

The request uses the same conservative feature switches established by
LAYOUT-0A: orientation classification, unwarping, text-line orientation,
seal, table, formula and chart recognition are disabled; region detection is
enabled. Visualization and Markdown image payloads are disabled because this
boundary only consumes layout evidence.

The service may still internally emit `block_content`. Only `prunedResult` is
passed to `PpStructureV3LayoutAdapter`, which continues to discard recognized
content and retain only label/order/bounds evidence.

Operational safeguards in this boundary are intentionally explicit:

```text
caller cancellation
finite per-request timeout
bounded input image size
bounded HTTP response size
HTTP status validation
service errorCode validation
exactly one page result for image input
fail-closed response schema checks
```

`HttpClient` is injected so connection pooling, DNS behavior, proxy/TLS
configuration and application lifetime remain with the hosting application.
No generic `ILayoutAnalyzer` is introduced yet: there is still one selected
layout backend and one concrete execution boundary.

## Live service integration validation

Phase B / 14B validated this HTTP boundary on 2026-08-14 against a real
self-hosted PP-StructureV3 service using the pinned Ehrman physical page 233.

The production `PpStructureV3ServingClient` successfully called
`POST /layout-parsing`, produced 10 neutral observations, and satisfied every
existing mixed-content acceptance gate. The representative sequence was:

```text
Heading -> RecognizeText
Text    -> RecognizeText
Figure  -> PreserveVisualWithoutOcr
Caption -> RecognizeText
Text    -> RecognizeText
```

The papyrus facsimile matched `Figure` with IoU 0.923 and remained subject to
`PreserveVisualWithoutOcr`. No text/content property exists on the neutral
`LayoutObservation`, so backend OCR-like `block_content` remains outside the
layout evidence boundary.

The observed request duration for this CPU integration run was approximately
8.66 seconds. This is recorded as an observation only, not as a performance
target or SLA.

Detailed evidence is recorded in:

```text
docs/evaluation/layout-ppstructurev3-live-integration-v1.md
docs/evaluation/layout-ppstructurev3-live-integration-v1.json
```

Phase B / 14B is therefore complete.

The next production increment is Phase B / 15: targeted OCR for regions whose
deterministic treatment is `RecognizeText`. Figure persistence, native/OCR
reconciliation and end-to-end hybrid regression remain separate later
increments.
