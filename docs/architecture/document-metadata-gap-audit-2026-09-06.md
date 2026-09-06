# Document metadata gap — audit — 2026-09-06

Audit only. No code was modified, no migration created, no refactoring, no
implementation. Every claim below was read from the current code, not from a
handoff or a phase record.

## 1. Executive summary

**The gap is confirmed, and it is wider than the title.**

`DocumentProcessingResult` carries no documentary metadata model. Its
`DocumentSourceDescriptor` is documented as "identity and descriptive metadata"
but exposes only `Format`, `Sha256`, `ByteLength`, `FileName` and
`DeclaredMediaType`. The last two are **caller-provided** — they describe the
physical artifact handed to the engine, not the intellectual document inside it.
There is no `Title`, no author, no publisher, no date, no description.

Three findings sharpen that.

**PDF native metadata is never read.** A search of `DocumentProcessing.Pdf` for
the PDF Info dictionary, XMP, or any equivalent returns nothing. PdfPig is
already a dependency and already exposes `document.Information` with `Title`,
`Author`, `Subject`, `Keywords`, `Creator`, `Producer` and creation dates. The
engine reads the same document for bookmarks — `PdfDocumentFormat.cs:145`,
`document.TryGetBookmarks` — and ignores the information dictionary sitting
beside it. This is unread data, not missing data.

**EPUB native metadata *is* read, and reaches the result — through a
format-specific door.** `EpubPackageExtractor.cs:261` builds an
`EpubDocumentSourceStructure` carrying `Title`, `Identifier` and `Language`,
read from the OPF package by `ReadMetadata(package, "title" | "identifier" |
"language")`. It reaches `DocumentProcessingResult.SourceStructure`. But
`SourceStructure` is an abstract `DocumentSourceStructure`, so a consumer must
downcast to an EPUB type to see a title — which is exactly what a format-neutral
portable result exists to prevent.

**The Manager cannot publish that title anyway.**
`PagedDocumentProcessingResultJsonEncoder.Encode` throws
`NotSupportedException` unless `SourceStructure is PagedDocumentSourceStructure`
(line 43), and its converter throws again on write for any non-paged structure
(line 96). EPUB is not referenced anywhere in `DocumentProcessing.Manager` or
`DocumentProcessing.Manager.DPEngine`. So the one title the engine does extract
cannot cross the boundary to Apologia today.

Downstream, `DocumentManagerEditorialDraftFactory.ProposeTitle(originalFileName)`
uses the file name because **no documentary title is available to it**. That is a
correct local decision over a missing portable fact, not an Apologia defect.

## 2. Current portable result

`src/DocumentProcessing.Core/Results/Processing/DocumentProcessingResult.cs`,
schema `document-processing-result-v4`.

| Concern | Property | Type | Present? |
|---|---|---|---|
| Source identity | `Source.Format` | `DocumentFormatId` | Yes — physical |
| Source identity | `Source.Sha256` | `string` | Yes — physical, custody root |
| Source identity | `Source.ByteLength` | `long` | Yes — physical |
| Filename | `Source.FileName` | `string?` | Yes — **caller-provided**, physical |
| Media type | `Source.DeclaredMediaType` | `string?` | Yes — **caller-provided**, physical |
| **Title** | — | — | **No** |
| **Subtitle** | — | — | **No** |
| **Description / abstract** | — | — | **No** |
| **Authors / contributors** | — | — | **No** |
| **Publisher** | — | — | **No** |
| **Publication date** | — | — | **No** |
| **Language** | — | — | **No** at result level |
| Table of contents | — | — | **No** as such |
| Structure | `StructuralSegments[].HeadingText` | `string?` | Partially — see below |
| Structure | `SourceStructure` | `DocumentSourceStructure?` | Format-specific, see below |
| Elements | `Elements`, `ElementProcessingEvidence` | lists | Yes |
| Custody | `ProcessingManifest` | `DocumentProcessingManifest` | Yes |
| Visuals | `VisualAssets` | list | Yes |
| Notes | `Notes` | `IReadOnlyList<DocumentNote>` | Yes |

`Source.FileName` **is not** a document title. It is optional, supplied by the
caller, and never validated against document content. The type's own remarks say
the descriptor "deliberately contains no physical-page count or other
format-specific structural state" — the omission of documentary metadata is not
stated as deliberate anywhere.

Two format-specific structures exist behind `DocumentSourceStructure`:

- `PagedDocumentSourceStructure` — page count only, no metadata.
- `EpubDocumentSourceStructure`
  (`src/DocumentProcessing.Epub/Locations/EpubDocumentSourceStructure.cs`) —
  `PackagePath`, `SpineItems`, **`Title`**, **`Identifier`**, **`Language`**,
  `BodyMatterStartSpineIndex`.

