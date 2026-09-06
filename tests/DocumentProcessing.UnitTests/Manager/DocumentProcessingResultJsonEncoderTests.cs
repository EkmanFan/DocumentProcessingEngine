using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Epub;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Epub.Locations;
using DocumentProcessing.Manager.DPEngine;
using DocumentProcessing.UnitTests.Epub;
using Xunit;

namespace DocumentProcessing.UnitTests.Manager;

/// <summary>
/// Portable result encoding across source-structure families.
/// </summary>
/// <remarks>
/// The encoder is write-only, so these assert the emitted JSON rather than a
/// symmetric round trip through a decoder that does not exist. What matters at
/// this boundary is that each family is written under its own discriminator,
/// that no family is coerced into another, and that an unknown family stops the
/// encode instead of producing a payload that silently lost its structure.
/// </remarks>
public sealed class DocumentProcessingResultJsonEncoderTests
{
    #region Methods Paged

    [Fact]
    public void Paged_result_is_encoded_under_the_paged_kind()
    {
        var json =
            Encode(
                BuildResult(
                    BuildPagedStructure(),
                    new PagedDocumentSourceLocation(
                        1)));

        var structure =
            json.RootElement.GetProperty(
                "sourceStructure");

        Assert.Equal(
            "paged",
            structure.GetProperty(
                    "kind")
                .GetString());

        Assert.Equal(
            1,
            structure.GetProperty(
                    "sourcePhysicalPageCount")
                .GetInt32());

        Assert.Equal(
            "paged",
            ElementLocation(
                    json)
                .GetProperty(
                    "kind")
                .GetString());
    }

    #endregion

    #region Methods Epub

    [Fact]
    public void Epub_result_is_encoded_without_inventing_pages()
    {
        var json =
            Encode(
                BuildResult(
                    BuildEpubStructure(),
                    new EpubDocumentSourceLocation(
                        0,
                        "OEBPS/chapter-1.xhtml",
                        3,
                        "para-7")));

        var structure =
            json.RootElement.GetProperty(
                "sourceStructure");

        Assert.Equal(
            "epub",
            structure.GetProperty(
                    "kind")
                .GetString());

        // No page count is invented for a document that has no pages.
        Assert.False(
            structure.TryGetProperty(
                "sourcePhysicalPageCount",
                out _));
        Assert.False(
            structure.TryGetProperty(
                "pages",
                out _));
    }

