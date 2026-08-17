# Document Processing Engine — Phase 15.2 Semantic Regression Automation

**Date:** 2026-08-17
**Canonical phase:** 15 — Semantic regression
**Increment:** 15.2 — automate semantic regression suite
**Status:** COMPLETE
**Baseline:** `62fc51e`

## 1. Decision

The semantic regression suite will be a **separate explicit evaluation suite**, not part of the normal fast `dotnet test` path.

Reason:

- the real suite depends on local-only PDF fixtures;
- PP-StructureV3 and PaddleOCR are external ML dependencies;
- model startup is expensive;
- model results are evaluation evidence, not ordinary unit-test dependencies;
- Phase 16, not Phase 15, owns performance acceptance.

Normal unit tests remain fast and deterministic.

The live semantic suite must fail loudly when its required fixtures or services are unavailable. It must never silently convert “not executed” into PASS.

## 2. Automation layers

```text
Layer 1 — deterministic contract regression
  no ML
  fast
  normal dotnet tests
  examples:
    Figure is not a text candidate
    unknown/future kind fails closed
    caption is a text candidate
    reconciliation policy invariants

Layer 2 — fixture/provenance/native regression
  local PDF fixtures
  no ML
  explicit semantic-regression runner
  examples:
    67 fixture lineage checks
    Habermas p70/p78/p79 Healthy/NativeOnly
    De Decretis p512..p561 native baseline

Layer 3 — live layout semantic regression
  PP-StructureV3
  explicit runner
  examples:
    p79/p233/p40/p43 positive captioned visual controls
    p148/p331 negative decorative controls
    p34/p36/p38/p44 known red meaningful-visual controls

Layer 4 — live OCR/reconciliation regression
  PP-StructureV3 then PaddleOCR
  never concurrent on the reference machine
  examples:
    p233 targeted OCR + exact visual custody + reading order
    p380 Conflict/None/unresolved
    p405 Agreement/NativePdf/resolved
    Figure OCR = 0
```

## 3. Machine-readable oracle

The suite source of truth is:

```text
docs/evaluation/semantic-regression-ground-truth-v1.json
```

The oracle records expected semantics, not current-code snapshots.

Current baseline classifications are included only to document the starting state of Phase 15.2.

## 4. Red-first requirement

The first automated live suite must reproduce the four known failures before any remediation:

```text
Habermas p34  expected PreserveMeaningfulVisual, observed Unknown
Habermas p36  expected PreserveMeaningfulVisual, observed Unknown
Habermas p38  expected PreserveMeaningfulVisual, observed Unknown
Habermas p44  expected PreserveMeaningfulVisual, observed Unknown
```

If the initial automation reports these as PASS without a production change, the automation is wrong.

This is the core anti-self-deception gate for 15.2.

## 5. Passing controls that must remain green

At minimum:

```text
Ehrman p233
  Missing → targeted OCR recovery
  exact papyrus crop/hash
  Figure OCR = 0
  Imagine → for example

Ehrman p380
  Conflict
  selected origin None
  unresolved
  divergence true

Ehrman p405
  Agreement
  NativePdf
  resolved
  divergence false
  Figure OCR = 0

Habermas p40 / p43
  exact meaningful visual crop/hash

Habermas p70 / p78 / p79
  Healthy / NativeOnly

De Decretis p512..p561
  50/50 Healthy / NativeOnly
  29,044 words
  269 blocks

Fixture provenance
  67/67
```

## 6. Implementation boundary

15.2 may add or modify only evaluation/test/support code and evaluation documentation unless a demonstrated defect prevents automation.

Do not:

- change production semantic policy;
- tune thresholds;
- add new model backends;
- rebuild CandidateVisualExecution;
- add a plugin system;
- optimize memory/performance.

Those belong to later work.

## 7. Intended permanent artifacts

The preferred permanent shape is:

```text
docs/evaluation/
  phase-15-1-semantic-regression-matrix.md
  semantic-regression-ground-truth-v1.json

tools/DocumentProcessing.EvaluationCli/
  semantic-regression observation/evaluation command(s)

scripts/
  run-semantic-regression.sh

tests/
  deterministic semantic evaluator / policy contract tests
```

The exact C# split must follow the current repository structure and should be implemented only after inspecting the current CLI/test seams at the exact baseline.

## 8. Exit criteria for 15.2

- machine-readable oracle is versioned;
- deterministic semantic invariants are automated;
- local fixture/provenance/native controls are automated;
- live PP controls are automated;
- live PP + PaddleOCR reconciliation controls are automated;
- four known red Habermas controls reproduce as FAIL;
- passing controls remain green;
- missing prerequisites fail explicitly, never silently PASS;
- live suite leaves the repository unchanged;
- model residency remains bounded and sequential where required;
- normal `dotnet test` remains fast and does not require ML services.

Only then move to:

```text
15.3 — remediate demonstrated semantic gaps
```

## 9. Current implementation checkpoint

The first permanent 15.2 slice is the **live layout semantic regression runner**.

