# EPUB-4 multi-corpus structural hardening V1

## Status

**EPUB-4 — Navigation headings and contextual visual evidence: complete**

EPUB-4 converts the Calvin and Bauckham corpus findings into deterministic
format evidence. It does not use source filenames, book titles or
corpus-specific resource names.

## Navigation-backed headings

An EPUB navigation document with `epub:type="toc"` is authoritative structural
evidence. For every packaged target:

- an explicit fragment identifies its containing or first descendant text
  block;
- a resource target without a fragment identifies the resource's first text
  block;
- that block is projected as a heading even when the publisher encoded it as a
  styled paragraph instead of an XHTML `h1`–`h6` element.

Native `h1`–`h6` elements remain headings. Page-list references are not
promoted to headings.

## Contextual visual evidence

The EPUB adapter now acquires three additional deterministic facts:

1. An image inside an XHTML `figure` is documentary structured content.
2. A packaged image is presentational when it is used more than once, every
   usage has an explicitly empty alternative text, no usage belongs to a
   `figure`, and the exact resource is at most 1,024 bytes.
3. Without an authoritative body-matter boundary, a final spine resource is
   presentational when it contains images but no textual blocks. A final
   resource containing text is treated the same way only when it is absent from
   publication navigation, contains multiple images, and exposes at least one
   external HTTP(S) link per image.

The adapter retains the acquired text blocks and marks the complete terminal
content unit as presentational; the Engine owns its omission from documentary
output. The Engine maps the first visual fact to
`StructuredContentMeaningfulVisual` and the latter facts to
`PublicationPresentationVisual`. These decisions occur before the optional
Paddle fallback.

## Frozen corpus results

```text
Habermas   31 headings   24 Meaningful visuals   0 Paddle calls
Calvin    139 headings    0 retained visuals     0 Paddle calls
Bauckham   34 headings    1 Meaningful visual    0 Paddle calls
```

For Calvin, the omitted terminal image is the fourth cover. For Bauckham, the
only retained visual is the captioned Irenaeus diagram; the repeated separator
and five terminal promotional covers are omitted.

The exact summaries are frozen in
`docs/evaluation/epub-multi-corpus-reference-v1.json`.

Run:

```bash
./scripts/run-epub-multi-corpus-regression.sh
```

The two corpus EPUB files remain local and ignored by Git. The script runs each
corpus with the default request and with unresolved-visual qualification
enabled. Identical results with the evaluation CLI's unreachable Paddle
endpoint prove that all selected or omitted visuals were resolved from EPUB
facts.

## Remaining boundary

CSS background images, generic `object` resources and video poster frames
remain outside the V1 acquisition boundary. The terminal-presentation rules
are intentionally narrow and must be expanded only from additional frozen
corpus evidence.
