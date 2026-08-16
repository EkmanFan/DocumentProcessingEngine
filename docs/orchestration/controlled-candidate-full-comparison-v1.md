# H.4D.4A — Cross-axis candidate comparison and explicit cutover blockers

## Status

```text
H.4D.2B    DONE
H.4D.3A    DONE
H.4D.3B    DONE
H.4D.4     ACTIVE

H.4D.4A    ACCEPTED
H.4D.4B    ACTIVE
H.4D.4B.1  ACCEPTED
H.4D.4B.2  NEXT
```

H.4D.4 is deliberately split into two bounded increments.

```text
H.4D.4A
    cross-axis execution comparison
    explicit cutover-blocker model
    legacy authority retained

H.4D.4B
    candidate portable output projection
    candidate provenance/quality projection
    candidate vs authoritative result comparison
    guarded cutover evidence
```

This split is required by the current architecture, not by a desire to add
layers. H.4D.2B currently compares text projections but discards candidate
`HybridDocumentPage` instances. H.4D.3B preserves source visual occurrences to
a shadow sink and retains neutral materialization metadata, but does not create
portable result elements or caller-owned persisted candidate assets.

Pretending that those reports already constitute a candidate
`DocumentIngestionResult` would fabricate output/provenance evidence.

## Position in orchestration

```text
H.4C planning
    ↓
legacy authoritative execution
    ↓
authoritative DocumentIngestionResult BUILT
    ↓
H.4D.2B controlled text execution
    ↓
H.4D.3B controlled visual execution
    ↓
H.4D.4A cross-axis comparison
    ↓
comparison report only
    ↓
return already-built authoritative result
```

The H.4D.4A runner has no extraction, raster, layout, OCR, reconciliation,
preservation or format-specific dependency.

Its only runtime input is already-produced evidence plus the authoritative
result.

## Comparison invariants

H.4D.4A requires exact custody and coverage:

```text
authoritative source SHA
    == H.4C source SHA
    == H.4D.2B source SHA
    == H.4D.3B source SHA

authoritative page count
    == H.4C page count
    == H.4D.2B page count
    == H.4D.3B page count

physical page identity exact

authoritative legacy route
    == text report legacy route
    == visual report legacy route

H.4C candidate text mode
    == H.4D.2B executed/deferred text mode

H.4C visual source indexes/actions
    == H.4D.3B executed source indexes/actions
```

Cross-report inconsistency is a comparison failure. It never transfers
authority.

## Page evidence

For every fully comparable page the report records:

```text
authoritative legacy route
candidate text mode
candidate text execution status
selected-text exactness
text-projection exactness
candidate visual actions
visual plan/execution exactness
candidate removes legacy text ML
candidate adds independent visual work to legacy-native page
```

This provides one place to query the complete H.4D execution intent/result
without merging execution responsibilities.

## Explicit cutover blockers

The blocker model is evidence-based.

Current blockers include:

```text
TextExecutionUnavailable
VisualExecutionUnavailable
TextExecutionIncomplete
SelectedTextSequenceDivergence
TextProjectionDivergence
CandidateVisualPersistenceNotCompared
PortableOutputNotCompared
ProvenanceNotCompared
```

The important H.4D.4A rule is:

```text
PortableOutputNotCompared
+
ProvenanceNotCompared
=
ReadyForGuardedCutover == false
```

Those two blockers are added deliberately to every otherwise-complete H.4D.4A
comparison.

Therefore H.4D.4A cannot accidentally authorize a candidate cutover.

## Why visual persistence remains a distinct blocker

`PreserveMeaningfulVisual` has now proven exact source-asset materialization.

However the controlled path still uses:

```text
Stream.Null
```

That proves candidate source visual custody and execution mechanics, but not
the final caller-owned persistence/output contract.

When preservation occurs, H.4D.4A therefore also reports:

```text
CandidateVisualPersistenceNotCompared
```

No semantic/layout provenance is invented merely to force that source asset
into the legacy portable result model.

## Failure semantics

```text
H.4C planning unavailable
    -> PlanningUnavailable comparison
    -> no authority change

text or visual candidate execution unavailable
    -> CandidateExecutionUnavailable
    -> explicit blocker(s)
    -> no authority change

ordinary comparison inconsistency/failure
    -> Failed comparison report
    -> no authority change

ordinary comparison observer failure
    -> best effort

caller cancellation
    -> propagate

OutOfMemoryException
    -> propagate
```

## H.4D.4B entry criteria

H.4D.4B should begin only after H.4D.4A passes deterministic regression and
real-corpus comparison evidence.

H.4D.4B must then answer the questions H.4D.4A intentionally refuses to fake:

```text
What is the complete candidate HybridDocumentPage stream?

How are candidate source-preserved visual assets represented without
inventing layout semantics?

What caller-owned persistence boundary is used for those assets?

Does candidate normalization/segmentation remain deterministic?

Does candidate DocumentIngestionResult preserve source/page/element/segment
custody?

Which output/provenance differences are intentional versus regressions?

Only then:
    can PortableOutputNotCompared be cleared
    can ProvenanceNotCompared be cleared
    can guarded cutover readiness be evaluated
```
## H.4D.4A acceptance evidence

H.4D.4A is accepted as a non-authoritative cross-axis comparison capability.

Deterministic evidence:

```text
Release -warnaserror        PASS
focused H.4D.4A tests       9 / 9
complete regression         543 / 543
```

Integrated real-corpus evidence:

| Corpus | Physical page | Candidate text | Candidate visual action | Text exact | Projection exact |
|---|---:|---|---|---|---|
| Habermas | 40 | `NativeText` | `PreserveMeaningfulVisual` | yes | yes |
| Habermas | 43 | `NativeText` | `PreserveMeaningfulVisual` | yes | yes |
| Habermas | 44 | `NativeText` | `PreserveMeaningfulVisual` | yes | yes |
| Ehrman | 36 | `TargetedOcrReconciliation` | `NoAdditionalSemanticProcessing` | yes | yes |
| Ehrman | 148 | `TargetedOcrVerification` | `AnalyzeVisual` | yes | yes |
| Ehrman | 233 | `TargetedOcrRecovery` | `AnalyzeVisual` | yes | yes |

The three Habermas controls preserve the exact source JPEG while the
authoritative route remains `NativeOnly`; the preservation path performs no
layout, raster or OCR work. Their returned authoritative result is serialized
equivalent to the candidate-disabled baseline.

The three Ehrman controls execute the controlled text and visual axes together
inside `DocumentProcessor`. Selected text and text projection are exact for all
three controls. Ehrman page 233 preserves Figure safety with zero Figure OCR
calls. PP-StructureV3 and PaddleOCR are kept sequentially resident during the
live proof.

The cross-axis comparison remains deliberately non-authoritative:

```text
PortableOutputNotCompared
ProvenanceNotCompared
```

remain present on all six controls, and
`CandidateVisualPersistenceNotCompared` additionally remains present on the
three `PreserveMeaningfulVisual` controls.

Therefore:

```text
ReadyForGuardedCutover = false
authority transfer     = no
performance acceptance = none
```

H.4D.4B is the next increment. It owns candidate portable output and provenance
projection. Only evidence from H.4D.4B may clear the remaining cutover blockers.