`DocumentStructuralSegment`
(`src/DocumentProcessing.Core/Results/Elements/DocumentStructuralSegment.cs`)
carries `SegmentId`, `Ordinal`, `Text`, `TextSha256`, **`HeadingText`** and
`SourceElementIds`. Ordered headings therefore exist in the portable result.

## 3. PDF metadata flow

| Item | State |
|---|---|
| PDF Info dictionary (`/Title`, `/Author`, `/Subject`, `/Keywords`, `/Creator`, `/Producer`, `/CreationDate`, `/ModDate`) | **Not read.** No reference anywhere in `DocumentProcessing.Pdf`. |
| XMP metadata | **Not read.** |
| Outline / bookmarks | **Read**, `PdfDocumentFormat.cs:145` via `document.TryGetBookmarks`, `UglyToad.PdfPig.Outline`. Used for split proposals only, per `current-architecture.md`: "Publisher-supplied PDF outlines or EPUB navigation are preferred." Never projected as metadata. |
| Headings | Extracted as structural evidence and projected into `StructuralSegment.HeadingText`. |

**No new dependency is required.** PdfPig 0.1.15 is already referenced and
already exposes `DocumentInformation` on the same `PdfDocument` instance the
format adapter opens. Reading `/Title` is strictly less work than reading the
bookmark tree, which the adapter already does.

Nothing else in the repository exposes PDF metadata: no second PDF primitive, no
external metadata tool.

Classification: PDF native metadata is **not read at all** — neither read and
dropped, nor read and unprojected.

## 4. EPUB metadata flow

| Item | State |
|---|---|
| `dc:title` | **Read** — `EpubPackageExtractor.cs:264-266`, `ReadMetadata(package, "title")` |
| `dc:identifier` | **Read** — same call site |
| `dc:language` | **Read** — same call site |
| Subtitle (`title-type` refinement) | **Not read.** EPUB 3 expresses subtitle through `meta refines` on the title; `ReadMetadata` returns a single element's text. |
| `dc:creator` / contributors | **Not read** on the ingest path |
| `dc:publisher` | **Not read** |
| `dc:description` | **Not read** |
| `dc:date` | **Not read** |
| Navigation / TOC | **Read** — `ReadNavigationContentResourcePaths`, `ReadNavigationReferences`; used to locate body matter and to propose splits |
| Spine / structure | **Read** — `ReadSpine`, retained as `SpineItems` with `SpineIndex`, `IdRef`, `ResourcePath`, `MediaType`, `IsLinear` |

Note the asymmetry with the **export** path: `EpubPublicationMetadata`
(`src/DocumentProcessing.Epub/Export/EpubPublicationMetadata.cs`) already models
`Title`, `Language`, `Creator`, `Identifier`, `ModifiedAtUtc`. The engine can
already *write* more publication metadata than it *reads*.

**What reaches `DocumentProcessingResult`:** title, identifier and language, via
`StructuredNativeDocumentProjector.cs:592` passing `evidence.SourceStructure`
through. **What reaches Apologia:** nothing, because the Manager encoder rejects
non-paged structures.

## 5. Current title flow

```text
source document
  ↓
format adapter
    PDF   : /Title present in ~most publisher PDFs   → NEVER READ
    EPUB  : dc:title                                  → READ
  ↓
native evidence
    PDF   : no title anywhere
    EPUB  : EpubDocumentSourceStructure.Title
  ↓
Engine
    no reconciliation step for documentary metadata exists
  ↓
DocumentProcessingResult
    Source.FileName            caller-provided, physical
    SourceStructure.Title      EPUB only, behind a downcast
  ↓
Manager publication
    PagedDocumentProcessingResultJsonEncoder
    throws NotSupportedException unless SourceStructure is Paged   ← EPUB title dies here
  ↓
Apologia import
    ReceivedDocumentManagerResult(byte[] Payload)
    payload parsed once only to verify the advertised schema version
  ↓
editorial draft
    DocumentManagerEditorialDraftFactory:
        Title       = ProposeTitle(assembly.OriginalFileName)
        Description = null
  ↓
UI review
    reviewer sees a filename in the Title field and corrects it by hand
```

Where the title is lost, precisely:

- **PDF — never created.** `/Title` is never read. Nothing is lost because
  nothing was ever extracted.
- **EPUB — extracted, then blocked at the boundary.** `dc:title` reaches
  `DocumentProcessingResult` and dies at the Manager encoder.
- **Engine — never reconciled.** No phase produces a concluded document title
  from competing evidence. `StructuralSegment.HeadingText` and the PDF outline
  are available as evidence and are never used for this purpose.
- **Apologia — substituted, correctly.**
  `DocumentManagerEditorialDraftFactory.ProposeTitle(originalFileName)`
  (line 67) takes the filename, strips the extension, trims to 1000 characters.
  Confirmed: **the filename is used as a pseudo-title precisely because no
  portable documentary title exists.** The comment on the draft's genre/form
  field — "A new draft carries no genre/form: the reviewer chooses" — shows the
  same deliberate restraint; the title has no such choice available.

