# EPUB-1 validation boundary V1

## Status

**EPUB-1 — First native EPUB processing: complete**

The first EPUB-1 slice established the mandatory conformance gate and its
failure contract. The completed increment now registers EPUB in the production
Host and continues conformant sources through native package, spine and XHTML
acquisition and Engine-owned non-paged projection.

## Official dependency

`DocumentProcessing.Epub` owns the EPUB-specific integration with the official
EPUBCheck 5.3.0 distribution. The project records the exact dependency in
`src/DocumentProcessing.Epub/Dependencies/epubcheck-5.3.0.json`:

```text
Maven artifact       org.w3c:epubcheck:5.3.0
distribution SHA-256 6c07e68584b2e2ce2f89fe06e1246dfead3eb36b46b340e7d93524f29dcff6c5
JAR SHA-256          f7f96617c929371821609b88c8484d6dc9f24fe916499863c46094c5fb778a65
license              BSD-3-Clause
```

EPUBCheck is a Java dependency, not a native .NET package. The V1 adapter runs
the official command-line entry point directly without a shell. Ordinary .NET
builds do not download tools from the network. Deployment must provision the
pinned distribution and a Java runtime explicitly.

## Internal conformance states

The adapter preserves five states for tests, logs and operational diagnosis:

```text
Conformant
NonConformant
Unavailable
Failed
TimedOut
```

The supervised invocation has a configurable timeout, kills the Java process
tree when that timeout expires, drains stdout and stderr without deadlock,
retains at most 8,192 diagnostic characters per stream, limits the JSON report
to 1 MiB, verifies the pinned JAR identity and removes its temporary report.

Explicit caller cancellation remains cancellation. A checker timeout or an
unexpected checker termination is not caller cancellation.

## Consumer-facing contract

Technical checker states are deliberately collapsed before they reach the
Engine result:

| Internal state | Acquisition result | Consumer message |
|---|---|---|
| `Conformant` | continue acquisition | none |
| `NonConformant` | invalid document | `Le fichier EPUB n’est pas conforme.` |
| `Unavailable` | temporarily unavailable | `La validation EPUB est temporairement indisponible.` |
| `Failed` | temporarily unavailable | `La validation EPUB est temporairement indisponible.` |
| `TimedOut` | temporarily unavailable | `La validation EPUB est temporairement indisponible.` |

Process output, Java command details, local paths and exceptions stay out of
`NativeEvidenceExtractionResult` and `DocumentProcessingOutcome`. They are
available only through structured internal logging.

## Engine contract addition

`NativeEvidenceExtractionResult.Unavailable` and the corresponding Engine
selection result now distinguish a recognized format whose required acquisition
capability is unavailable from a document proven invalid. The Engine forwards
only the consumer-safe reason through its existing functional-failure boundary.

## Validation evidence

The deterministic unit tests cover all five internal states, their public
mapping, cancellation, dependency mismatch and absence, and the absence of a
technical diagnostic payload in the conformance result.

The production adapter has also been exercised locally against the pinned
EPUBCheck 5.3.0 distribution and the exact Habermas reference EPUB established
by EPUB-0; the result was `Conformant`.

## Native processing continuation

After a `Conformant` result, the registered format now performs:

1. bounded recognition and temporary materialization of EPUB sources;
2. package, spine and XHTML acquisition with guarded archive/XML parsing;
3. EPUB-specific non-paged source structure and locations;
4. Engine-owned whitespace normalization, content-unit segmentation and
   projection to `DocumentProcessingResult`.

The accepted Habermas result is frozen in
`docs/evaluation/habermas-epub-native-reference-v1.json` and reproduced by
`scripts/run-epub-native-regression.sh`. EPUB spine items are not represented
as physical pages merely to reuse the PDF projector.
