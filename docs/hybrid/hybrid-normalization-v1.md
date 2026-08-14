# Unified hybrid normalization V1

## Status

Phase 18C production-boundary increment.

```text
17   Native/OCR reconciliation                 DONE
18A  unified hybrid assembly boundary          DONE
18B  real page-233 hybrid runtime integration  DONE
18C  unified hybrid normalization              THIS INCREMENT
18D  structural segmentation over hybrid      NEXT
18E  broader end-to-end corpus regression      LATER
```

## Purpose

Normalization must happen **after** native/OCR evidence has been unified.

The engine must not normalize a native stream and an OCR stream independently
and then glue the normalized outputs together. That would reintroduce duplicate,
ordering, and provenance problems already solved by the hybrid assembly
boundary.

The V1 flow is:

```text
HybridDocumentAssemblyResult
        ↓
HybridDocumentNormalizer
        ↓
HybridDocumentNormalizationResult
        ↓
future Phase 18D segmentation
```

## Source of truth and provenance

Every normalized page retains its exact `HybridDocumentPage`.

Every normalized element retains its exact `HybridDocumentElement`.

Therefore all existing provenance remains reachable:

```text
physical page
reading order
bounds
HybridDocumentElementKind
TextSelectionOrigin
DocumentTextBlock
LayoutObservation
TextReconciliationResult
PreservedVisualEvidence
OCR observations through reconciliation
```

Normalization is a projection, not a destructive replacement.

## Unified element behavior

### Authoritative textual elements

```text
Text
Heading
Caption
```

retain their kind and text origin and receive normalized text.

### Visual

Remains:

```text
Text = null
TextOrigin = None
PreservedVisualEvidence retained
```

Normalization cannot create text for it.

### UnresolvedText

Remains textless and unresolved.

A reconciliation `Conflict` is not repaired or hidden by normalization.

### Deferred

Remains textless neutral evidence.

Unknown/Table evidence is not discarded merely because it is outside the
current textual path.

## Deterministic text rules

The existing born-digital normalizer already proved these generic rules:

- Unicode NFC;
- line-ending normalization;
- conservative native line-break dehyphenation;
- whitespace collapse;
- recurring margin detection.

Phase 18C extracts the generic text transform into the internal shared
`DeterministicTextNormalizationRules` component.

The existing `DocumentTextNormalizer` is changed only to consume those same
rules. Its public contract and normalization profile remain unchanged, and its
existing regression tests must all continue to pass.

This is the first justified shared abstraction because there are now two real
production consumers:

```text
DocumentTextNormalizer
HybridDocumentNormalizer
```

No framework, strategy registry, or plugin system is introduced.

## OCR-only dehyphenation

One extra issue exists for OCR-only hybrid text.

`TextReconciliationInput` with `NativeTextStatus.Missing` uses the raw
`OcrOnly` reconciliation path. OCR observations are composed into selected text,
but their explicit observation boundaries remain available through provenance.

Phase 17D already established the deterministic OCR rule:

```text
observation A ends "-"
AND
observation B begins lowercase
        ↓
remove boundary "-"
join observations
```

Therefore hybrid normalization reuses
`ReconciliationTextDehyphenator.DehyphenateOcr(...)` **only** when:

- selected origin is `Ocr`;
- an OCR region is explicitly available;
- reconciliation did not already carry `OcrTextPreparation`.

The resulting `TextDehyphenationResult` is retained as
`NormalizationDehyphenation` only when it actually changed the text.

No dictionary, fuzzy score, OCR confidence, spell checker, or LLM participates.

Hard hyphens inside one OCR observation remain untouched. A boundary before an
uppercase continuation remains separated.

## Recurring margins

Hybrid normalization applies the same V1 recurrence policy used by the existing
native path:

- only short authoritative text candidates;
- top/bottom margin zones;
- minimum three occurrences;
- occurrence threshold grows proportionally for larger documents;
- digit runs canonicalized so changing page/chapter numbers can recur.

Repeated headers and footers remain present and auditable:

```text
Text retained
ExclusionReason = RepeatedHeader / RepeatedFooter
IsTextFlowElement = false
```

Visual, Deferred and UnresolvedText elements are never margin candidates.

## Page boundaries

Physical pages remain explicit provenance boundaries:

```text
HybridDocumentNormalizationResult
  └── NormalizedHybridDocumentPage
```

They are **not** semantic segmentation boundaries.

Phase 18D must be allowed to form structural segments across physical pages when
the existing typography/structure evidence justifies continuity.

## 18C output

New neutral types:

```text
NormalizedHybridDocumentElement
NormalizedHybridDocumentPage
HybridDocumentNormalizationResult
```

New engine component:

```text
HybridDocumentNormalizer
```

The normalized element exposes:

```text
SourceElement
SourceText
Text
Kind
TextOrigin
Bounds
NativeBlock
LayoutObservation
Reconciliation
PreservedVisual
NormalizationDehyphenation
ExclusionReason
IsTextFlowElement
```

## Acceptance

18C unit/regression coverage must prove:

- native text uses the same generic normalization rules as the legacy native
  path;
- OCR-only observation-boundary dehyphenation is deterministic and auditable;
- uppercase OCR boundaries are not joined;
- Heading/Caption kind and Ocr origin survive normalization;
- Visual/Deferred/Conflict evidence remains textless;
- recurring headers and footers are excluded without deletion;
- repeated body content is not excluded;
- page/element source identity and order are preserved;
- text-flow projection excludes margins but retains non-text evidence in the
  full normalized page;
- cancellation is honored;
- all existing native normalization regressions remain green.

## Explicit non-goals

18C does not:

- perform PDF extraction;
- call PP-StructureV3 or PaddleOCR;
- select native versus OCR authority;
- resolve `Conflict`;
- OCR figures;
- change visual bytes;
- create semantic segments;
- infer paragraphs across pages;
- create `DocumentIngestionResult`;
- add a generic pipeline/orchestration framework.

## Next

Phase 18D should make structural segmentation consume the unified normalized
hybrid text flow.

The critical design requirement is:

> segment the already-unified native/OCR text stream; do not segment native and
> OCR independently and merge segments afterward.

The existing strict typography-aware cross-page segmentation remains the
behavioral benchmark, but Phase 18D must preserve hybrid origin/provenance rather
than manufacturing native `DocumentTextBlock` evidence for OCR text.
