# De Decretis native PDF parity v1

## Purpose

This evaluation freezes the first real-document parity target for the
Document Processing Engine before normalization or segmentation is added.

It compares the engine's current native PDF extraction, layout blocks, and
reading-order diagnostics with the already validated ApologiaStudio Stage 2D
result for the same source artifact and page range.

## Frozen source

- Source artifact: NPNF2-04 PDF (`npnf204`)
- SHA-256: `de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75`
- Byte length: `11,963,985`
- Total PDF pages: `1,479`
- Selected physical PDF pages: `512-561`

The PDF itself is not stored in this repository.

## Frozen native parity assertions

The selected 50-page range must produce:

- 50 pages with extractable native text;
- 100.0% text-layer coverage;
- 29,044 words;
- 269 layout blocks;
- 0 textless dominant-raster pages;
- 4 multi-column candidate pages;
- 2 interleaved-column pages;
- 3 vertical reading-order reversal pages;
- exactly one page-word-stream and one block match for
  `endless ages of ages. Amen.`

The historical ApologiaStudio report excluded zero recurring headers and zero
recurring footers for this source range. Therefore these layout diagnostics are
comparable before the new engine's normalization stage exists.

## Architectural boundary

Document-specific hashes, page ranges, probes, and expected metric values remain
in the external evaluation runner:

```text
scripts/evaluate-de-decretis-native-parity.sh
```

They do not belong in `DocumentProcessing.Core` or `DocumentProcessing.Pdf`.

The generic evaluation CLI only analyzes a caller-supplied PDF, page range, and
optional probes and emits bounded metrics. It does not persist document content,
create retrieval chunks, run OCR, or add source-specific production behavior.

## Run

```bash
bash scripts/evaluate-de-decretis-native-parity.sh \
  --de-decretis "/absolute/path/npnf204.pdf"
```

The JSON report is written under `scripts/tmp/` by default and is ignored by Git.
