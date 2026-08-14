# Native/OCR reconciliation production boundary V1

## Status

Phase B / 17A production-boundary increment.

This increment establishes the smallest neutral reconciliation model and a
fully deterministic V1 policy for explicitly paired native PDF text and OCR
text evidence.

It intentionally stops before claiming real-corpus native/OCR reconciliation.

```text
17 Native/OCR reconciliation
   17A neutral model + deterministic policy   THIS INCREMENT
   17B real reconciliation evaluation         NEXT
```

## Why reconciliation is a separate boundary

Native extraction and OCR are independent evidence sources.

Neither should silently overwrite the other:

```text
native PDF evidence ─┐
                     ├─ reconciliation
OCR evidence ────────┘
```

The handoff direction remains:

```text
healthy native text
    -> prefer native

missing native text
    -> use OCR when recovered

suspicious native text
    -> require OCR as secondary evidence
    -> compare
    -> make disagreement explicit
```

Phase 17A turns that direction into a typed deterministic boundary.

## Explicit input status

`NativeTextStatus` contains:

```text
Missing
Healthy
Suspicious
```

The reconciler does not infer this status from text length, OCR confidence, or
an LLM.

The caller supplies it from deterministic preflight/page/region evidence.

The model enforces consistency:

- `Missing` cannot carry a native block;
- `Healthy` and `Suspicious` require a native block;
- OCR evidence must belong to the same physical page;
- when native and OCR evidence are paired, their regions must have a positive
  spatial intersection.

Automatic pairing/matching is deliberately not introduced in 17A.

## Deterministic V1 decisions

`TextReconciliationDecision` records the outcome explicitly:

```text
NativeOnly
OcrOnly
Agreement
HealthyNativePreferred
SuspiciousNativeUnverified
Conflict
NoTextRecovered
```

The policy is:

| Native status | Usable OCR | Comparison | Decision | Selected text |
|---|---|---|---|---|
| Healthy | no | n/a | `NativeOnly` | native |
| Missing | yes | n/a | `OcrOnly` | OCR |
| Missing | no | n/a | `NoTextRecovered` | none |
| Suspicious | no | n/a | `SuspiciousNativeUnverified` | none |
| Healthy | yes | agree | `Agreement` | native |
| Suspicious | yes | agree | `Agreement` | native |
| Healthy | yes | differ | `HealthyNativePreferred` | native |
| Suspicious | yes | differ | `Conflict` | none |

A healthy-native/OCR disagreement is not hidden: the selected text remains
native, but the decision and `HasDivergence` explicitly record the conflict.

A suspicious-native/OCR disagreement remains unresolved. V1 does **not** guess.

## Selection origin

`TextSelectionOrigin` contains only:

```text
None
NativePdf
Ocr
```

There is deliberately no `Merged` value in 17A because the implementation does
not synthesize a third text from two conflicting strings.

If later evidence justifies real merging, that behavior must be introduced and
evaluated explicitly rather than implied by an enum value.

## Conservative comparison

V1 intentionally avoids a fuzzy similarity threshold.

Before equality comparison it performs only:

- Unicode compatibility normalization (`FormKC`);
- removal of discretionary soft hyphen (`U+00AD`);
- whitespace collapsing.

It does **not** normalize away:

- case differences;
- punctuation differences;
- spelling differences;
- word substitutions;
- missing clauses.

Therefore a false conflict is preferred over a false claim that two sources
agree.

Real-corpus Phase 17B evidence should determine whether any additional
normalization is justified.

## OCR confidence

OCR confidence remains accessible through the retained `OcrRegionResult`, but
V1 does not use an arbitrary confidence threshold to choose between native and
OCR text.

A threshold such as 0.5, 0.7, or 0.9 would be policy without evidence at this
stage.

## Provenance retention

`TextReconciliationResult` retains the complete `TextReconciliationInput`, so
callers can still reach:

- the original `DocumentTextBlock`;
- the original `OcrRegionResult`;
- OCR backend/profile/confidence;
- native typography/word evidence;
- physical page context;
- native and OCR geometry.

The result additionally exposes:

- deterministic decision;
- selected origin;
- selected text, when resolved;
- composed OCR text used by comparison;
- whether the two texts were conservatively equivalent;
- whether a divergence exists;
- whether the candidate is resolved.

## Deliberate non-decisions

Phase 17A does **not** add:

- automatic native/OCR spatial pairing;
- edit-distance or semantic similarity thresholds;
- LLM arbitration;
- OCR-confidence arbitration;
- word-by-word text synthesis;
- cross-page continuity;
- final normalization/segmentation integration;
- page/document orchestration;
- ApologiaStudio semantics;
- `DocumentIngestionResult`.

## Next step — Phase 17B

Evaluate this boundary using real native and OCR evidence from the pinned
corpus.

The evaluation should include at least three classes:

```text
healthy native + OCR secondary evidence
missing native + OCR recovery
suspicious native + OCR disagreement/agreement
```

The objective is not to manufacture a PASS for every candidate. A real
`Conflict` or `SuspiciousNativeUnverified` result is valid evidence if the
inputs genuinely disagree or remain insufficient.

Only after real evaluation should Phase 17 be marked DONE or the conservative
policy be broadened.