    [Fact]
    public void Epub_package_facts_survive_encoding()
    {
        var structure =
            Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubDocumentSourceLocation(
                            0,
                            "OEBPS/chapter-1.xhtml",
                            0)))
                .RootElement
                .GetProperty(
                    "sourceStructure");

        Assert.Equal(
            "OEBPS/content.opf",
            structure.GetProperty(
                    "packagePath")
                .GetString());
        Assert.Equal(
            "Jesus and the Eyewitnesses",
            structure.GetProperty(
                    "title")
                .GetString());
        Assert.Equal(
            "urn:isbn:9780802874313",
            structure.GetProperty(
                    "identifier")
                .GetString());
        Assert.Equal(
            "en",
            structure.GetProperty(
                    "language")
                .GetString());
        Assert.Equal(
            1,
            structure.GetProperty(
                    "bodyMatterStartSpineIndex")
                .GetInt32());

        var spineItems =
            structure.GetProperty(
                    "spineItems")
                .EnumerateArray()
                .ToArray();

        Assert.Equal(
            2,
            spineItems.Length);
        Assert.Equal(
            "OEBPS/cover.xhtml",
            spineItems[0]
                .GetProperty(
                    "resourcePath")
                .GetString());
    }

    [Fact]
    public void Epub_location_retains_its_native_coordinates()
    {
        var location =
            ElementLocation(
                Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubDocumentSourceLocation(
                            4,
                            "OEBPS/chapter-2.xhtml",
                            11,
                            "note-3"))));

        Assert.Equal(
            "epub",
            location.GetProperty(
                    "kind")
                .GetString());
        Assert.Equal(
            4,
            location.GetProperty(
                    "spineIndex")
                .GetInt32());
        Assert.Equal(
            "OEBPS/chapter-2.xhtml",
            location.GetProperty(
                    "resourcePath")
                .GetString());
        Assert.Equal(
            11,
            location.GetProperty(
                    "blockIndex")
                .GetInt32());
        Assert.Equal(
            "note-3",
            location.GetProperty(
                    "fragmentId")
                .GetString());

        // No physical page is fabricated for a spine position.
        Assert.False(
            location.TryGetProperty(
                "physicalPageNumber",
                out _));
    }

    [Fact]
    public void Epub_location_without_a_fragment_omits_it()
    {
        Assert.False(
            ElementLocation(
                    Encode(
                        BuildResult(
                            BuildEpubStructure(),
                            new EpubDocumentSourceLocation(
                                0,
                                "OEBPS/chapter-1.xhtml",
                                0))))
                .TryGetProperty(
                    "fragmentId",
                    out _));
    }

    /// <summary>
    /// The end-to-end proof for this boundary: a real EPUB package is extracted
    /// by the production extractor, and the structure and locations it actually
    /// produces are encoded. Nothing about the structure is hand-built.
    /// </summary>
    /// <remarks>
    /// The full host path additionally runs EPUBCheck, which needs a Java
    /// distribution this suite does not assume. The conformance gate is
    /// unrelated to portable serialization, so the proof starts at extraction —
    /// the first step whose output the encoder must carry.
    /// </remarks>
    [Fact]
    public void Extracted_epub_structure_and_locations_encode_losslessly()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create());

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var extracted =
            Assert.IsType<EpubDocumentSourceStructure>(
                evidence.SourceStructure);

        var location =
            evidence.ContentUnits
                .SelectMany(
                    unit =>
                        unit.TextBlocks)
                .Select(
                    block =>
                        block.Location)
                .OfType<EpubDocumentSourceLocation>()
                .First();

        var json =
            Encode(
                BuildResult(
                    extracted,
                    location));

        var structure =
            json.RootElement.GetProperty(
                "sourceStructure");

        Assert.Equal(
            "epub",
            structure.GetProperty(
                    "kind")
                .GetString());
        Assert.Equal(
            extracted.PackagePath,
            structure.GetProperty(
                    "packagePath")
                .GetString());
        Assert.Equal(
            extracted.Title,
            structure.GetProperty(
                    "title")
                .GetString());
        Assert.Equal(
            extracted.Language,
            structure.GetProperty(
                    "language")
                .GetString());
        Assert.Equal(
            extracted.Identifier,
            structure.GetProperty(
                    "identifier")
                .GetString());

        // The spine survives with its order and its resource paths.
        var spineItems =
            structure.GetProperty(
                    "spineItems")
                .EnumerateArray()
                .ToArray();

        Assert.Equal(
            extracted.SpineItems.Count,
            spineItems.Length);

        for (var index = 0; index < spineItems.Length; index++)
        {
            Assert.Equal(
                extracted.SpineItems[index].ResourcePath,
                spineItems[index]
                    .GetProperty(
                        "resourcePath")
                    .GetString());
            Assert.Equal(
                extracted.SpineItems[index].SpineIndex,
                spineItems[index]
                    .GetProperty(
                        "spineIndex")
                    .GetInt32());
        }

        var encodedLocation =
            ElementLocation(
                json);

        Assert.Equal(
            location.ResourcePath,
            encodedLocation.GetProperty(
                    "resourcePath")
                .GetString());
        Assert.Equal(
            location.SpineIndex,
            encodedLocation.GetProperty(
                    "spineIndex")
                .GetInt32());

        // Nowhere along the path is a physical page invented.
        Assert.False(
            structure.TryGetProperty(
                "sourcePhysicalPageCount",
                out _));
        Assert.False(
            encodedLocation.TryGetProperty(
                "physicalPageNumber",
                out _));
    }

    /// <summary>
    /// A visual carries its own EPUB coordinates, distinct from a text
    /// position.
    /// </summary>
    /// <remarks>
    /// This case was missed by the first pass of the encoder and surfaced only
    /// when a real illustrated EPUB traversed the host: a synthetic package
    /// without images never produces it. The fail-closed converter is what made
    /// it visible instead of silently dropping the visual's provenance.
    /// </remarks>
    [Fact]
    public void Epub_visual_location_is_encoded_under_its_own_kind()
    {
        var location =
            ElementLocation(
                Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubVisualSourceLocation(
                            2,
                            "OEBPS/chapter-1.xhtml",
                            "OEBPS/images/figure-1.png",
                            1,
                            "figure-1",
                            true))));

        Assert.Equal(
            "epub-visual",
            location.GetProperty(
                    "kind")
                .GetString());
        Assert.Equal(
            "OEBPS/chapter-1.xhtml",
            location.GetProperty(
                    "contentResourcePath")
                .GetString());
        Assert.Equal(
            "OEBPS/images/figure-1.png",
            location.GetProperty(
                    "imageResourcePath")
                .GetString());
        Assert.Equal(
            1,
            location.GetProperty(
                    "occurrenceIndex")
                .GetInt32());
        Assert.True(
            location.GetProperty(
                    "isAuxiliary")
                .GetBoolean());

        Assert.False(
            location.TryGetProperty(
                "physicalPageNumber",
                out _));
    }

    #endregion

    #region Methods Contract

    [Fact]
    public void Unsupported_structure_fails_closed()
    {
        var exception =
            Assert.Throws<NotSupportedException>(
                () =>
                    Encode(
                        BuildResult(
                            new FutureDocumentSourceStructure(),
                            new PagedDocumentSourceLocation(
                                1))));

        Assert.Contains(
            nameof(FutureDocumentSourceStructure),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_location_fails_closed()
    {
        var exception =
            Assert.Throws<NotSupportedException>(
                () =>
                    Encode(
                        BuildResult(
                            BuildEpubStructure(),
                            new FutureDocumentSourceLocation())));

        Assert.Contains(
            nameof(FutureDocumentSourceLocation),
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Concluded metadata crosses the Manager boundary with its provenance.
    /// </summary>
    /// <remarks>
    /// Provenance is what lets a consumer tell a publisher's own title from an
    /// inferred one. Carrying the value without it would leave the consumer
    /// exactly where it was before: unable to distinguish a real title from a
    /// filename.
    /// </remarks>
    [Fact]
    public void Document_metadata_is_encoded_with_its_provenance()
    {
        var metadata =
            Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubDocumentSourceLocation(
                            0,
                            "OEBPS/chapter-1.xhtml",
                            0),
                        new DocumentMetadata(
                            new DocumentMetadataValue(
                                "Jesus and the Eyewitnesses",
                                DocumentMetadataOrigin.Native,
                                "epub.opf.dc:title"),
                            subtitle:
                                null,
                            description:
                                null,
                            contributors:
                            [
                                new DocumentMetadataContributor(
                                    "Bauckham, Richard",
                                    DocumentMetadataOrigin.Native,
                                    "epub.opf.dc:creator")
                            ],
                            publisher:
                                null,
                            language:
                                new DocumentMetadataValue(
                                    "en",
                                    DocumentMetadataOrigin.Native,
                                    "epub.opf.dc:language"),
                            dates:
                            [
                                new DocumentMetadataDate(
                                    "2017-04-28",
                                    DocumentMetadataDateKind.Unspecified,
                                    DocumentMetadataOrigin.Native,
                                    "epub.opf.dc:date")
                            ])))
                .RootElement
                .GetProperty(
                    "documentMetadata");

        var title =
            metadata.GetProperty(
                "title");

        Assert.Equal(
            "Jesus and the Eyewitnesses",
            title.GetProperty(
                    "value")
                .GetString());
        Assert.Equal(
            "native",
            title.GetProperty(
                    "origin")
                .GetString());
        Assert.Equal(
            "epub.opf.dc:title",
            title.GetProperty(
                    "sourceHint")
                .GetString());

        Assert.Equal(
            "en",
            metadata.GetProperty(
                    "language")
                .GetProperty(
                    "value")
                .GetString());
        Assert.Equal(
            "Bauckham, Richard",
            metadata.GetProperty(
                    "contributors")
                .EnumerateArray()
                .First()
                .GetProperty(
                    "statement")
                .GetString());
        Assert.Equal(
            "unspecified",
            metadata.GetProperty(
                    "dates")
                .EnumerateArray()
                .First()
                .GetProperty(
                    "kind")
                .GetString());

        // Absent values are absent, never a placeholder.
        Assert.False(
            metadata.TryGetProperty(
                "subtitle",
                out _));
        Assert.False(
            metadata.TryGetProperty(
                "publisher",
                out _));
    }

    [Fact]
    public void Schema_version_is_advertised_and_matches_the_payload()
    {
        var encoder =
            new DocumentProcessingResultJsonEncoder();

        Assert.Equal(
            DocumentProcessingResult.SchemaVersionId,
            encoder.SchemaVersion);

        Assert.Equal(
            encoder.SchemaVersion,
            Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubDocumentSourceLocation(
                            0,
                            "OEBPS/chapter-1.xhtml",
                            0)))
                .RootElement
                .GetProperty(
                    "schemaVersion")
                .GetString());
    }

    [Fact]
    public void Source_identity_survives_encoding()
    {
        var source =
            Encode(
                    BuildResult(
                        BuildEpubStructure(),
                        new EpubDocumentSourceLocation(
                            0,
                            "OEBPS/chapter-1.xhtml",
                            0)))
                .RootElement
                .GetProperty(
                    "source");

        Assert.Equal(
            Sha256,
            source.GetProperty(
                    "sha256")
                .GetString());
        Assert.Equal(
            "bauckham.epub",
            source.GetProperty(
                    "fileName")
                .GetString());
    }

    #endregion

    #region Methods Helpers

    private const string Sha256 =
        "9f2c4b8e1d7a306f5c9b2e8a4d1f7c30596b8e2a4d1f7c30596b8e2a4d1f7c30";

    private static JsonDocument Encode(
        DocumentProcessingResult result) =>
        JsonDocument.Parse(
            new DocumentProcessingResultJsonEncoder()
                .Encode(
                    result));

    private static JsonElement ElementLocation(
        JsonDocument json) =>
        json.RootElement
            .GetProperty(
                "elements")
            .EnumerateArray()
            .First()
            .GetProperty(
                "location");

    private static PagedDocumentSourceStructure BuildPagedStructure() =>
        new(
            [
                new PagedDocumentPageDescriptor(
                    physicalPageNumber:
                        1,
                    new NormalizedRectangle(
                        0,
                        0,
                        1,
                        1))
            ]);

    private static EpubDocumentSourceStructure BuildEpubStructure() =>
        new(
            "OEBPS/content.opf",
            [
                new EpubSpineItemDescriptor(
                    0,
                    "cover",
                    "OEBPS/cover.xhtml",
                    "application/xhtml+xml",
                    true),
                new EpubSpineItemDescriptor(
                    1,
                    "chapter-1",
                    "OEBPS/chapter-1.xhtml",
                    "application/xhtml+xml",
                    true)
            ],
            "Jesus and the Eyewitnesses",
            "urn:isbn:9780802874313",
            "en",
            1);

    private static DocumentProcessingResult BuildResult(
        DocumentSourceStructure structure,
        DocumentSourceLocation location,
        DocumentMetadata? documentMetadata = null)
    {
        const string text =
            "Ordered documentary content.";

        var isEpub =
            structure is EpubDocumentSourceStructure;

        var element =
            new DocumentElement(
                elementId:
                    "element-1",
                ordinal:
                    0,
                DocumentElementKind.Text,
                location,
                segmentId:
                    null,
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text));

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    element.ElementId,
                DocumentTextSourceKind.Native,
                selectedSourceText:
                    text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                nativeCandidateSequence:
                    0,
                layoutCandidateSequence:
                    null,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                reconciliationDecision:
                    null,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    null,
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    false,
                exclusionReason:
                    null,
                isResolved:
                    true,
                layoutKind:
                    null);

        return new DocumentProcessingResult(
            new DocumentSourceDescriptor(
                isEpub
                    ? DocumentFormatId.Epub
                    : DocumentFormatId.Pdf,
                Sha256,
                byteLength:
                    1024,
                isEpub
                    ? "bauckham.epub"
                    : "bauckham.pdf"),
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "native",
                        "native-v1"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    [],
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1"),
            [element],
            [evidence],
            structuralSegments:
                [],
            segmentProcessingEvidence:
                [],
            visualAssets:
                [],
            DocumentProcessingQualityObservations.Empty,
            sourceStructure:
                structure,
            notes:
                null,
            documentMetadata);
    }

    private sealed record FutureDocumentSourceStructure
        : DocumentSourceStructure;

    private sealed record FutureDocumentSourceLocation
        : DocumentSourceLocation;

    #endregion
}
