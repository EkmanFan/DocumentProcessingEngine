# Phase H.4D.2B — Controlled OCR-backed candidate text execution

## Status

Candidate implementation only. Legacy execution remains authoritative.

H.4D.2B extends the H.4D.1 controlled text seam to the three OCR-backed text
modes:

```text
TargetedOcrRecovery
TargetedOcrVerification
TargetedOcrReconciliation
```

No candidate visual execution is introduced.

## Authority boundary

The runtime order remains:

```text
H.4C candidate planning
        |
        v
legacy authoritative execution
        |
        v
authoritative DocumentIngestionResult built
        |
        v
controlled candidate text execution
        |
        v
comparison report only
        |
        v
return the already-built authoritative result
```

Candidate output never feeds normalization, segmentation, provenance, quality
projection, or the returned `DocumentIngestionResult`.

## Explicit opt-in and backward compatibility

The H.4D.1 constructor remains valid:

```text
DocumentControlledCandidateTextExecutionDependencies(observer)
```

With that composition:

```text
NativeText          -> executed
OCR-backed modes    -> deferred
```

H.4D.2B adds an explicit capability composition:

```text
observer
IDocumentRasterizer
IPageLayoutAnalyzer
IRegionTextRecognizer
```

Only this composition enables controlled OCR-backed execution.

There is no visual destination, visual preserver, or visual-analysis service in
the controlled text dependencies.

## OCR text execution

H.4D.2B uses one candidate document-scoped raster session and a text-only page
executor.

The page executor owns:

```text
full-page raster
layout analysis
text-region iteration
```

and delegates the safety-critical text mechanics to the H.4D.2A shared
primitive:

```text
TargetedHybridTextExecutor
  OCR target planning
  target-centric native/layout pairing
  ambiguity fail-closed
  targeted crop rendering
  OCR recognition
  native/OCR reconciliation
```

Therefore the legacy and controlled candidate paths do not maintain separate
pairing/reconciliation implementations.

## Mode semantics

The three modes remain distinct.

```text
Missing
  -> TargetedOcrRecovery
  -> OCR-only recovery

Unverified
  -> TargetedOcrVerification
  -> native/OCR secondary evidence
  -> agreement may verify native
  -> disagreement remains unresolved

Suspicious
  -> TargetedOcrReconciliation
  -> conservative native/OCR reconciliation
  -> disagreement remains unresolved
```

`TargetedOcrVerification` is not collapsed into recovery and does not grant OCR
authority merely because OCR exists.

The current deterministic reconciler already distinguishes missing text from
native-present suspicious/unverified evidence. H.4D.2B reuses that mechanism.

## Visual isolation

Layout observations whose deterministic treatment is not `RecognizeText` are
ignored by the controlled text executor.

This is intentional:

```text
text execution            H.4D.2B
visual execution          H.4D.3
```

A Figure therefore does not become OCR text merely because candidate
raster/layout execution is active.

The comparison report still records whether H.3C planned independent visual
work so that the deferred visual axis remains visible.

## Candidate raster lifetime

Candidate OCR execution opens a separate document-scoped raster session after
the authoritative result has already been built.

This deliberately avoids:

```text
extending the lifetime of the legacy authoritative raster session
sharing mutable candidate/authority raster state
changing authoritative dependency resolution
```

The cost is duplicate controlled-evaluation raster work. That is acceptable
while candidate execution is diagnostic rather than authoritative.

## Failure semantics

```text
shadow planning unavailable
    -> candidate execution skipped
    -> authoritative result unchanged

ordinary candidate raster/layout/OCR/pairing/reconciliation failure
    -> Failed candidate report
    -> partial candidate page comparisons discarded
    -> authoritative result unchanged

ordinary observer failure
    -> best effort
    -> authoritative result unchanged

caller cancellation
    -> propagates

OutOfMemoryException
    -> propagates
```

Candidate source access is reset after execution so source-position state does
not escape the controlled path.

## Comparison evidence

Executed pages retain the existing comparison dimensions:

```text
selected text sequence exact
text projection exact
authoritative/candidate text element count
authoritative/candidate reconciliation evidence count
candidate removes legacy text ML
pending independent visual work
```

