using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Epub;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Epub.Locations;
using DocumentProcessing.Epub.Recognition;

namespace DocumentProcessing.UnitTests.Epub;

public sealed class EpubDocumentFormatTests
{
    #region Methods Tests

    [Fact]
    public void Recognizer_ValidContainerSignatureRecognizesEpubAndRestoresPosition()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create());

        stream.Position =
            7;

        var recognized =
            new EpubFormatRecognizer()
                .IsRecognized(
                    new DocumentSource(
                        stream));

        Assert.True(
            recognized);

        Assert.Equal(
            7,
            stream.Position);
    }

    [Fact]
    public void Recognizer_NonZipIsNotRecognized()
    {
        using var stream =
            new MemoryStream(
                "not an epub"u8.ToArray());

        Assert.False(
            new EpubFormatRecognizer()
                .IsRecognized(
                    new DocumentSource(
                        stream)));
    }

    [Fact]
    public void Extractor_AcquiresPackageSpineMetadataAndOrderedTextBlocks()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create());

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var structure =
            Assert.IsType<EpubDocumentSourceStructure>(
                evidence.SourceStructure);

        Assert.Equal(
            "OEBPS/content.opf",
            structure.PackagePath);

        Assert.Equal(
            "Test Book",
            structure.Title);

        Assert.Equal(
            "urn:test:book",
            structure.Identifier);

        Assert.Equal(
            "en",
            structure.Language);

        Assert.Equal(
            2,
            structure.SpineItems.Count);

        Assert.Equal(
            [
                "OEBPS/chapter1.xhtml",
                "OEBPS/chapter2.xhtml"
            ],
            structure.SpineItems
                .Select(
                    item =>
                        item.ResourcePath));

        Assert.Equal(
            2,
            evidence.ContentUnits.Count);

        Assert.Equal(
            [
                StructuredNativeTextBlockKind.Heading,
                StructuredNativeTextBlockKind.Text,
                StructuredNativeTextBlockKind.Caption
            ],
            evidence.ContentUnits[0]
                .TextBlocks
                .Select(
                    block =>
                        block.Kind));

        var firstLocation =
            Assert.IsType<EpubDocumentSourceLocation>(
                evidence.ContentUnits[0]
                    .TextBlocks[0]
                    .Location);

        Assert.Equal(
            0,
            firstLocation.SpineIndex);

        Assert.Equal(
            "heading-1",
            firstLocation.FragmentId);

        Assert.DoesNotContain(
            evidence.ContentUnits
                .SelectMany(
                    unit =>
                        unit.TextBlocks),
            block =>
                block.Location is
                    DocumentProcessing.Core.Locations.PagedDocumentSourceLocation);
    }

    [Fact]
    public void Extractor_RejectsArchivePathEscapingPublicationRoot()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create(
                    includeUnsafeEntry:
                        true));

        Assert.Throws<InvalidDataException>(
            () =>
                new EpubPackageExtractor()
                    .Extract(
                        stream,
                        new EpubDocumentFormatOptions()));
    }

    [Fact]
    public void Extractor_AcquiresReferencedImageFactsAndRetainsAuxiliaryUsage()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create(
                    includeVisuals:
                        true));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var visuals =
            evidence.Visuals.ToDictionary(
                visual =>
                    visual.SourceResourceId,
                StringComparer.Ordinal);

        Assert.Equal(
            4,
            visuals.Count);

        Assert.True(
            visuals["OEBPS/images/cover.png"]
                .IsPublicationCover);

        Assert.False(
            visuals["OEBPS/images/diagram.png"]
                .IsPublicationCover);

        Assert.True(
            visuals["OEBPS/images/decoration.png"]
                .IsExplicitlyPresentationOnly);

        Assert.True(
            visuals["OEBPS/images/auxiliary.png"]
                .IsAuxiliary);

        Assert.All(
            evidence.Visuals,
            visual =>
                Assert.IsType<EpubVisualSourceLocation>(
                    visual.Location));

        Assert.DoesNotContain(
            "OEBPS/images/unused.png",
            visuals.Keys);
    }

    [Fact]
    public async Task VisualMaterializer_CopiesExactPackagedBytesAndRestoresSourcePosition()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create(
                    includeVisuals:
                        true));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var visual =
            evidence.Visuals.Single(
                candidate =>
                    candidate.SourceResourceId ==
                    "OEBPS/images/diagram.png");

        stream.Position =
            11;

        await using var destination =
            new MemoryStream();

        var result =
            await ((IStructuredNativeVisualMaterializer)
                    new EpubDocumentFormat())
                .MaterializeAsync(
                    new DocumentSource(
                        stream),
                    DocumentFormatId.Epub,
                    visual,
                    destination);

        Assert.Equal(
            new byte[] { 10, 20, 30, 40, 50 },
            destination.ToArray());

        Assert.Equal(
            "epub-package-image-raw-v1",
            result.ProfileId);

        Assert.Equal(
            11,
            stream.Position);
    }

    [Fact]
    public async Task DocumentFormat_MissingEpubCheckReturnsConsumerSafeUnavailableResult()
    {
        var missingDistribution =
            Path.Combine(
                Path.GetTempPath(),
                "missing-epubcheck",
                Guid.NewGuid()
                    .ToString(
                        "N"));

        var format =
            new EpubDocumentFormat(
                new EpubDocumentFormatOptions(
                    new EpubCheckOptions(
                        missingDistribution)));

        await using var stream =
            new MemoryStream(
                TestEpubFactory.Create());

        var result =
            await format.TryExtractNativeEvidenceAsync(
                new DocumentSource(
                    stream,
                    "book.epub",
                    "application/epub+zip"));

        var unavailable =
            Assert.IsType<NativeEvidenceExtractionResult.Unavailable>(
                result);

        Assert.Equal(
            "La validation EPUB est temporairement indisponible.",
            unavailable.Reason);

        Assert.DoesNotContain(
            "java",
            unavailable.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentFormat_SourceAboveConfiguredBoundaryReturnsInvalidBeforeChecker()
    {
        var format =
            new EpubDocumentFormat(
                new EpubDocumentFormatOptions(
                    new EpubCheckOptions(
                        Path.Combine(
                            Path.GetTempPath(),
                            "missing-epubcheck")),
                    maximumSourceBytes:
                        1));

        await using var stream =
            new MemoryStream(
                TestEpubFactory.Create());

        var result =
            await format.TryExtractNativeEvidenceAsync(
                new DocumentSource(
                    stream));

        var invalid =
            Assert.IsType<NativeEvidenceExtractionResult.Invalid>(
                result);

        Assert.Equal(
            "Le fichier EPUB dépasse la taille maximale prise en charge.",
            invalid.Reason);
    }

    #endregion
}
