# Document Processing Engine — Phase 15.1 Semantic Regression Matrix

**Date:** 2026-08-17
**Canonical phase:** 15 — Semantic regression
**Increment:** 15.1 — Define the real-corpus semantic regression matrix
**Baseline observed:** `62fc51e` — `refactor: remove obsolete candidate visual execution`
**Status:** COMPLETE
**Next canonical increment:** 15.2 — automate semantic regression suite

## 1. Purpose

Phase 15.1 defines the semantic acceptance oracle independently from current engine behavior.

The evaluation order is:

```text
ground truth / independent expectation
→ current-engine observation
→ PASS / FAIL / Deferred expected
→ automated regression
→ code change only for demonstrated gaps
```

The current baseline was observed with:

- native PdfPig extraction;
- the current deterministic planner;
- live PP-StructureV3 where layout evidence was required;
- live PaddleOCR where OCR/reconciliation evidence was required;
- exact fixture/source provenance validation;
- exact crop/hash validation where a human oracle exists.

No production policy or routing was changed during 15.1.

## 2. Classification semantics

| Classification | Meaning |
|---|---|
| **PASS** | Current baseline satisfies the independent semantic expectation. |
| **FAIL** | Current baseline does not satisfy the independent semantic expectation. This is a real remediation candidate. |
| **Deferred expected** | Current baseline refuses to guess when available runtime evidence is insufficient. This is safe fail-closed behavior, not semantic success. |

Historical parity is not an acceptance criterion.

## 3. Frozen invariants

```text
native text presence != native text trustworthiness

OCR recognition != layout analysis != reading order

model output = evidence
evidence != policy
policy != execution

Layout Figure != meaningful visual

SourceVisualIndex != semantic visual occurrence

Figure OCR = 0

Unknown / future layout kind
→ fail closed
→ not a text candidate
→ no invented semantic certainty

Caption remains a text candidate.

PhysicalPageNumber in a one-page fixture is 1.
Original physical page identity comes from fixture name + manifest.
```

## 4. Category coverage

| ID | Required category | Current controls | Phase 15.1 result |
|---|---|---|---|
| A | Born-digital normal native text | Habermas p70/p78/p79; De Decretis p512..p561 | **PASS** |
| B | Missing native text → OCR recovery | Ehrman p233 | **PASS** |
| C | Native present + OCR verification/reconciliation | Ehrman p380/p405 | **PASS** |
| D | Meaningful Figure + Caption → preserve region | Ehrman p79/p233; Habermas p40/p43 | **PASS** |
| D2 | Meaningful visual without sufficient caption evidence → preserve | Habermas p34/p36/p38/p44 | **FAIL** |
| E | Decorative visual → do not preserve semantic visual | Ehrman p148/p331 | **PASS** |
| F | Insufficient/ambiguous visual evidence → fail closed | Ehrman p36 | **Deferred expected** |
| G | Figure OCR = 0 | live p233/p380/p405 controls | **PASS** |
| H | Caption remains text candidate | Ehrman p233; layout controls p79/p40/p43 | **PASS** |
| I | Future/unknown layout kind → fail closed | deterministic contract test | **PASS** |
| J | Provenance / physical page identity | all 67 fixtures | **PASS** |
| K | Reading order | Ehrman p233 | **PASS** |
| L | Exact visual crop/hash | Ehrman p233; Habermas p40/p43 | **PASS** |

The added category **D2** is a direct result of the human ground truth. The initial matrix treated Habermas p34/p36/p38 as generic full-image/OCR candidates. Human review established that they are semantically meaningful relationship/hierarchy diagrams and must be preserved as visuals. Habermas p44 was already an independently meaningful visual control.

## 5. Ehrman controls

| Page | Independent expectation | Current observation at `62fc51e` | Classification |
|---|---|---|---|
| p36 | Decorative presentation/container visual; retain useful text/structure; Figure OCR = 0 | 688 native words; one PP Figure seq10; semantic evidence `Unknown`; no positive meaningful-visual decision | **Deferred expected** |
| p79 | Preserve informative pyramid diagram; retain caption/text; Figure OCR = 0 | one Figure seq2 + Caption seq3; `CaptionedMeaningfulVisual` | **PASS** |
| p148 | Discard tiny heading icon; retain heading/text | 789 native words; PP Figures = 0; heading/text remain | **PASS** |
| p233 | Missing native text; recover title/body/caption via targeted OCR; preserve papyrus only; no Figure OCR; preserve reading order | `Missing → LayoutWithTargetedOcrRecovery`; 7 real OCR calls; Figure OCR 0; exact papyrus custody PASS; `Imagine` seq3 precedes `for example` seq6 | **PASS** |
| p331 | Discard decorative heading icon; retain heading/text | 818 native words; PP Figures = 0 | **PASS** |
| p373 | Decorative framing/header; retain meaningful grouped/table text | 551 native words; PP emitted Table + Text and no Figure; exact final grouping semantics were not independently re-rendered during 15.1 | **Deferred expected** |
| p380 | Native/OCR conflict must remain unresolved | `Unverified → LayoutWithTargetedOcrReconciliation`; 9 real OCR calls; target seq5 = `Conflict / None / unresolved / divergence=true`; native block 2 | **PASS** |
| p405 | Native/OCR agreement may select native text; Figure must remain outside OCR | `Unverified → LayoutWithTargetedOcrReconciliation`; 8 real OCR calls; target seq9 = `Agreement / NativePdf / resolved / divergence=false`; native block 6; Figure OCR 0 | **PASS** |

