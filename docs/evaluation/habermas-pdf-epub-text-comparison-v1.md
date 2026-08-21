# Habermas PDF/EPUB text comparison V1

## Status

**PASS — cross-format comparison found and closed one EPUB text omission**

The complete PDF and EPUB editions of *The Case for the Resurrection of Jesus*
were processed through the production Host. Copyrighted text and visual bytes
remain local and ignored by Git. This record retains only source identities,
hashes, counts and aggregate comparison measurements.

## Defect found by the first comparison

The first EPUB capture contained 94,117 normalized tokens, compared with
120,968 for the PDF. The difference was concentrated in the notes:

```text
PDF notes                        37,294 tokens
EPUB result before correction   11,336 tokens
EPUB notes XHTML source         37,308 tokens
```

The publication represents its 477 notes with the standard XHTML form
`aside epub:type="footnote"`. The EPUB adapter traversed recognized descendant
blocks but lost inline text owned directly by these unrecognized containers.

Native extraction profile `epub-xhtml-native-v3+epubcheck-5.3.0` now treats an
`aside` as one textual block. Traversal stops at the recognized container, so
nested paragraphs are retained exactly once rather than duplicated. A focused
regression freezes inline and nested footnotes in reading order.

The same correction retains 511 previously omitted asides in Calvin and 1,639
in Bauckham. Brenner and the Septante contain no asides and retain their exact
text fingerprints. Visual reports are unchanged for all four publications.

## Corrected Habermas comparison

Text is compared after Unicode Form KC normalization, soft-hyphen removal,
line-break dehyphenation, whitespace collapse, invariant lower-casing and
Unicode letter/number tokenization.

| Measurement | PDF | EPUB |
|---|---:|---:|
| Result elements | 1,217 | 2,228, including 24 visuals |
| Textual elements | 1,194 | 2,204 |
| Normalized tokens | 120,968 | 120,082 |

The corrected structural partitions are:

| Publication part | PDF tokens | EPUB tokens | Difference |
|---|---:|---:|---:|
| Main matter through appendix | 80,268 | 79,662 | +606 PDF |
| Notes | 37,294 | 37,301 | +7 EPUB |
| Bibliography | 3,119 | 3,119 | 0 |
| Terminal PDF-only matter | 287 | 0 | +287 PDF |

A zero-context histogram diff finds 118,707 tokens in the same order. That is
98.8549% of the EPUB sequence and 98.1309% of the PDF sequence. Comparing token
occurrence counts without order finds 120,027 common occurrences, or 99.9542%
of the EPUB sequence.

The order-independent value is diagnostic only. Repeated words can match even
when their local context differs, so it is not proof of exact equality. The
remaining ordered differences include PDF page furniture, physical-line
hyphenation, terminal matter and local extraction/order differences that need
separate inspection if a stricter equivalence gate is required.

## Visual preparation

The text run does not need to be repeated for the later visual comparison:

- the 22 current PDF visual assets are retained locally;
- the 24 EPUB packaged JPEGs are retained locally;
- all 24 EPUB bytes match their Engine custody hashes.

No visual correspondence claim is made in this text increment.

## Regression boundary

The correction is accepted only when all of the following pass:

- focused EPUB extraction tests;
- exact Habermas native EPUB regression;
- default and opt-in four-corpus EPUB regression;
- Release build with warnings as errors;
- complete unit and integration regression.

Machine-readable evidence is retained in
`habermas-pdf-epub-text-comparison-v1.json`.
