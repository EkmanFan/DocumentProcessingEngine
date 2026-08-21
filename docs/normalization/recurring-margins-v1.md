# Recurring margin detection v1

## Purpose

Increment 7.2 extends deterministic text normalization with recurring
header/footer detection while retaining every extracted source block.

The active normalization profile is:

```text
unicode-nfc-whitespace-dehyphenation-recurring-margins-v1
```

## Heuristic

A block is eligible as a recurring margin candidate only when:

- its normalized text is non-empty;
- normalized text length is at most 160 characters;
- block height is positive and no more than 20% of page height;
- it begins in the top 12% of the page, or ends in the bottom 12%.

Core geometry uses a normalized top-left origin. Therefore:

- header candidate: `Bounds.Top <= 0.12`;
- footer candidate: `Bounds.Bottom >= 0.88`.

Recurrence keys:

1. use normalized block text;
2. convert to uppercase invariant form;
3. replace every digit run with `#`.

Digit canonicalization allows changing page numbers to be recognized as the
same recurring margin pattern.

The minimum recurrence count is:

```text
max(3, min(10, ceil(selectedPageCount * 0.02)))
```

A recurrence counts distinct physical pages, not repeated blocks on one page.

## Auditability

Source blocks are never deleted or mutated.

A normalized block exposes:

- `SourceBlock`;
- `SourceText`;
- normalized `Text`;
- `IsExcluded`;
- typed `ExclusionReason`.

Current exclusion reasons are:

```text
RepeatedHeader
RepeatedFooter
```

Downstream stages may omit excluded blocks from document content flow while the
source evidence remains available.

## Frozen real-document parity

The durable regression covers two documents.

### Ehrman

For the pinned 617-page artifact:

```text
raw blocks                 3179
included blocks            2648
excluded recurring headers 531
excluded recurring footers 0
multi-column pages          235
interleaved pages           154
vertical reversal pages     19
```

The layout counters are the current coordinate-corrected Engine baseline. The
historical ApologiaStudio comparison remains `229 / 144 / 10`; it is not the
current Engine output.

Normalized block probe counts:

```text
TAKE A STAND                      6
WHAT DO YOU THINK?                7
SUGGESTIONS FOR FURTHER READING  21
```

### De Decretis pages 512-561

```text
raw blocks                  269
included blocks             269
excluded recurring headers    0
excluded recurring footers    0
multi-column pages             4
interleaved pages              2
vertical reversal pages        3
```

This second corpus guards against over-aggressive recurring-margin exclusion.

## Run

Build first, then:

```bash
bash scripts/evaluate-recurring-margins-parity.sh
```

The source PDFs are identified by pinned SHA-256 values and are not stored in
the repository. JSON reports are written under `scripts/tmp/`.