The page status now distinguishes:

```text
ExecutedNativeText
ExecutedTargetedOcrRecovery
ExecutedTargetedOcrVerification
ExecutedTargetedOcrReconciliation
DeferredNonNativeTextMode
```

## What H.4D.2B does not claim

This implementation does not yet establish production cutover evidence.

In particular it does not claim:

```text
real-corpus PP-StructureV3/PaddleOCR parity
acceptable controlled ML latency
acceptable controlled ML allocation/RSS cost
visual execution equivalence
permission to replace legacy authority
```

Those require real-corpus execution after the implementation passes deterministic
regression correctness.

## Next validation sequence

```text
implementation
    |
    v
unit / integration regression
    |
    v
real-corpus controlled ML execution
    |
    v
candidate vs legacy text comparison
    |
    v
performance / memory characterization
    |
    v
only then H.4D.2B acceptance
```

H.4D.3 remains frozen until H.4D.2B is accepted.

## H.4D.2B acceptance

Status:

```text
H.4D.2B    ACCEPTED
H.4D.3     NEXT
```

H.4D.2B is accepted as an **explicit controlled diagnostic/shadow capability**.
It is not accepted as the final cutover topology and is not intended to be
enabled indiscriminately on normal production processing.

### Correctness evidence

The exact candidate was validated against the pinned Ehrman corpus with live
PP-StructureV3 and live PaddleOCR:

| Physical page | Candidate text mode | Text sequence | Text projection | Legacy/candidate OCR |
|---:|---|---|---|---:|
| 36 | TargetedOcrReconciliation | exact | exact | 9/9 |
| 148 | TargetedOcrVerification | exact | exact | 9/9 |
| 233 | TargetedOcrRecovery | exact | exact | 7/7 |

Additional safety evidence:

```text
Figure -> OCR requests           0
p233 authoritative Figure seq4  preserved
candidate visual execution      disabled
authority transfer              no
Release regression              513 / 513
```

This validates all three OCR-backed candidate text modes while preserving the
H.4D.2A shared pairing/reconciliation mechanism and the H.4D.1 authority
boundary.

### Performance / memory characterization

The current controlled path deliberately owns an additional raster/layout/OCR
evaluation pass. With one warmup and three measured samples, the projected
incremental per-page shadow cost was:

| Physical page | Candidate mode | Projected incremental cost |
|---:|---|---:|
| 36 | TargetedOcrReconciliation | 10.773 s |
| 148 | TargetedOcrVerification | 11.959 s |
| 233 | TargetedOcrRecovery | 11.114 s |

Observed service-memory peaks:

```text
PP-StructureV3    10632.1 MiB
PaddleOCR         2138.0 MiB
```

PP-StructureV3 and PaddleOCR were not kept resident concurrently during these
validation runs.

### Architectural decision

The extra cost is substantial enough that H.4D.2B must remain controlled and
explicitly opt-in. It would be inappropriate to run the diagnostic dual path
indiscriminately across large documents.

However, optimizing away the duplicated ML work **before authority cutover** is
also rejected. Such an optimization would couple the authoritative and
candidate paths, extend shared resource lifetimes, or introduce caching solely
to accelerate temporary shadow evaluation. That complexity is not justified
by a diagnostic path.

The accepted rule is therefore:

```text
H.4D.2B controlled evaluation
    -> explicit opt-in
    -> sampled / targeted corpus validation
    -> duplicate work accepted temporarily
    -> no authority transfer
    -> no production throughput promise
```

Performance optimization belongs after candidate behavior has accumulated
enough evidence to justify cutover, where redundant legacy/candidate work can
be removed rather than optimized as permanent duplication.

### Acceptance conclusion

```text
three OCR-backed candidate modes executed with live ML
+
candidate/legacy text parity exact on representative controls
+
Figure never OCR'd
+
visual axis remains deferred
+
ordinary candidate failures remain fail-open
+
cancellation/OOM semantics preserved
+
513 tests pass
+
runtime cost measured and operational boundary documented
=
H.4D.2B ACCEPTED
```

H.4D.3 may now proceed with independent candidate visual execution.
