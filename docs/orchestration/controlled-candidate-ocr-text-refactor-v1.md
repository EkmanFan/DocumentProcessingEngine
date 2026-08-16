# Phase H.4D.2A — Shared targeted-text execution refactor

## Status

Candidate refactor only. No controlled OCR-backed candidate execution is
enabled by this increment.

## Purpose

H.4D.1 established controlled `NativeText` execution while leaving all
OCR-backed candidate modes deferred.

The two legacy hybrid executors currently contain duplicate targeted-text
mechanics:

```text
MissingNativeHybridPageExecutor
  targeted OCR
  missing-native reconciliation

NativePresentHybridPageExecutor
  target-centric native/layout pairing
  targeted OCR
  native/OCR reconciliation
```

H.4D.2B will need those same mechanics for controlled candidate execution.

Duplicating them in a new candidate runner would create two implementations of
OCR target planning, pairing, ambiguity handling, and reconciliation. H.4D.2A
therefore extracts the existing mechanics first.

## New internal primitive

```text
TargetedHybridTextExecutor
```

It owns only:

```text
OCR target planning
target-centric native/layout pairing
ambiguous ownership fail-closed check
targeted region rasterization
OCR recognition
OCR/source-observation identity validation
missing-native reconciliation
native-present reconciliation
```

It does **not** own:

```text
page-level routing
full-page rasterization
layout execution
visual preservation
deferred visual evidence
page assembly
DocumentProcessor orchestration
candidate authority
```

The type is internal to `DocumentProcessing.Engine`.

## Exact ordering preservation

Failure ordering is deliberately preserved.

Missing-native route remains:

```text
request validation
caller cancellation check
full-page raster
page-raster validation
layout
layout validation
OCR target planning
visual target planning
visual-destination requirement
ordered region execution
```

Native-present route remains:

```text
request validation
caller cancellation check
full-page raster
page-raster validation
layout
layout validation
target-centric native/layout pairing
ambiguous ownership fail-closed check
OCR target planning
visual target planning
visual-destination requirement
ordered region execution
```

In particular, native/layout ambiguity still fails **before OCR target
execution/recognition**.

## Authority and visual boundary

No change is made to:

```text
DocumentProcessor
DocumentControlledCandidateTextExecutionRunner
H.4C shadow planning
H.4D.1 authority boundary
VisualAssetPreserver ownership
```

Visual preservation remains in the two legacy page executors.

H.4D.2A does not execute candidate OCR.

## Regression expectations

Existing public constructor signatures and page-executor APIs remain unchanged.

The refactor must preserve:

```text
Missing
  -> targeted OCR recovery

Suspicious / Unverified legacy reconciliation
  -> target-centric pairing
  -> ambiguous ownership fails closed before OCR
  -> targeted OCR
  -> deterministic native/OCR reconciliation

Figure
  -> visual preservation path
  -> never OCR merely because raster
```

## Next

Only after this refactor is validated:

```text
H.4D.2B
  controlled OCR-backed candidate text execution

H.4D.3
  independent candidate visual execution
```

## H.4D.2A acceptance — exact behavior parity

Status:

```text
H.4D.2A    ACCEPTED
H.4D.2B    NEXT
```

The refactor was compared directly against the exact detached baseline:

```text
078a2626cf1c522ea126bb48ba4ccc0fa2c886a5
```

using the same harness code against both assemblies.

The Habermas real corpus supplied the physical pages used by the probes:

```text
page 34    missing-native recovery
page 70    native-present reconciliation / ambiguity
```

External layout/OCR ML was not invoked. Deterministic fake layout/OCR components
were used so the comparison isolates the engine behavior moved by this
refactor.

### Exact parity results

| Scenario | Baseline outcome | Candidate parity |
|---|---|---|
| missing-native recovery | success | exact |
| native-present comparable reconciliation | success | exact |
| native/layout ambiguous ownership | failure | exact |

The missing-native trace remained:

```text
raster-page:34 > layout:34:1000x1000 > raster-region:34:100,100,800,200 > ocr:0:100,100,800,200 > raster-dispose
```

The comparable native-present trace remained:

```text
raster-page:70 > layout:70:1000x1000 > raster-region:70:120,304,32,24 > ocr:0:120,304,32,24 > raster-dispose
```

The ambiguity trace remained:

```text
raster-page:70 > layout:70:1000x1000 > ocr-call-count:0 > raster-dispose
```

The ambiguity failure remains:

```text
System.IO.InvalidDataException
Native/layout pairing for physical page 70, layout observation 0 has ambiguous native word ownership. Hybrid reconciliation fails closed before OCR authority selection.
```

and the trace proves that OCR recognition was never called:

```text
ocr-call-count:0
```

Therefore the fail-closed native/layout ambiguity ordering is unchanged.

### Regression evidence

The accepted candidate passed:

```text
Release -warnaserror          PASS
focused hybrid tests          10 / 10
full regression              506 / 506
baseline/candidate parity     EXACT
external ML invoked           NO
candidate OCR enabled         NO
authority changed             NO
```

### Acceptance rationale

H.4D.2A is accepted as a pure refactor because:

```text
targeted OCR mechanics are shared
+
native/layout pairing is shared
+
reconciliation is shared
+
legacy page-executor behavior is exact
+
failure ordering is exact
+
visual preservation ownership is unchanged
+
DocumentProcessor is unchanged
+
H.4D.1 controlled authority boundary is unchanged
=
ACCEPT
```

H.4D.2A does not execute controlled candidate OCR. That belongs to H.4D.2B.
