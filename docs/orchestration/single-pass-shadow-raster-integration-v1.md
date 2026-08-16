# Phase 21E.2B — Single-pass shadow raster integration V1

## Status

**ACCEPTED — 2026-08-16**

Phase 21E.2B is complete.

The production candidate reuses one physical PdfPig page materialization for
authoritative native extraction plus H.4A raster observation whenever the
configured extractor and raster-observation source advertise the optional
coordinated capability.

Committed comparison baseline:

```text
3dc9b07e42d3a208f0332274a56a4680f723296a
```

Accepted pre-commit candidate-state fingerprint:

```text
4ee929f1b9a8d5aaa69126dbb1aedf605a7cf3d6f5c66e1f5a775c3d7062c0b5
```

H.4D remained frozen throughout 21E.2B and may only begin after this accepted
candidate is committed and the repository returns to a clean baseline.

## Boundary

The generic optional capability lives in `DocumentProcessing.Core`:

```text
IDocumentExtractorWithRasterObservations
```

It contains no PdfPig type.

The PDF adapter implements it only for the compatible pair:

```text
PdfPigDocumentExtractor
+
PdfPigVisualRasterObservationSource
```

`DocumentProcessing.Engine` remains independent of PdfPig and of
`DocumentProcessing.Pdf`.

If the capability is absent or incompatible, the pre-existing fallback remains:

```text
IDocumentExtractor.ExtractAsync
then later
IVisualRasterObservationSource.ObserveAsync
```

No plugin registry, generic DAG, or format-specific dependency was introduced
into Engine.

## Authoritative law

Native extraction remains authoritative.

```text
native extraction failure
    -> propagate

ordinary coordinated H.4A failure
    -> preserve authoritative extraction
    -> carry sanitized raster-acquisition failure
    -> H.4C reports RasterObservation failure
    -> authoritative legacy execution continues

caller cancellation
    -> propagate

OutOfMemoryException
    -> propagate
```

Partial raster observations are discarded after an ordinary acquisition
failure. They are never presented as complete document coverage.

## Shadow authority

The coordinated pass does not promote candidate planning.

`DocumentProcessor` still resolves authoritative legacy requirements before the
shadow candidate can affect any externally visible processing result.

The H.4C candidate remains diagnostics/evaluation only and is never read back
into authoritative route selection or authoritative output selection.

## Failure-order consequence

H.4A now executes during the shared physical traversal when the coordinated
capability is active.

An ordinary H.4A failure is captured and deferred to the shadow-reporting path,
so authoritative legacy behavior remains fail-open.

Caller cancellation and `OutOfMemoryException` are deliberately never deferred.
They can therefore surface earlier than in the former second-traversal shadow
path. Their fatal semantics are unchanged.

This temporal consequence is accepted as the cost of avoiding a second physical
PdfPig page materialization; retaining the former timing would require keeping
large format-specific page state alive or reintroducing the second traversal.

## Correctness evidence — 21E.2B.2B

The candidate passed:

- Release build with warnings as errors;
- focused coordinated/fallback/failure-semantics tests;
- full regression suite: 496 / 496;
- Engine -> Pdf/PdfPig dependency guard;
- exact native-extraction parity on Ehrman, De Decretis and Habermas;
- exact H.4A raster-observation parity on the same corpora.

Corpus parity:

| Corpus | Pages | Visuals | Native extraction | H.4A raster |
|---|---:|---:|---|---|
| Ehrman | 617 | 617 | exact | exact |
| De Decretis | 1479 | 3 | exact | exact |
| Habermas | 170 | 27 | exact | exact |

## Production performance evidence — 21E.2B.3

Method:

- exact detached `3dc9b07e42d3a208f0332274a56a4680f723296a` two-pass baseline;
- current uncommitted coordinated single-pass candidate;
- one warmup plus three measured runs;
- full GC between measured iterations;
- fresh one-shot `/usr/bin/time` MaxRSS probe;
- no external ML invocation;
- workload includes native extraction, legacy planning, native normalization,
  H.4A raster observation, H.4B structural enrichment, and H.3C guarded
  candidate planning.