## 6. Metadata ownership analysis

Using the requested four categories:

**A. Native metadata** — facts a format represents about itself.
PDF `/Title`, `/Author`, `/Subject`, `/Keywords`, `/CreationDate`, XMP `dc:*`;
EPUB `dc:title`, `dc:creator`, `dc:publisher`, `dc:language`, `dc:description`,
`dc:date`. Reading these is unambiguously the **format adapter's**
responsibility: only the adapter knows the representation.
*Current state: EPUB partial, PDF none.*

**B. Structural evidence** — facts derivable from document content.
Title-page text, the dominant first heading, the outline or navigation tree.
Extraction is the adapter's; interpretation is not.
*Current state: `StructuralSegment.HeadingText` exists, PDF bookmarks and EPUB
navigation are read — all of it used only for splitting.*

**C. Engine-concluded document metadata** — a single title chosen after
reconciling A and B, with provenance.
This is precisely what the normative pipeline exists to do. `ASSESS`,
`RECONCILE` and `ASSEMBLE` are already the phases where competing evidence
becomes one neutral answer.
*Current state: **does not exist**. No type, no phase, no field.*

**D. Consumer-specific metadata** — Apologia's Genre/Form, canonical taxonomy,
editorial workflow. Correctly outside the engine and staying there.

The boundary the architecture states is:

```text
FORMAT   → knows the native facts of its representation
ENGINE   → decides how to reconcile evidence into the neutral document
APOLOGIA → enriches with business semantics
```

Categories A and B are held today but never leave the format layer as metadata.
Category C is **missing entirely**, and its absence is what pushes Apologia to
invent a title from a filename — a consumer doing engine work with worse
information than the engine had.

## 7. Confirmed gaps

1. **No documentary metadata model in the portable result.** Category C has no
   representation. This is the primary gap.
2. **PDF native metadata unread**, despite the dependency already exposing it
   and the adapter already opening the document for bookmarks.
3. **EPUB metadata reachable only by downcast**, and only title, identifier and
   language.
4. **EPUB results cannot be published at all** through the Manager encoder, so
   even the metadata that exists cannot reach a consumer.
5. **No reconciliation of documentary evidence.** Outline, headings and native
   fields are read for splitting, never combined into a concluded title.

### Is this a DPEngine responsibility?

**Yes**, at the `RECONCILE` / `ASSEMBLE` level, for three reasons drawn from the
code rather than from preference.

Only the format layer can read `/Title` or `dc:title`; a consumer receiving
`byte[]` and a schema version cannot. The engine already owns the equivalent
decision for text — `NativeTextStatus`, the three closed page routes, and
reconciliation of native against OCR evidence — so concluding one title from
several evidences is the same shape of decision it already makes. And the
alternative is worse: every consumer would re-implement per-format metadata
reading, which is exactly the duplication the portable result exists to prevent.
The two-repository PdfPig duplication already noted in Apologia's own guide is
the warning sign.

It is **not** an Apologia responsibility because Apologia never sees the source
bytes — it receives a portable result plus custody hashes — and because a second
consumer would need the same facts.

### Formats concerned

Today: **PDF** (nothing read) and **EPUB** (partially read, cannot be
published). Both are in production.

Future, without designing them now: DOCX (`docProps/core.xml`, OOXML core
properties), ODT (`meta.xml`, Dublin Core), PPTX (same OOXML core properties),
HTML (`<title>`, `<meta name="description">`, OpenGraph). Every one of these
carries native Dublin-Core-shaped metadata. A per-format contract that returns
native metadata as evidence generalises without further design; a title field
bolted onto the PDF path would not.

## 8. Minimal target model

Only what the evidence justifies. Nothing is added for a format that does not
exist yet.

```text
DocumentProcessingResult
  └── DocumentMetadata?          new, optional
        ├── Title?               concluded, with provenance
        ├── Subtitle?            EPUB 3 refinement, PDF rarely
        ├── Description?         EPUB dc:description, PDF /Subject
        ├── Contributors?        EPUB dc:creator, PDF /Author
        ├── Publisher?           EPUB dc:publisher
        ├── Language?            EPUB dc:language  (already extracted)
        └── Dates?               EPUB dc:date, PDF /CreationDate
```

### Simple values, values with provenance, or candidates?

The evidence points to **values with provenance, not a candidate-resolution
structure** — and the reason is specific rather than aesthetic.

A plain `string? Title` would be indistinguishable between "the publisher
declared this" and "we guessed from the first heading". Apologia's editorial
review shows a human a proposed title; a reviewer needs to know whether they are
confirming a publisher's own metadata or an inference. The repository already
holds this pattern: `DocumentElementProvenance.TextOrigin` distinguishes native
text from recovered text for exactly the same reason.

