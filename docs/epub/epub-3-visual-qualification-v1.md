# EPUB-3 visual qualification V1

## Status

**EPUB-3 — Deterministic visual qualification with optional Paddle fallback: complete**

EPUB-3 separates inexpensive publication facts from optional external visual
analysis. The user controls only the external Paddle fallback; deterministic
EPUB qualification always runs.

## Deterministic qualification

The EPUB adapter acquires facts and the Engine assigns the shared visual
evidence vocabulary:

```text
cover / navigation / explicit presentation
    -> PublicationPresentationVisual

image used before OPF/landmark bodymatter
    -> PublicationPresentationVisual

image used in bodymatter
    -> StructuredContentMeaningfulVisual

no authoritative bodymatter boundary
    -> Unknown
```

EPUB 3 `landmarks/bodymatter` is preferred. The EPUB 2 OPF guide `type="text"`
is the fallback. No filename is used by production policy.

`PublicationPresentationVisual` is omitted from documentary output.
`StructuredContentMeaningfulVisual` is preserved and returned with
`DocumentVisualQualification.Meaningful`.

## User-controlled Paddle fallback

The per-request option is disabled by default:

```csharp
var requestOptions = new DocumentProcessingRequestOptions(
    qualifyUnresolvedVisuals: true);

var outcome = await host.ProcessDocumentAsync(
    source,
    requestOptions,
    cancellationToken);
```

Only `Unknown` images can enter this path. With the default option:

- no Paddle request is made;
- the source image remains conservatively preserved;
- its result qualification is `Unqualified`.

With the option enabled, supported raster images are sent to the configured
PP-Structure capability. A detected Figure or Table qualifies the image as
`Meaningful`; insufficient evidence leaves it `Unqualified`. Deterministically
resolved images never call Paddle, even when the option is enabled.

The temporary page number required by the existing PP-Structure adapter is an
internal request carrier only. It never becomes an EPUB page or a result
location.

## Habermas truth set

The exact reference EPUB establishes:

```text
manifest images                    27
cover image                         1
preliminary/title images U6/U7      2
meaningful body images             24
Paddle calls                        0
```

The 24 meaningful resources begin at `image_rsrc3U8.jpg` and end at the last
present `image_rsrc3U*.jpg` resource. Their paths, exact lengths, SHA-256 values
and `Meaningful` qualifications are frozen in
`docs/evaluation/habermas-epub-visual-reference-v1.json`.

The filename pattern is evaluation truth supplied for this corpus, not a
production classification rule.

Run:

```bash
./scripts/run-epub-visual-regression.sh
```

The workflow executes once with the default option and once with Paddle
fallback enabled. Both runs produce the same Habermas result while using an
unreachable Paddle endpoint, proving that the cover exclusion and all 26
referenced non-cover images are resolved by EPUB facts alone.
