# EPUB-2 visual discovery and preservation V1

## Status

**EPUB-2 — Discovery and preservation of significant EPUB visuals: complete**

EPUB-2 extends the conformant EPUB path with deterministic discovery of image
resources used by reading content, exact-byte preservation through
`UserVisualAssetWriter`, and portable visual elements without fabricated page
geometry.

## Acquisition facts and Engine selection policy

The EPUB adapter starts from image usages in spine XHTML (`img` and SVG
`image`). It emits resource identity and the source facts below; it does not
decide the final output. The Engine applies the selection policy and retains a
resource once even when it is referenced several times.

The following facts make the Engine exclude a resource deterministically:

- OPF `cover-image` or EPUB 2 `meta name="cover"`;
- a cover content document identified by the OPF guide;
- the EPUB navigation document identified by the OPF `nav` property;
- an XHTML usage explicitly marked `aria-hidden="true"`,
  `role="presentation"` or `role="none"`;
- an image present in the manifest but unused by spine content.

An empty `alt` value alone does not exclude an image. The Habermas reference
uses `alt=""` for every substantive diagram, so treating it as sufficient
proof of decoration would silently lose documentary content.

An image referenced only from a spine item with `linear="no"` remains selected
and is marked auxiliary. Non-linear content is not silently discarded.

V1 is deliberately conservative: a referenced image that has no deterministic
exclusion is preservation-worthy. This prevents data loss but does not claim
that every retained image has already received a richer semantic label.

## Engine and callback contract

`UserVisualAssetWriter` keeps its human-facing role but now receives a neutral
`UserVisualAssetWriteRequest`:

- `UserLayoutVisualAssetWriteRequest` for a paged layout region;
- `UserSourceVisualAssetWriteRequest` for an exact embedded resource.

The EPUB adapter owns archive paths and exact resource materialization. The
Engine owns the preservation decision, invokes the user's writer, creates the
portable `DocumentElementKind.Visual`, and records `DocumentVisualAsset`
custody. EPUB assets have no `RasterDerivation` because their original packaged
bytes are copied directly using profile `epub-package-image-raw-v1`.

If selected visuals exist and the user supplied no writer, processing fails
closed instead of claiming that visual preservation succeeded.

## Habermas reference

The exact conformant Habermas EPUB produces:

```text
manifest image resources       27
excluded cover images           1
selected visual resources      26
auxiliary selected resources    0
portable visual elements       26
portable visual assets         26
raster derivations              0
```

All 26 selected JPEG files are copied byte-for-byte. Their paths, lengths and
SHA-256 values are frozen in
`docs/evaluation/habermas-epub-visual-reference-v1.json`. The EPUB-1 text
reference is checked in the same workflow and remains unchanged.

Run:

```bash
./scripts/run-epub-visual-regression.sh
```

## Current boundary

V1 does not discover CSS background images, video poster frames or generic
`object` resources. It also does not infer a richer visual taxonomy from image
pixels or nearby prose. Those are later semantic-enrichment decisions, not
reasons to invent geometry or discard source assets now.