## 6. Habermas controls

| Page | Human ground truth / expectation | Current observation at `62fc51e` | Classification |
|---|---|---|---|
| p34 | Meaningful full-page hierarchy/relationship diagram → preserve visual; do not flatten into narrative OCR; Figure OCR = 0 | nativeWords=0; PP Figure=1; semantic evidence `Unknown`; no text candidates; current semantic policy cannot positively preserve it | **FAIL** |
| p36 | Meaningful full-page hierarchy/relationship diagram → preserve visual; do not flatten into narrative OCR; Figure OCR = 0 | nativeWords=0; PP Figure=1; semantic evidence `Unknown`; no text candidates; current semantic policy cannot positively preserve it | **FAIL** |
| p38 | Meaningful full-page hierarchy/relationship diagram → preserve visual; do not flatten into narrative OCR; Figure OCR = 0 | nativeWords=0; PP Figure=1; semantic evidence `Unknown`; no text candidates; current semantic policy cannot positively preserve it | **FAIL** |
| p40 | Preserve “Conversion of the Skeptic James” diagram; retain surrounding text; Figure OCR = 0 | `CaptionedMeaningfulVisual`; exact crop `1678x1928`, 798598 bytes, SHA `bffd8d7fd27462ab530f50c483aa39bad7849653c81960d15e8cf73c8c829f03` | **PASS** |
| p43 | Preserve “Empty Tomb” diagram; retain surrounding text; Figure OCR = 0 | `CaptionedMeaningfulVisual`; exact crop `1702x1173`, 878872 bytes, SHA `bf20176970e097a3d20d1142db44488f7aba4e7ca8e76c5c8bf7b6b799b813f2` | **PASS** |
| p44 | Independently meaningful visual occurrence → preserve; retain narrative text; Figure OCR = 0 | 315 native words; one PP Figure seq0; semantic evidence `Unknown`; 7 text candidates; current semantic policy cannot positively preserve the visual | **FAIL** |
| p70 | Normal born-digital text → native-first | 711 words; `Healthy / NativeOnly`; no Figure | **PASS** |
| p78 | Normal born-digital text → native-first | 696 words; `Healthy / NativeOnly`; no Figure | **PASS** |
| p79 | Normal born-digital text → native-first | 958 words; `Healthy / NativeOnly`; no Figure | **PASS** |

## 7. De Decretis controls

For original physical pages p512..p561:

```text
50/50 fixtures
29,044 words
269 blocks
50 pages with words
0 pages without words
Healthy / NativeOnly: 50/50
sentinel "endless ages of ages. Amen.": 1 word-stream match, 1 block match
```

Classification: **PASS** for the current native-first regression control.

The source itself is not mutated merely to remove PDF/XRef warnings.

## 8. Provenance

The local fixture manifest was verified end-to-end:

```text
67 rows
Ehrman       8
Habermas     9
De Decretis 50

fixture_page = 1 for every one-page fixture
fixture filename encodes original physical page
canonical source SHA is pinned per corpus
fixture SHA matches manifest
fixture byte length matches manifest
```

Classification: **PASS**.

## 9. Reading order

Ehrman p233 independent oracle:

```text
Imagine
→ for example
```

Current real OCR observation:

```text
seq3 contains "Imagine"
seq6 contains "for example"
```

Classification: **PASS**.

## 10. Exact visual custody

| Control | Dimensions | Bytes | SHA-256 | Result |
|---|---:|---:|---|---|
| Ehrman p233 papyrus | `841x1398` | `1505768` | `c4170e36da6d0bfdec419f8db199ba972baf3075887a264aa2e9e4d46e6e4e77` | **PASS** |
| Habermas p40 | `1678x1928` | `798598` | `bffd8d7fd27462ab530f50c483aa39bad7849653c81960d15e8cf73c8c829f03` | **PASS** |
| Habermas p43 | `1702x1173` | `878872` | `bf20176970e097a3d20d1142db44488f7aba4e7ca8e76c5c8bf7b6b799b813f2` | **PASS** |

## 11. Demonstrated gaps

Phase 15.1 exposes one coherent product gap, represented by four pages:

```text
Habermas p34
Habermas p36
Habermas p38
Habermas p44
```

Human ground truth:

```text
meaningful visual
→ preserve
```

Current runtime evidence:

```text
PP Figure
→ DefaultLayoutVisualEvidenceAssessor
→ no strong caption association
→ Unknown
→ fail closed / Deferred
```

Safe deferral avoids a false semantic decision, but it does **not** satisfy the positive product requirement to preserve these known meaningful diagrams.

Therefore these four controls are **FAIL**, not `Deferred expected`.

This gap must be automated in 15.2 before remediation is attempted.

## 12. Phase 15.1 exit criteria

- [x] Categories A–L represented.
- [x] Human GT separated from architectural expectation and historical evidence.
- [x] Exact visual crop/hash oracles recorded.
- [x] Reading-order oracle recorded and observed.
- [x] Missing ground truth closed or explicitly classified.
- [x] Current baseline observed on selected controls.
- [x] Every selected control classified `PASS`, `FAIL`, or `Deferred expected`.
- [x] FAILs tied to independent human expectations, not legacy parity.
- [x] Matrix is now eligible to become an automated regression suite.

## 13. Phase transition

```text
15.1 semantic regression matrix
DONE

15.2 automate semantic regression suite
ACTIVE

Known red controls to automate first:
  Habermas p34
  Habermas p36
  Habermas p38
  Habermas p44
```

No remediation should be implemented until the automated suite can reproduce these failures.
