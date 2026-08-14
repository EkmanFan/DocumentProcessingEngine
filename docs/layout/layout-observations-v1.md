# Layout observations V1

## Status

Production boundary increment.

This increment is the first production code derived from the OCR/layout
evaluation series. It intentionally stops before live model execution and OCR
routing.

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

There is no live analyzer implementation yet, so introducing a generic
`ILayoutAnalyzer` abstraction in this increment would be premature.

The next production increment should add deterministic treatment policy and the
smallest live execution boundary required to obtain these observations from a
raster page.
