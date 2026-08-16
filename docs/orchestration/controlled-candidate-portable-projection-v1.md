# H.4D.4B.1 — Candidate portable projection substrate

## Status

```text
H.4D.4A    DONE
H.4D.4B    ACTIVE

H.4D.4B.1  ACCEPTED
H.4D.4B.2  NEXT
```

## Why H.4D.4B is split

H.4D.4A intentionally identified three remaining evidence gaps:

```text
PortableOutputNotCompared
ProvenanceNotCompared
CandidateVisualPersistenceNotCompared
```

The current canonical `HybridDocumentElement.Visual` requires both preserved
visual bytes and `Figure` layout evidence. H.4D.3B source-occurrence
preservation, however, intentionally has no layout semantics.

Likewise `AnalyzeVisual` is unresolved evidence. Converting it directly into a
`Figure` would turn analysis evidence into policy.

H.4D.4B therefore cannot honestly be completed by fabricating canonical visual
elements.

## H.4D.4B.1 scope

H.4D.4B.1 adds the smallest projection substrate needed for the final
comparison.

```text
controlled candidate text execution
    -> retain actual HybridDocumentPage

retained candidate pages
    -> HybridDocumentAssembler
    -> HybridDocumentNormalizer
    -> HybridDocumentSegmenter
    -> DocumentIngestionResultBuilder
    -> candidate canonical DocumentIngestionResult

PreserveMeaningfulVisual
    -> neutral source-occurrence provenance sidecar

AnalyzeVisual
    -> neutral unresolved raster/layout provenance sidecar
```

The sidecars are part of controlled candidate evidence. They are not inserted
into the canonical document as invented visual elements.

## Run-level identities

Canonical provenance already requires explicit raster/layout identities for
layout-backed evidence and an explicit reconciliation identity for
reconciliation-backed evidence.

H.4D.4B.1 therefore requires those candidate identities explicitly when the
retained candidate pages actually contain that evidence.

No authoritative processing identity is silently borrowed merely to make the
candidate projection build.

## Authority

```text
authoritative DocumentIngestionResult
    built first

candidate text
candidate visual
H.4D.4A comparison
H.4D.4B.1 projection
    all shadow/evaluation only

return authoritative DocumentIngestionResult
```

No candidate result is returned by `DocumentProcessor`.

## Failure semantics

```text
candidate execution incomplete
    -> InputUnavailable

ordinary projection/provenance failure
    -> Failed report
    -> authoritative result unchanged

ordinary observer failure
    -> best effort

caller cancellation
    -> propagate

OutOfMemoryException
    -> propagate
```

## What H.4D.4B.1 does not claim

H.4D.4B.1 does not clear H.4D.4A cutover blockers.

In particular:

```text
source-preserved visual bytes
    are still not owned by a final caller persistence contract

AnalyzeVisual evidence
    is still unresolved

candidate canonical output
    has not yet been compared as the final cutover candidate
    against authoritative output/provenance
```

## H.4D.4B.2 next

H.4D.4B.2 must provide:

```text
caller-owned candidate source-visual persistence
safe final disposition for analyzed visuals
complete candidate-vs-authoritative output comparison
complete candidate-vs-authoritative provenance comparison
explicit blocker clearing only when evidence supports it
```

Only H.4D.4B.2 may evaluate guarded cutover readiness.
## H.4D.4B.1 acceptance evidence

H.4D.4B.1 is accepted as the non-authoritative candidate portable-output and
provenance substrate.

Deterministic evidence:

```text
Release -warnaserror        PASS
focused H.4D.4B.1 tests     9 / 9
complete regression         552 / 552
```

Integrated real-corpus evidence:

| Corpus | Physical page | Candidate document | Source visual sidecar | AnalyzeVisual sidecar |
|---|---:|---|---:|---:|
| Habermas | 40 | built | 1 exact | 0 |
| Habermas | 43 | built | 1 exact | 0 |
| Habermas | 44 | built | 1 exact | 0 |
| Ehrman | 36 | built | 0 | 0 |
| Ehrman | 148 | built | 0 | 1 unresolved |
| Ehrman | 233 | built | 0 | 1 unresolved |

All six controls build candidate `DocumentIngestionResult` and candidate
provenance.

The three Habermas controls retain exact source JPEG custody as neutral
sidecars, while their canonical candidate document is equivalent to the
authoritative canonical document.

Ehrman page 36 has no visual sidecar and is structurally ready for the final
H.4D.4B.2 comparison.

Ehrman pages 148 and 233 preserve `AnalyzeVisual` evidence as unresolved neutral
sidecars. Page 233 retains 10 layout observations including one `Figure`, while
Figure OCR remains zero.

No layout/Figure semantics are fabricated for preserved source visuals.

The acceptance does not authorize cutover:

```text
authority transfer              = no
guarded cutover ready           = no
final output comparison         = not performed
final provenance comparison     = not performed
performance acceptance          = none
```

H.4D.4B.2 is next. It owns final source-visual persistence, safe disposition of
unresolved visual-analysis evidence, complete candidate-versus-authoritative
output/provenance comparison, and explicit blocker clearing only when supported
by evidence.
