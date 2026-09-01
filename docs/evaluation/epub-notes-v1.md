# EPUB Notes V1

## Status

**Current design and targeted acceptance evidence**

EPUB Notes V1 introduces native extraction profile
`epub-xhtml-native-v4+epubcheck-5.3.0` and portable processing-result schema
`document-processing-result-v4`.

## Responsibility boundary

`DocumentProcessing.Epub` concludes note relations from explicit XHTML facts:

- `epub:type="noteref"` or `role="doc-noteref"` references;
- internal `href` targets, including cross-resource targets;
- payloads annotated as footnotes/endnotes or with equivalent ARIA roles;
- stable EPUB resource, block and fragment custody.

The format emits neutral `StructuredNativeDocumentNote` evidence. It does not
exclude content or construct the portable result. The Engine resolves reference
owners to stable elements, retains payload elements and processing evidence,
excludes concluded payloads from narrative segments, and projects
`DocumentNote` values.

Correlation and narrative-flow classification are independent decisions:

```text
explicit or reciprocal unique relation -> concluded note, payload excluded
unresolved relation + note payload      -> no invented note, payload excluded
ordinary content                         -> narrative content
```

Every excluded payload remains a portable element with source and processing
evidence, so unresolved relations do not cause silent evidence loss or pollute
reading/chunking segments.

No physical footnote/endnote placement is promoted to portable semantics.

## Targeted evidence

Deterministic fixtures cover inline payloads, nested payload markup, backlinks,
multiple references, cross-resource endnotes, equivalent ARIA roles, missing
targets, external links, duplicate targets, missing reference owners,
unreferenced annotated payloads and ordinary asides.

Two local EPUB controls were executed without PDF, Docker, layout or OCR:

- Habermas: 477 explicitly annotated payloads, 478 references and 478 concluded
  relations. Reciprocal backlinks repair the malformed forward link for note
  `22`: note `21` resolves to `a33X`, while the unannotated payload `a36X`
  resolves to marker `22` instead of being concatenated or left in narrative
  content.
- Allison: 4,017 annotated payloads and 4,017 unambiguous relations. Four
  references are owned directly by table cells; generic `td`/`th` structured
  text acquisition preserves their owner elements.

These counts characterize the current local files. The fail-closed rules and
fixture tests are the portable regression contract.