Permanent entry points:

```text
DocumentProcessing.EvaluationCli
  evaluate-semantic-layout-regression

scripts/run-semantic-layout-regression.sh
```

The runner evaluates the meaningful-visual controls from the versioned oracle against live PP-StructureV3 evidence and deterministic regional preservation.

Initial baseline expectation:

```text
green:
  Ehrman p233
  Habermas p40
  Habermas p43

red:
  Habermas p34
  Habermas p36
  Habermas p38
  Habermas p44
```

`--mode baseline` succeeds only when the live suite reproduces those independent Phase-15.1 classifications.

`--mode all-pass` succeeds only when every evaluated layout semantic control satisfies the ground truth. It is intentionally expected to fail before remediation.

## 10. No-ML native/provenance automation checkpoint

The second permanent 15.2 slice adds:

```text
DocumentProcessing.EvaluationCli
  evaluate-semantic-native-regression

scripts/run-semantic-native-regression.sh
```

This evaluator has no Docker or ML dependency. It validates the machine-readable oracle against the local page fixtures and manifest:

```text
fixture provenance
  67/67 rows
  corpus counts
  original physical page encoded by filename + manifest
  standalone fixture PhysicalPageNumber = 1
  source SHA pinned per corpus
  fixture SHA/byte length

native-first routing
  Habermas p70/p78/p79
    Healthy / NativeOnly

  De Decretis p512..p561
    50/50 Healthy / NativeOnly
    29,044 words
    269 blocks
```

This runner remains explicit rather than part of normal `dotnet test`, because the PDF fixtures are local-only evaluation assets.

## 11. Real PP + PaddleOCR semantic automation checkpoint

The third permanent 15.2 slice adds:

```text
DocumentProcessing.EvaluationCli
  evaluate-semantic-ocr-regression

scripts/run-semantic-ocr-regression.sh
```

The runner executes only the three Phase-15.1 green controls that require real OCR/reconciliation:

```text
Ehrman p233
  Missing -> LayoutWithTargetedOcrRecovery
  real targeted OCR
  Figure OCR = 0
  exact papyrus custody
  "Imagine" before "for example"

Ehrman p380
  LayoutWithTargetedOcrReconciliation
  target seq5
  Conflict / None / unresolved / divergence
  native block 2
  Figure OCR = 0

Ehrman p405
  LayoutWithTargetedOcrReconciliation
  target seq9
  Agreement / NativePdf / resolved / no divergence
  native block 6
  Figure OCR = 0
```

PP-StructureV3 and PaddleOCR are never resident concurrently on the reference machine. The CLI owns semantic evaluation only; the shell runner owns model lifecycle and the layout-to-OCR handoff.

## 12. Final Phase 15.2 acceptance

Phase 15.2 has a permanent aggregate entry point:

```text
scripts/run-semantic-regression.sh
```

For the pre-remediation baseline:

```bash
scripts/run-semantic-regression.sh --layout-mode baseline
```

For post-remediation semantic acceptance:

```bash
scripts/run-semantic-regression.sh --layout-mode all-pass
```

Final acceptance requires all of the following:

```text
normal Release build                           PASS
normal dotnet regression                       PASS
deterministic text/visual contract tests       PASS

native/provenance suite
  fixture provenance                           67/67 PASS
  Habermas p70/p78/p79                         3/3 Healthy / NativeOnly
  De Decretis p512..p561                       50/50 Healthy / NativeOnly
  De Decretis                                  29,044 words / 269 blocks

live layout suite, baseline mode
  controls                                     7
  semantic PASS                                3
  semantic FAIL                                4
  baseline mismatch                            0
  exact red set                                Habermas p34/p36/p38/p44

real PP + PaddleOCR suite
  Ehrman p233                                  PASS
  Ehrman p380                                  PASS
  Ehrman p405                                  PASS
  Figure OCR                                   0

model residency
  PP-StructureV3 and PaddleOCR                 never concurrent

repository mutation by evaluation runners
  permanent candidate files                    unchanged
  reports/logs                                 scripts/tmp only
```

The deterministic contract coverage remains in normal unit tests rather than being duplicated in the real-corpus runners. In particular:

```text
LayoutTextPolicy
  Text / Heading / Caption / Table -> text candidate
  Figure / Unknown -> not text candidate
  undefined future enum -> fail closed

DefaultLayoutVisualEvidenceAssessor
  strong single caption -> CaptionedMeaningfulVisual
  no caption -> Unknown
  ambiguous/multiple/distant caption -> Unknown
```

Phase 15.2 therefore automates the independently established Phase-15.1 oracle without changing production semantic policy.

## 13. Phase transition

```text
15.1 semantic regression matrix
DONE

15.2 automate semantic regression suite
DONE

15.3 remediate demonstrated semantic gaps
NEXT

Known red controls entering 15.3:
  Habermas p34
  Habermas p36
  Habermas p38
  Habermas p44
```

The first 15.3 change must be justified by these demonstrated failures and must preserve all green controls.
