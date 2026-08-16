# Phase H.4D.1 — Controlled candidate NativeText execution

## Decision

H.4D is implemented incrementally.

The first increment executes only the candidate text mode that already maps to
a deterministic, ML-free engine mechanism:

```text
TextExecutionMode.NativeText
```

OCR-backed candidate text modes remain deferred:

```text
TargetedOcrRecovery
TargetedOcrVerification
TargetedOcrReconciliation
```

All candidate visual actions also remain deferred in H.4D.1.

This is intentional. H.4A visual observations identify source visual indexes
and structural evidence, but they do not retain source visual bytes. Existing
visual preservation is layout-region based. Executing the visual axis now
would require introducing a new source-visual access/preservation contract
before its semantics have been proven.

## Authority boundary

The processor order is:

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
H.4D.1 controlled candidate NativeText execution
        |
        v
comparison report only
        |
        v
return the already-built authoritative result
```

The candidate report is never consumed to select authoritative routing or
output.

## Opt-in

H.4D.1 is disabled unless
`DocumentControlledCandidateTextExecutionDependencies` is explicitly supplied.

Controlled execution cannot be configured without H.4C shadow planning. The
constructor fails fast on that invalid composition.

## Failure semantics

```text
shadow planning unavailable
    -> candidate execution skipped
    -> authoritative result unchanged

ordinary candidate execution failure
    -> Failed candidate report
    -> authoritative result unchanged

ordinary candidate observer failure
    -> best effort / isolated
    -> authoritative result unchanged

caller cancellation
    -> propagates

OutOfMemoryException
    -> propagates
```

## Candidate execution semantics

For `NativeText` pages H.4D.1 uses the same deterministic native-page assembly
mechanism as the legacy `NativeOnly` route.

The runner receives no rasterizer, layout analyzer, OCR recognizer,
reconciler, or visual preserver. Therefore H.4D.1 cannot accidentally invoke
those capabilities.

Per executed page it records:

- authoritative legacy route;
- candidate text mode;
- whether the candidate removes legacy text ML;
- whether independent visual work remains pending;
- exact selected-text sequence agreement;
- stronger text-projection agreement;
- authoritative/candidate text element counts;
- authoritative/candidate reconciliation-evidence counts.

## Explicitly deferred work

H.4D.1 does **not** claim completion of H.4D.

Next increments:

```text
H.4D.2
  controlled OCR-backed candidate text execution

H.4D.3
  independent candidate visual execution

H.4D.4
  full candidate execution comparison / guarded cutover evidence
```

The full real-corpus routing/performance rebase belongs after the concrete OCR
and visual execution increments exist.

## Regression law

The existing invariants remain authoritative:

```text
Missing
  -> OCR recovery

Corrupted / Suspicious
  -> reconciliation

Trusted + presentation-only
  -> no layout/OCR

Trusted + meaningful visual
  -> preserve
  -> no OCR

Trusted + Unknown
  -> visual analysis
  -> no OCR

Unknown
  -> fail closed

Figure
  -> never OCR merely because raster
```

## H.4D.1 acceptance — real-corpus validation

Status:

```text
H.4D.1    ACCEPTED
H.4D.2    NEXT
```

The H.4D.1 candidate was validated against the three pinned real corpora without
invoking external ML.

### Correctness

| Corpus | Pages | Candidate NativeText | Deferred OCR-backed text | Native execution projection exact |
|---|---:|---:|---:|---:|
| Ehrman | 617 | 211 | 406 | 211/211 |
| De Decretis | 1479 | 1477 | 2 | 1477/1477 |
| Habermas | 170 | 159 | 11 | 159/159 |

For De Decretis and Habermas, every candidate `NativeText` page is also a
legacy `NativeOnly` page, so the controlled runner compared against the genuine
deterministic authoritative legacy page:

```text
De Decretis
  executed NativeText       1477
  deferred non-native text     2
  exact selected text       1477 / 1477
  exact text projection     1477 / 1477

Habermas
  executed NativeText        159
  deferred non-native text    11
  exact selected text        159 / 159
  exact text projection      159 / 159
```

For Ehrman, H.4C identifies:

```text
candidate NativeText          211
candidate OCR                 406
candidate removes legacy ML   211
```

Those 211 candidate-NativeText pages are legacy hybrid pages. This no-ML proof
therefore executes the candidate native assembly and proves exact equality with
the native extraction projection, but deliberately does **not** fabricate a
legacy hybrid authoritative result.

Consequently H.4D.1 does **not** claim Ehrman hybrid-authority equivalence.
That comparison belongs to a controlled ML-backed execution increment.

### Incremental execution overhead

H.4D.1's own deterministic execution cost was characterized after warmup:

| Corpus | Median H.4D.1 execution time | Median managed allocation |
|---|---:|---:|
| Ehrman | 0.854 ms | 1.223 MiB |
| De Decretis | 16.017 ms | 15.723 MiB |
| Habermas | 0.692 ms | 1.851 MiB |

These values are characterization evidence, not production SLAs. The added
deterministic candidate-NativeText execution is small relative to the existing
document-processing workload and introduces no observed correctness regression.

### Regression evidence

The accepted candidate passed:

```text
Release -warnaserror          PASS
focused H.4D.1 tests          9 / 9
full regression              505 / 505
real-corpus proof             PASS
external ML invoked           NO
legacy authority changed      NO
```

### Acceptance rationale

H.4D.1 is accepted because it establishes a real controlled-execution boundary
without transferring authority:

```text
candidate NativeText actually executes
+
legacy authoritative output remains authoritative
+
real-corpus candidate-native projection is exact
+
genuine legacy NativeOnly comparison is exact where available
+
ordinary candidate failures remain isolated
+
cancellation / OOM remain fatal
+
incremental overhead is characterized
=
ACCEPT
```

OCR-backed candidate text execution and all independent visual execution remain
outside H.4D.1.