Results:

| Corpus | Time baseline -> candidate | Delta | Alloc baseline -> candidate | Delta | MaxRSS baseline -> candidate | Delta |
|---|---:|---:|---:|---:|---:|---:|
| Ehrman | 44.761s -> 29.392s | -34.34% | 47.008 GiB -> 29.778 GiB | -36.65% | 7085.1 MiB -> 6751.6 MiB | -4.71% |
| De Decretis | 8.015s -> 7.232s | -9.77% | 11.154 GiB -> 9.128 GiB | -18.16% | 2062.6 MiB -> 2050.9 MiB | -0.57% |
| Habermas | 2.771s -> 2.321s | -16.26% | 2.956 GiB -> 2.324 GiB | -21.37% | 411.2 MiB -> 389.6 MiB | -5.25% |

The single-pass integration therefore retains a material performance benefit on
the real corpus, especially on Ehrman:

```text
median deterministic workload time  -34.34%
managed allocation volume           -36.65%
fresh-process MaxRSS                 -4.71%
```

## MaxRSS characterization — 21E.2B.3A

The first 2B.3 script contained an experimental Ehrman acceptance guard of at
least `-5%` fresh MaxRSS.

The observed one-shot value was:

```text
-4.71%
```

so that experimental aggregate script reported `FAIL` even though:

- exact correctness passed;
- time improved materially;
- allocations improved materially;
- MaxRSS also improved;
- De Decretis and Habermas passed their controls.

The threshold was not lowered after observing the result.

Instead, a separate threshold-free repeatability experiment ran five fresh
baseline and five fresh candidate processes in alternating order.

Repeatability result:

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Median MaxRSS | 7084.3 MiB | 6855.1 MiB | -3.24% (-229.2 MiB) |
| MaxRSS range | 7081.5 MiB - 7093.1 MiB | 6709.3 MiB - 6863.9 MiB | — |
| Median time | 46.686s | 31.655s | -32.20% |
| Median allocated | 47.042 GiB | 29.812 GiB | -36.63% |

The repeatability probe confirms that RSS improvement is modest rather than the
primary benefit of this increment.

It does not invalidate the architecture: the coordinated traversal removes a
large amount of transient allocation and roughly one third of deterministic
processing time on the dominant Ehrman corpus while preserving exact outputs.

## Acceptance decision

**ACCEPT 21E.2B.**

The acceptance is based on the engineering decision contract, not on moving the
experimental RSS threshold:

```text
exact correctness
+
failure semantics preserved
+
material deterministic time improvement
+
material allocation-volume improvement
+
no measured RSS regression
=
ACCEPT
```

The original `-5%` Ehrman RSS probe remains recorded as a missed experimental
guard. It is not rewritten to manufacture a pass.

The 21E.2B.1 measurements remain evidence anchors rather than fixed production
SLAs.

## Residual memory finding

On Ehrman the integrated workload still reaches a high physical-memory
high-water mark (median approximately 6855.1 MiB).

The single-pass change substantially reduces total allocation volume without a
proportional MaxRSS reduction. This indicates that the peak resident set is
likely dominated by another simultaneously-live state in the wider deterministic
workload rather than by the duplicate PdfPig traversal alone.

That question is retained as future memory/long-document optimization work. It
does not block acceptance of 21E.2B because this increment:

- removes the duplicate physical traversal;
- improves every measured corpus;
- introduces no correctness regression;
- introduces no measured RSS regression;
- preserves the architectural boundary.

## Next

After commit of this accepted candidate:

```text
21E.2B   COMPLETE
H.4D     NEXT — Controlled Candidate Execution
```

H.4D must remain controlled and must not retroactively turn the H.4C shadow
candidate into an authority without explicit guarded cutover evidence.
