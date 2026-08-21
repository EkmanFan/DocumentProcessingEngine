# Deterministic text normalization v1

## Scope

Increment 7.1 introduces text-only normalization after native extraction and
layout analysis.

It deliberately does not detect recurring headers or footers and does not
perform structural segmentation.

## Profile

```text
unicode-nfc-whitespace-dehyphenation-v1
```

The profile performs, in order:

1. Unicode NFC normalization;
2. CRLF and CR line-ending normalization to LF;
3. conservative line-break dehyphenation;
4. whitespace collapse;
5. leading/trailing whitespace removal.

## Conservative dehyphenation

A hyphen is removed only when all of the following are true:

- the character before the hyphen is a Unicode letter;
- the hyphen occurs at a line break, allowing horizontal whitespace around the
  break;
- the first character after the break is a Unicode lowercase letter.

Examples:

```text
inter-
national
```

becomes:

```text
international
```

while:

```text
Upper-
Case
```

becomes:

```text
Upper- Case
```

and an ordinary in-line hyphen such as `well-being` is preserved.

## Source evidence

Normalization is a derived projection.

`NormalizedDocumentTextBlock.SourceBlock` retains the exact extracted block and
`SourceText` exposes its original text. `Text` contains the normalized form.

No source text, geometry, source sequence, or reading-order evidence is mutated.

## Deferred

Recurring margin detection is the next increment. The historical ApologiaStudio
post-normalization Ehrman targets remain deferred until then:

- 531 recurring header blocks excluded;
- 0 recurring footer blocks excluded;
- 229 multi-column candidate pages;
- 144 interleaved-column pages;
- 10 vertical reading-order reversal pages.

Those numbers remain the historical ApologiaStudio comparison. The current
coordinate-corrected Engine baseline is documented by the recurring-margin
regression rather than retroactively replacing this historical reference.
