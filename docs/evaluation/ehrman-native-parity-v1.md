# Ehrman native PDF parity v1

## Purpose

This regression freezes the Document Processing Engine native-PDF baseline
against the previously validated ApologiaStudio pipeline before normalization
or OCR is introduced.

The evaluation intentionally separates native extraction evidence from
post-normalization diagnostics.

## Frozen source

- Work: *The New Testament: A Historical Introduction to the Early Christian Writings*
- Source SHA-256: `f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe`
- Byte length: `233,369,762`
- PDF pages: `617`

The PDF itself is not stored in this repository.

## Full-document native parity

The complete PDF must produce:

- 617 physical PDF pages;
- 233,595 native words;
- 3,179 raw layout blocks;
- 331 pages with native words;
- 286 pages without native words;
- 53.6% text-layer coverage;
- 286 textless pages with a dominant raster image.

Native word-stream probes must occur on:

- 6 pages for `TAKE A STAND`;
- 20 pages for `WHAT DO YOU THINK?`;
- 21 pages for `SUGGESTIONS FOR FURTHER READING`.

## Born-digital reference range

Physical PDF pages 398-405 must produce:

- 8 selected pages;
- 7 pages with native words;
- 1 page without native words;
- 4,728 words;
- 87 raw layout blocks;
- 1 textless dominant-raster page.

## Raster-only reference range

Physical PDF pages 14-20 must produce:

- 7 selected pages;
- 0 pages with native words;
- 7 pages without native words;
- 0 words;
- 0 layout blocks;
- 7 textless dominant-raster pages.

This is deliberately a no-OCR baseline.

## Deferred post-normalization parity

The following historical ApologiaStudio values are not native-stage assertions:

- 531 recurring header blocks excluded;
- 0 recurring footer blocks excluded;
- 229 multi-column candidate pages;
- 144 interleaved-column pages;
- 10 vertical reading-order reversal pages;
- normalized block-probe parity.

The current raw extraction observes 252 multi-column candidates,
146 interleaved pages, and 10 vertical-reversal pages before recurring margins
are excluded.

These post-normalization assertions belong to the normalization increment and
must not be forced into the PDF extractor.

## Run

Build first:

```bash
dotnet build
```

Then run:

```bash
bash scripts/evaluate-ehrman-native-parity.sh \
  --ehrman "/absolute/path/to/ehrman.pdf"
```

The generated JSON reports are written under `scripts/tmp/` and are ignored by
Git.
