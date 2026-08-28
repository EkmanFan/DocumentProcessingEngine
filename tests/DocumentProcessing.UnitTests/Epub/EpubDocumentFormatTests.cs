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
    public void DocumentFormatOptions_DefaultsDoNotImposeProductSizeBoundaries()
    {
        var options =
            new EpubDocumentFormatOptions();

        Assert.Equal(
            long.MaxValue,
            options.MaximumSourceBytes);

        Assert.Equal(
            int.MaxValue,
            options.MaximumArchiveEntries);

        Assert.Equal(
            long.MaxValue,
            options.MaximumTotalUncompressedBytes);

        Assert.Equal(
            long.MaxValue,
            options.MaximumTextResourceBytes);

        Assert.Equal(
            long.MaxValue,
            options.MaximumVisualResourceBytes);
    }

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
    public void Extractor_RetainsFootnoteAsidesExactlyOnceInReadingOrder()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.Create(
                    includeFootnotes:
                        true));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var blocks =
            evidence.ContentUnits[1]
                .TextBlocks;

        Assert.Equal(
            [
                "Before note.1",
                "1 Inline footnote content.↩",
                "Nested footnote paragraph.",
                "After note."
            ],
            blocks.Select(
                block =>
                    block.SourceText));

        Assert.All(
            blocks,
            block =>
                Assert.Equal(
                    StructuredNativeTextBlockKind.Text,
                    block.Kind));

        Assert.Equal(
            [
                "before-note",
                "inline-note",
                "nested-note",
                "after-note"
            ],
            blocks.Select(
                block =>
                    Assert.IsType<EpubDocumentSourceLocation>(
                            block.Location)
                        .FragmentId));

        var note =
            Assert.IsType<
                DocumentProcessing.Core.Documents.Notes.StructuredNativeDocumentNote>(
                Assert.Single(
                    evidence.DocumentNotes));

        Assert.Equal(
            "1",
            note.Label);

        Assert.Equal(
            "1 Inline footnote content.",
            note.Text);

        Assert.Single(
            note.References);

        Assert.Equal(
            "before-note",
            Assert.IsType<EpubDocumentSourceLocation>(
                    note.References[0].OwnerLocation)
                .FragmentId);

        Assert.Equal(
            "inline-note-ref",
            Assert.IsType<EpubDocumentSourceLocation>(
                    note.References[0].Location)
                .FragmentId);

        Assert.Equal(
            "inline-note",
            Assert.IsType<EpubDocumentSourceLocation>(
                    Assert.Single(
                        note.SourceLocations))
                .FragmentId);
    }

    [Fact]
    public void Extractor_ConcludesCrossResourceEndnoteWithMultipleReferences()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.CreateNotes(
                    """
                    <p id="body-1">First reference<a id="ref-1" epub:type="noteref" href="chapter2.xhtml#endnote-7">7</a>.</p>
                    <p id="body-2">Second reference<a id="ref-2" role="doc-noteref" href="chapter2.xhtml#endnote-7">7</a>.</p>
                    """,
                    """
                    <aside id="endnote-7" role="doc-endnote"><p>Nested endnote payload.</p><a role="doc-backlink" href="chapter1.xhtml#ref-1">back</a></aside>
                    """));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var note =
            Assert.IsType<
                DocumentProcessing.Core.Documents.Notes.StructuredNativeDocumentNote>(
                Assert.Single(
                    evidence.DocumentNotes));

        Assert.Equal(
            "7",
            note.Label);

        Assert.Equal(
            "Nested endnote payload.",
            note.Text);

        Assert.Equal(
            2,
            note.References.Count);

        Assert.All(
            note.References,
            reference =>
                Assert.Equal(
                    "OEBPS/chapter1.xhtml",
                    Assert.IsType<EpubDocumentSourceLocation>(
                            reference.OwnerLocation)
                        .ResourcePath));

        Assert.Equal(
            "OEBPS/chapter2.xhtml",
            Assert.IsType<EpubDocumentSourceLocation>(
                    Assert.Single(
                        note.SourceLocations))
                .ResourcePath);
    }

    [Fact]
    public void Extractor_ReciprocalBacklinksRepairContradictoryForwardTarget()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.CreateNotes(
                    """
                    <p id="body"><span id="marker-21"/><a epub:type="noteref" href="chapter2.xhtml#payload-21">21</a><span id="marker-22"/><a epub:type="noteref" href="chapter2.xhtml#payload-21">22</a></p>
                    """,
                    """
                    <aside id="payload-21" epub:type="footnote"><a href="chapter1.xhtml#marker-21">21</a>. Payload twenty-one.</aside>
                    <p id="payload-22"><a href="chapter1.xhtml#marker-22">22</a>. Payload twenty-two.</p>
                    """));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        var notes =
            evidence.DocumentNotes
                .Cast<DocumentProcessing.Core.Documents.Notes
                    .StructuredNativeDocumentNote>()
                .ToArray();

        Assert.Equal(
            ["21", "22"],
            notes.Select(
                note =>
                    note.Label));

        Assert.Equal(
            ["Payload twenty-one.", "Payload twenty-two."],
            notes.Select(
                note =>
                    note.Text));

        Assert.Equal(
            ["payload-21", "payload-22"],
            notes.Select(
                note =>
                    Assert.IsType<EpubDocumentSourceLocation>(
                            Assert.Single(
                                note.SourceLocations))
                        .FragmentId));

        Assert.Equal(
            2,
            evidence.NotePayloadCandidateLocations.Count);
    }

    [Fact]
    public void Extractor_AmbiguousOrBrokenRelationsRemainOrdinaryContent()
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.CreateNotes(
                    """
                    <p id="body-1">Ambiguous<a epub:type="noteref" href="#duplicate-note">1</a>.</p>
                    <p id="body-2">Broken<a epub:type="noteref" href="#missing-note">2</a>.</p>
                    <p id="body-3">External<a epub:type="noteref" href="https://example.test/note">3</a>.</p>
                    <p id="body-4">Valid owner<a epub:type="noteref" href="#partially-owned-note">4</a>.</p>
                    <a epub:type="noteref" href="#partially-owned-note">4</a>
                    <aside id="duplicate-note" epub:type="footnote">First candidate.</aside>
                    <aside id="duplicate-note" epub:type="footnote">Second candidate.</aside>
                    <aside id="partially-owned-note" epub:type="footnote">Partially owned payload.</aside>
                    <aside id="unreferenced-note" epub:type="footnote">Unreferenced payload.</aside>
                    <aside epub:type="footnote">Missing ID payload.</aside>
                    <aside id="ordinary-aside">Ordinary aside.</aside>
                    """,
                    "<p>Second chapter.</p>"));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        Assert.Empty(
            evidence.DocumentNotes);

        Assert.Equal(
            4,
            evidence.NotePayloadCandidateLocations.Count);

        var text =
            evidence.ContentUnits
                .SelectMany(
                    unit =>
                        unit.TextBlocks)
                .Select(
                    block =>
                        block.SourceText)
                .ToArray();

        Assert.Contains(
            "First candidate.",
            text);

        Assert.Contains(
            "Second candidate.",
            text);

        Assert.Contains(
            "Unreferenced payload.",
            text);

        Assert.Contains(
            "Partially owned payload.",
            text);

        Assert.Contains(
            "Missing ID payload.",
            text);

        Assert.Contains(
            "Ordinary aside.",
            text);
    }

    [Fact]
    public void Extractor_TargetedLocalCorporaConcludeExpectedNoteRelations()
    {
        var repositoryRoot =
            FindRepositoryRoot();

        var controls =
            new[]
            {
                (FileName: "habermas-case-for-resurrection.epub",
                    ExpectedNotes: 478),
                (FileName: "Historical Theology_ An Introduction to Christian Doctrine - Gregg Allison.epub",
                    ExpectedNotes: 4017)
            };

        foreach (var control in
                 controls)
        {
            var path =
                Path.Combine(
                    repositoryRoot,
                    "tests",
                    "document_corpus",
                    "epub",
                    control.FileName);

            if (!File.Exists(
                    path))
            {
                throw Xunit.Sdk.SkipException.ForSkip(
                    $"Targeted EPUB control '{control.FileName}' is unavailable.");
            }

            using var stream =
                File.OpenRead(
                    path);

            var evidence =
                new EpubPackageExtractor()
                    .Extract(
                        stream,
                        new EpubDocumentFormatOptions());

            Assert.Equal(
                control.ExpectedNotes,
                evidence.DocumentNotes.Count);

            if (string.Equals(
                    control.FileName,
                    "habermas-case-for-resurrection.epub",
                    StringComparison.Ordinal))
            {
                Assert.Contains(
                    evidence.DocumentNotes,
                    note =>
                        note.Label ==
                            "21" &&
                        note is DocumentProcessing.Core.Documents.Notes
                            .StructuredNativeDocumentNote structured &&
                        Assert.IsType<EpubDocumentSourceLocation>(
                                Assert.Single(
                                    structured.SourceLocations))
                            .FragmentId ==
                            "a33X");

                Assert.Contains(
                    evidence.DocumentNotes,
                    note =>
                        note.Label ==
                            "22" &&
                        note is DocumentProcessing.Core.Documents.Notes
                            .StructuredNativeDocumentNote structured &&
                        Assert.IsType<EpubDocumentSourceLocation>(
                                Assert.Single(
                                    structured.SourceLocations))
                            .FragmentId ==
                            "a36X");
            }
        }
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
            6,
            visuals.Count);

        var structure =
            Assert.IsType<EpubDocumentSourceStructure>(
                evidence.SourceStructure);

        Assert.Equal(
            1,
            structure.BodyMatterStartSpineIndex);

        Assert.True(
            visuals["OEBPS/images/cover.png"]
                .IsPublicationCover);

        Assert.False(
            visuals["OEBPS/images/diagram.png"]
                .IsPublicationCover);

        Assert.True(
            visuals["OEBPS/images/diagram.png"]
                .IsStructuredFigure);

        Assert.True(
            visuals["OEBPS/images/decoration.png"]
                .IsExplicitlyPresentationOnly);

        Assert.True(
            visuals["OEBPS/images/auxiliary.png"]
                .IsAuxiliary);

        Assert.True(
            visuals["OEBPS/images/front.png"]
                .IsPreliminaryMatter);

        Assert.True(
            visuals["OEBPS/images/diagram.png"]
                .HasBodyMatterBoundary);

        Assert.False(
            visuals["OEBPS/images/diagram.png"]
                .IsPreliminaryMatter);

        Assert.True(
            visuals["OEBPS/images/separator.png"]
                .IsRepeatedPresentationVisual);

        var styledHeading =
            evidence.ContentUnits
                .SelectMany(
                    unit =>
                        unit.TextBlocks)
                .Single(
                    block =>
                        block.Location is
                            EpubDocumentSourceLocation
                            {
                                FragmentId: "styled-heading"
                            });

        Assert.Equal(
            StructuredNativeTextBlockKind.Heading,
            styledHeading.Kind);

        Assert.All(
            evidence.Visuals,
            visual =>
                Assert.IsType<EpubVisualSourceLocation>(
                    visual.Location));

        Assert.DoesNotContain(
            "OEBPS/images/unused.png",
            visuals.Keys);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Extractor_ExcludesTerminalPresentationMatter(
        bool promotional,
        int expectedVisualCount)
    {
        using var stream =
            new MemoryStream(
                TestEpubFactory.CreateWithTerminalPresentation(
                    promotional));

        var evidence =
            new EpubPackageExtractor()
                .Extract(
                    stream,
                    new EpubDocumentFormatOptions());

        Assert.Equal(
            expectedVisualCount,
            evidence.Visuals.Count);

        Assert.All(
            evidence.Visuals,
            visual =>
                Assert.True(
                    visual.IsTerminalPresentationMatter));

        Assert.True(
            evidence.ContentUnits[1]
                .IsPresentationOnly);

        if (promotional)
        {
            Assert.NotEmpty(
                evidence.ContentUnits[1]
                    .TextBlocks);
        }
        else
        {
            Assert.Empty(
                evidence.ContentUnits[1]
                    .TextBlocks);
        }

        var chapterHeading =
            Assert.Single(
                evidence.ContentUnits[0]
                    .TextBlocks,
                block =>
                    block.Kind ==
                    StructuredNativeTextBlockKind.Heading);

        Assert.Equal(
            "Styled chapter title",
            chapterHeading.SourceText);
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

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new InvalidOperationException(
            "DocumentProcessingEngine repository root could not be located.");
    }

    #endregion
}
