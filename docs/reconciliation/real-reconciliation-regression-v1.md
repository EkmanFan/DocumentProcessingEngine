# Real native/OCR reconciliation regression V1

## Status

Phase B / 17E closeout increment.

```text
17 Native/OCR reconciliation
   17A deterministic reconciliation boundary      DONE
   17B real evidence + human diagnosis            DONE
   17C comparable native text extent              DONE
   17D deterministic dehyphenation                DONE
   17E real reconciliation regression             THIS INCREMENT

18 End-to-end hybrid regression                   NEXT
```

## Purpose

17E connects the two corrective boundaries proven by real evidence:

```text
raw native block + OCR region
        ↓
ComparableNativeTextExtent
        ↓
deterministic source-aware dehyphenation
        ↓
existing deterministic reconciliation policy
```

The authority policy remains conservative and unchanged.

## Comparable reconciliation API

The Phase 17A `Reconcile(TextReconciliationInput)` overload is retained for
backward compatibility and low-level policy tests.

Real Phase 17B/17C evidence demonstrated that one `DocumentTextBlock` may cover
a larger textual extent than one OCR/layout region. Production callers that
have a paired native block and OCR region should therefore first obtain a
`ComparableNativeTextExtent` and call:

```text
NativeOcrTextReconciler.ReconcileComparable(...)
```

This explicit API does not invent an automatic matcher. Pairing remains supplied
by the caller at the Phase 17 boundary.

## Result provenance

`TextReconciliationResult` optionally retains:

- the `ComparableNativeTextExtent`;
- deterministic native `TextDehyphenationResult`;
- deterministic OCR `TextDehyphenationResult`.

Original native/OCR/layout evidence remains available through the raw input.

## Real corpus regression

The live evaluator reuses retained real Phase 17B OCR observations and the
current PdfPig extraction. It does not call Paddle services.

### Physical page 233 — Missing

Expected:

```text
decision: OcrOnly
selected origin: Ocr
resolved: true
divergence: false
```

### Physical page 405 — Healthy

17C established the 132-word comparable extent. 17D established four
deterministic joins on both sources.

Expected:

```text
decision: Agreement
selected origin: NativePdf
TextsEquivalent: true
resolved: true
divergence: false
selected prepared native chars: 720
prepared OCR chars: 720
```

No similarity threshold participates in the decision.

### Physical page 380 — Suspicious

17C established the 299-word comparable extent. 17D established five
deterministic joins on both sources while real character differences remained.

Expected:

```text
decision: Conflict
selected origin: None
TextsEquivalent: false
resolved: false
divergence: true
prepared OCR chars: 1683
```

## No "minor divergence" state yet

17E deliberately does not add one.

The current evidence shows that small character-level disagreement can contain
errors on either side. A magnitude label would not answer which source is
authoritative. Adding a fuzzy selection rule or a new authority state would
therefore increase policy complexity without a deterministic basis.

For Suspicious native evidence, `Conflict` remains the safe explicit result.

## Explicit non-changes

17E does not:

- modify `TextReconciliationDecision`;
- add `Merged` origin;
- add fuzzy comparison;
- add edit-distance authority thresholds;
- add spell correction;
- use OCR confidence to select text;
- use an LLM;
- automatically find native/OCR pairs;
- perform cross-page reconciliation.

## Acceptance

The same real regression must pass in:

1. implementation worktree;
2. second exact-baseline worktree after patch application;
3. main after applying the same validated patch.

No commit is created automatically.

## Next

Phase 18 should exercise the complete hybrid path: raster-only recovery,
native/OCR mixed pages, visual preservation, captions, reading order, duplicate
avoidance, provenance, cross-page continuity, and born-digital regression.
