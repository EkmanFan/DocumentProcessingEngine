# Authoritative two-phase semantic reference V1

## Status

**PASS — frozen reference**

Implementation commit:

```text
604bf4063cf868b6d229d02359e36d6e4849ff15
```

Public entry point:

```text
DocumentProcessor.ProcessAsync(...)
```

This document freezes the accepted semantic behavior of the current
Authoritative two-phase PDF processing path.

## Frozen architecture

The accepted Authoritative execution shape is:

```text
PHASE 1 — layout

for every layout-backed page:
  full-page raster
  -> PP-StructureV3
  -> compact layout evidence
  -> disk spool
  -> release full-page raster bytes

PHASE 2 — semantic execution

for every prepared page:
  reload compact spool entry
  -> execute with precomputed layout
  -> targeted region rendering only where required
  -> PaddleOCR where policy permits
  -> reconciliation / visual preservation
```

Plain `NativeOnly` pages that require no visual analysis bypass
raster/layout/OCR.

The spool is a deliberate disk boundary. Full-page raster bytes are not
persisted in it.

## Acceptance evidence

### Permanent semantic suite — Stage 1

```text
native/provenance                 PASS
layout semantic controls          7/7 PASS
real PP-StructureV3 + PaddleOCR   PASS
Figure OCR                        0
deterministic regression before  643/643 PASS
deterministic regression after   643/643 PASS
```

### Public DocumentProcessor — Stage 2

All fixed controls were re-executed through
`DocumentProcessor.ProcessAsync(...)`.

```text
De Decretis representative  Healthy / NativeOnly / 612 words
Habermas p40                Healthy / NativeOnly / meaningful visual / OCR 0
Habermas p43                Healthy / NativeOnly / meaningful visual / OCR 0
Habermas p44                Healthy / NativeOnly / meaningful visual / OCR 0

Ehrman p233
  Missing
  -> LayoutWithTargetedOcrRecovery
  -> 7 OCR calls
  -> Figure OCR 0
  -> exact meaningful visual preserved
  -> reading order PASS

Ehrman p380
  Unverified
  -> LayoutWithTargetedOcrReconciliation
  -> 9 OCR calls
  -> Conflict / None
  -> unresolved
  -> divergence true
  -> native block 2

Ehrman p405
  Unverified
  -> LayoutWithTargetedOcrReconciliation
  -> 8 OCR calls
  -> Agreement / NativePdf
  -> resolved
  -> divergence false
  -> native block 6

post-Stage-2 deterministic regression  643/643 PASS
Figure OCR                           0
```

Exact visual byte-oracles remain intentionally pinned for Habermas p40/p43 and
Ehrman p233. Habermas p44 is frozen semantically as one preserved meaningful
visual, but its crop bytes are not promoted here to an exact permanent oracle.

## What is now the reference

Commit `604bf4063cf868b6d229d02359e36d6e4849ff15` is the implementation reference for:

```text
Authoritative two-phase logical execution
layout-before-semantic-execution ordering
compact disk spool boundary
precomputed-layout execution
targeted region OCR
native authority semantics
reconciliation semantics
meaningful visual preservation
reading order on accepted controls
Figure OCR == 0
```

Future optimization work must preserve this reference behavior unless an
explicitly reviewed semantic change intentionally supersedes it.

## Explicitly not frozen as complete

This checkpoint does **not** claim that the production engine already owns the
physical lifecycle of PP-StructureV3 and PaddleOCR.

The following remain separate follow-up work:

```text
automatic PP start/stop lifecycle
automatic PaddleOCR start/stop lifecycle
PP-once / Paddle-once proof per document
fresh-process memory envelope
wall-clock performance acceptance
```

Those concerns must not be retroactively treated as prerequisites for the
semantic two-phase reference frozen here.

## Evidence integrity

The permanent JSON companion records SHA-256 values of the exact Stage 2
evaluation reports used to create this reference.

Stage 1 evidence was also pinned before freezing:

```text
permanent semantic log
9fe8523f1359b44a0276827d00eba71f044c1fcab22017e69ff6d08ee44739d4

pre-live deterministic regression
26521183169e52b0decb978ed8ff5272a901800e6dfa2a45a97b222d2f33084d

post-live deterministic regression
1e22fb3dd43727c785825de6029139ec3e019c8194e269dd02c3b31af0119277
```