A full multi-candidate structure is not justified yet. Nothing today produces
competing titles: PDF reads none, EPUB reads one. Building resolution machinery
before two sources exist would be designing for a conflict that has never
occurred.

The minimum that is honest:

```text
DocumentMetadataValue<T>
  ├── Value
  ├── Origin        Native | Structural | Absent
  └── SourceHint?   "/Title", "dc:title", "first-heading"
```

`Origin` is the field that matters. It lets Apologia decide to trust a
publisher's title and to flag an inferred one for review — the decision it
cannot make today because a filename and a real title are the same `string`.

Reconciliation, when a second source appears, then becomes a rule *inside*
`ASSEMBLE` rather than a change to the portable shape.

## 9. Integration impacts

**Portable schema version.** Adding an optional `DocumentMetadata?` to
`DocumentProcessingResult` changes `document-processing-result-v4`. The
serializer sets `DefaultIgnoreCondition = WhenWritingNull`, so a null metadata
object is absent from the JSON and existing consumers are unaffected — but the
schema identifier is asserted on both sides
(`ConsumeDocumentManagerResultHandler.VerifyAdvertisedSchemaVersion`), so a
version bump to `v5` propagates to Apologia whether or not the payload changes.
This is the single largest coordination cost and must be planned across both
repositories.

**PDF.** Additive. Reading `document.Information` in `PdfDocumentFormat` beside
the existing bookmark read. No route change, no assessment change, no OCR
interaction.

**EPUB.** Two changes of different natures: extend `ReadMetadata` to further
`dc:*` elements, and lift title/language out of `EpubDocumentSourceStructure`
into the neutral model. Leaving them duplicated in both places during migration
avoids breaking anything that downcasts today.

**Manager.** `PagedDocumentProcessingResultJsonEncoder` is the real blocker. It
is paged-only by construction, and until it can encode a non-paged structure,
EPUB cannot be published regardless of metadata work. This is a pre-existing
limitation that the metadata gap merely exposes.

**Apologia import.** `DocumentManagerEditorialDraftFactory.ProposeTitle` becomes
a fallback used only when no native or structural title arrived. `Description`
stops being unconditionally null. `ReceivedDocumentManagerResult.Payload` is
already retained in `DocumentManagerResultInboxEntity`, so no new transport is
needed — but Apologia currently parses that payload only to check a schema
version, so a real reader is new work there.

**Backward compatibility.** Drafts already created keep filename titles. Nothing
should retroactively rewrite a reviewer-edited title; the migration story is
"new submissions get better proposals", not a backfill.

**Tests.** The engine's result-shape tests, the JSON encoder tests, and
Apologia's `DocumentManagerEditorialDraftFactory` tests are the ones that will
move. The Apologia architecture tests are unaffected: nothing here changes
layering.

## 10. Recommended implementation slices

In dependency order, each independently valuable.

1. **Read PDF native metadata as evidence.** Smallest possible: read
   `document.Information` in `PdfDocumentFormat` and carry it as native
   evidence. No portable-shape change, no schema bump, no consumer impact. It
   converts "we don't have it" into "we have it and haven't decided what to do
   with it", and makes the rest measurable on a real corpus.
2. **Extend EPUB reading** to `dc:creator`, `dc:publisher`, `dc:description`,
   `dc:date` alongside the existing three. Same containment.
3. **Introduce `DocumentMetadata` in the portable result**, with `Origin`
   provenance, populated from 1 and 2. This is the schema bump and the
   coordination point with Apologia.
4. **Consume it in Apologia**: `ProposeTitle` becomes a fallback, `Description`
   is populated when present.
5. **Structural title inference** — first heading, outline root — only if
   measurement after slice 1 shows how many real documents lack native metadata.

Slice 1 is deliberately first because it answers, with data instead of
assumption, how large the problem actually is.

## 11. Open questions

**How often is PDF `/Title` present, and is it usable?** Publisher PDFs often
carry a filename, a LaTeX job name, or "Microsoft Word - final3.docx" in
`/Title`. Slice 1 makes this measurable on the existing evaluation corpus
before any portable shape is frozen. Deciding the model without that number
risks trusting a field that is frequently junk.

**Should the engine ever infer a title from content?** Reading `/Title` is
uncontroversially the format layer's job. Choosing the dominant first heading as
a title is an inference, and it is the kind of decision this project has
repeatedly and deliberately kept out of the extraction path. Recommendation:
keep inference out of slices 1 to 4, decide it separately once the native-hit
rate is known.

**Does the Manager need non-paged encoding first?** EPUB metadata cannot reach
any consumer until the encoder accepts non-paged structures. Whether that is
part of this work or a separate correction is a sequencing decision, not a
technical one.
