# Documentation guide

This directory contains both current documentation and permanent historical
records. The distinction matters: an increment document can accurately describe
its baseline while using names or "next step" statements that no longer describe
the current repository.

## Authority and status

Use this order when documents disagree:

1. [Current architecture](architecture/current-architecture.md) defines the
   active repository-level responsibilities and invariants.
2. Source code and executable tests define exact current behavior and contracts.
3. [Target architecture reference V1](evaluation/target-architecture-reference-v1.md)
   is frozen acceptance evidence for the architectural cutover.
4. Versioned design notes and evaluation records describe the commit or phase
   at which they were produced.

The root [README](../README.md) is the current operational entry point.

## Directory map

| Path | Status | Purpose |
|---|---|---|
| `architecture/current-architecture.md` | Current | Active ownership, dependency and extension rules. |
| `architecture/phase-*.md` | Historical | Increment decisions pinned to the `Baseline` recorded in each file. |
| `evaluation/target-architecture-reference-v1.md` | Frozen evidence | Proof that the cutover baseline satisfied the target architecture. |
| `evaluation/local-fixture-layout.md` | Current | Local test-document organization and pinned PNG toolchain. |
| `evaluation/habermas-epub-reference-v1.md` | Frozen evidence | Exact EPUB identity, official conformance result and p18/p28 structural controls. |
| `evaluation/habermas-epub-native-reference-v1.md` | Frozen evidence | Production Host native EPUB result, non-paged custody and text fingerprint. |
| `epub/epub-1-validation-boundary-v1.md` | Current design | Official EPUBCheck boundary and its integration with native non-paged EPUB processing. |
| `evaluation/` | Historical evidence | Reproducible corpus, diagnostic, performance and semantic observations. |
| Domain folders such as `orchestration/`, `reconciliation/`, `result/` | Versioned decision records | Design rationale and acceptance evidence for individual increments. |

## Reading historical records

Historical records are intentionally not rewritten to imitate current output.
For example, `NativePdf`, `Shadow` or `ControlledCandidate` may be correct names
for the baseline that a record documents even though active code now uses
`Native`, `DualRun` and `DualRunCandidate`.

Likewise, phrases such as "current production", "still does not" and "next
increment" are relative to the document's recorded baseline unless the file is
explicitly marked **Current**.

When changing runtime behavior:

- update the root README if capabilities, prerequisites or public entry points
  change;
- update `current-architecture.md` only for an explicitly reviewed architecture
  decision;
- add a new versioned decision/evaluation record instead of rewriting old
  evidence;
- keep assertions about exact current behavior executable in tests whenever
  practical.
