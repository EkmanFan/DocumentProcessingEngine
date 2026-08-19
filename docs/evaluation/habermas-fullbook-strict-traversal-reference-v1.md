# Habermas Full-Book Strict Traversal Reference V1

## Status

**PASS**

This reference freezes a strict full-book traversal of the 170-page Habermas source on commit `f456e59cf527349736f459bf4c16b20b9efd7111`.

It is a **fail-closed coverage/traversal reference**, not exhaustive human semantic acceptance of every element on every page.

## Execution contract

- Public `DocumentProcessor` processing path.
- Live PP-StructureV3.
- Live PaddleOCR.
- PP and Paddle concurrently resident.
- Diagnostic source patch: **none**.
- Semantic bypass: **none**.
- Physical pages traversed: **170 / 170**.
- Fail-closed interruption: **none**.
- Figure OCR: **0**.

## Reviewed result

| Metric | Result |
|---|---:|
| NativeOnly pages | 159 |
| OCR-backed planned pages | 11 |
| LayoutWithTargetedOcrRecovery | 11 |
| PP calls | 28 |
| OCR calls | 17 |
| OCR-executed pages | 4 |
| Full-page renders | 28 |
| Region renders | 39 |
| Preserved visuals | 22 |
| Preserved visual bytes | 23,984,612 |
| Result elements | 1,217 |
| Total processing time | 181.668 s |
| DPE peak working set | 475.2 MiB |
| Managed allocations | 4,268.4 MiB |

## Interpretation

This proves that the complete Habermas document traverses the current authoritative public path without a fail-closed interruption after the p18/p28 robustness correction.

The result also preserves the critical invariant **Figure OCR = 0**.

This reference does **not** claim exhaustive semantic correctness for all 1,217 produced elements. Existing page-level semantic fixtures and acceptance controls remain the correctness oracles for detailed behavior.

The known p28 `figure_title` classification weakness remains outside this reference because it did not prevent the accepted final document behavior and has not yet been generalized into a safe caption-semantics correction.

## Provenance

- Baseline commit: `f456e59cf527349736f459bf4c16b20b9efd7111`
- Source file: `The Case for the Resurrection of Jesus - Gary R. Habernas.pdf`
- Source SHA-256: `f367a503f298337ec589eb1ad5ec5fe956999e49205a486d50798bfeee6d0399`
- Strict traversal script SHA-256: `25a603f9bf79c3308381008ae8a4ff34b40d2598727c4858e51e26b39a3ac63a`
- Strict traversal report SHA-256: `48d1b48ec69a905e90cde2f4209fa8b0aa3f1eb428849ef192cc259e8a23ce72`
- Strict traversal console SHA-256: `7d2fa519949cd997bf59e933af01a67037ddccab81db416e8e14d7b0676b2c7f`

## Boundary

This is a **strict traversal reference**.

It is not:

- exhaustive page-by-page human semantic acceptance;
- a replacement for the permanent semantic regression suites;
- a new 67-fixture performance baseline;
- a claim that ConcurrentResident is universally safe for arbitrary shared-server workloads.
