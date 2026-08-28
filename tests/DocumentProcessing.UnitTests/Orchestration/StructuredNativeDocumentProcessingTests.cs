using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Epub.Locations;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class StructuredNativeDocumentProcessingTests
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity LayoutIdentity =
        new(
            "unused-layout",
            "unused-layout-v1");

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessDocumentAsync_StructuredEvidenceProjectsNonPagedPortableResult()
    {
        var structure =
            new EpubDocumentSourceStructure(
                "OEBPS/content.opf",
                [
                    new EpubSpineItemDescriptor(
                        0,
                        "chapter-1",
                        "OEBPS/chapter1.xhtml",
                        "application/xhtml+xml",
                        isLinear:
                            true)
                ],
                title:
                    "Test Book");

        var evidence =
            new StructuredNativeDocumentEvidence(
                structure,
                [
                    new StructuredNativeContentUnit(
                        "OEBPS/chapter1.xhtml",
                        [
                            Block(
                                StructuredNativeTextBlockKind.Heading,
                                0,
                                "  Chapter   One "),
                            Block(
                                StructuredNativeTextBlockKind.Text,
                                1,
                                "First\nparagraph."),
                            Block(
                                StructuredNativeTextBlockKind.Caption,
                                2,
                                "A caption.")
                        ]),
                    new StructuredNativeContentUnit(
                        "OEBPS/promotions.xhtml",
                        [
                            Block(
                                StructuredNativeTextBlockKind.Heading,
                                3,
                                "Promotional heading")
                        ],
                        isPresentationOnly:
                            true)
                ],
                new ProcessingComponentIdentity(
                    "test-epub",
                    "test-epub-native-v1"));

        var engine =
            new DocumentProcessingEngine(
                [
                    new StubStructuredFormat(
                        evidence)
                ],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                "structured source"u8.ToArray());

        var result =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream,
                    "book.epub",
                    "application/epub+zip"));

        Assert.Equal(
            DocumentFormatId.Epub,
            result.Source.Format);

        Assert.Same(
            structure,
            result.SourceStructure);

        Assert.Equal(
            [
                DocumentElementKind.Heading,
                DocumentElementKind.Text,
                DocumentElementKind.Caption
            ],
            result.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Equal(
            "Chapter One",
            result.Elements[0].Text);

        Assert.Equal(
            "First paragraph.",
            result.Elements[1].Text);

        Assert.All(
            result.Elements,
            element =>
                Assert.IsType<EpubDocumentSourceLocation>(
                    element.Location));

        var segment =
            Assert.Single(
                result.StructuralSegments);

        Assert.Equal(
            "Chapter One",
            segment.HeadingText);

        Assert.Equal(
            "Chapter One\n\nFirst paragraph.\n\nA caption.",
            segment.Text);

        Assert.All(
            result.ElementProcessingEvidence,
            item =>
                Assert.Equal(
                    DocumentTextSourceKind.Native,
                    item.TextSource));

        Assert.Null(
            result.ProcessingManifest.Rasterization);

        Assert.Null(
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Empty(
            result.ProcessingManifest.Ocr);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ConcludedStructuredNoteIsProjectedOutsideNarrativeFlow()
    {
        var referenceOwnerLocation =
            new EpubDocumentSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                0,
                "paragraph-1");

        var payloadLocation =
            new EpubDocumentSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                1,
                "note-1");

        var evidence =
            new StructuredNativeDocumentEvidence(
                new EpubDocumentSourceStructure(
                    "OEBPS/content.opf",
                    [
                        new EpubSpineItemDescriptor(
                            0,
                            "chapter-1",
                            "OEBPS/chapter1.xhtml",
                            "application/xhtml+xml",
                            isLinear:
                                true)
                    ]),
                [
                    new StructuredNativeContentUnit(
                        "OEBPS/chapter1.xhtml",
                        [
                            new StructuredNativeTextBlock(
                                StructuredNativeTextBlockKind.Text,
                                referenceOwnerLocation,
                                "Body text.1"),
                            new StructuredNativeTextBlock(
                                StructuredNativeTextBlockKind.Text,
                                payloadLocation,
                                "1 Note payload.")
                        ])
                ],
                new ProcessingComponentIdentity(
                    "test-epub",
                    "test-epub-native-v1"),
                visuals:
                    null,
                documentNotes:
                    [
                        new StructuredNativeDocumentNote(
                            "1",
                            "1 Note payload.",
                            [
                                new StructuredNativeNoteReference(
                                    referenceOwnerLocation,
                                    new EpubDocumentSourceLocation(
                                        0,
                                        "OEBPS/chapter1.xhtml",
                                        0,
                                        "note-ref-1"))
                            ],
                            [payloadLocation])
                    ]);

        var engine =
            new DocumentProcessingEngine(
                [new StubStructuredFormat(evidence)],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                "structured source"u8.ToArray());

        var result =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream,
                    "book.epub",
                    "application/epub+zip"));

        var note =
            Assert.Single(
                result.Notes);

        Assert.Equal(
            "1 Note payload.",
            note.Text);

        var payloadElement =
            Assert.Single(
                result.Elements,
                element =>
                    Equals(
                        element.Location,
                        payloadLocation));

        Assert.Null(
            payloadElement.SegmentId);

        Assert.Equal(
            DocumentBlockExclusionReason.NoteContent,
            Assert.Single(
                    result.ElementProcessingEvidence,
                    item =>
                        item.ElementId ==
                        payloadElement.ElementId)
                .ExclusionReason);

        Assert.DoesNotContain(
            "Note payload",
            Assert.Single(
                    result.StructuralSegments)
                .Text,
            StringComparison.Ordinal);

        Assert.Equal(
            result.Elements[0].ElementId,
            Assert.Single(
                    note.References)
                .Provenance.ElementId);
    }

    [Fact]
    public async Task ProcessDocumentAsync_UnresolvedNotePayloadCandidateRemainsAuditableButNonNarrative()
    {
        var bodyLocation =
            new EpubDocumentSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                0,
                "body");

        var payloadLocation =
            new EpubDocumentSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                1,
                "unresolved-note");

        var evidence =
            new StructuredNativeDocumentEvidence(
                new EpubDocumentSourceStructure(
                    "OEBPS/content.opf",
                    [
                        new EpubSpineItemDescriptor(
                            0,
                            "chapter-1",
                            "OEBPS/chapter1.xhtml",
                            "application/xhtml+xml",
                            isLinear:
                                true)
                    ]),
                [
                    new StructuredNativeContentUnit(
                        "OEBPS/chapter1.xhtml",
                        [
                            new StructuredNativeTextBlock(
                                StructuredNativeTextBlockKind.Text,
                                bodyLocation,
                                "Narrative body."),
                            new StructuredNativeTextBlock(
                                StructuredNativeTextBlockKind.Text,
                                payloadLocation,
                                "Unresolved note payload.")
                        ])
                ],
                new ProcessingComponentIdentity(
                    "test-epub",
                    "test-epub-native-v1"),
                visuals:
                    null,
                documentNotes:
                    [],
                notePayloadCandidateLocations:
                    [payloadLocation]);

        var engine =
            new DocumentProcessingEngine(
                [new StubStructuredFormat(evidence)],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                "structured source"u8.ToArray());

        var result =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream,
                    "book.epub",
                    "application/epub+zip"));

        Assert.Empty(
            result.Notes);

        var payloadElement =
            Assert.Single(
                result.Elements,
                element =>
                    Equals(
                        element.Location,
                        payloadLocation));

        Assert.Null(
            payloadElement.SegmentId);

        Assert.Equal(
            "Unresolved note payload.",
            payloadElement.Text);

        Assert.Equal(
            DocumentBlockExclusionReason.NoteContent,
            Assert.Single(
                    result.ElementProcessingEvidence,
                    item =>
                        item.ElementId ==
                        payloadElement.ElementId)
                .ExclusionReason);

        Assert.Equal(
            "Narrative body.",
            Assert.Single(
                    result.StructuralSegments)
                .Text);
    }

    [Fact]
    public async Task ProcessDocumentAsync_SelectedNativeVisualUsesUserWriterAndPortableCustody()
    {
        var visualBytes =
            new byte[] { 10, 20, 30, 40, 50 };

        var visualLocation =
            new EpubVisualSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                "OEBPS/images/diagram.png",
                0,
                "diagram",
                isAuxiliary:
                    false);

        var evidence =
            new StructuredNativeDocumentEvidence(
                new EpubDocumentSourceStructure(
                    "OEBPS/content.opf",
                    [
                        new EpubSpineItemDescriptor(
                            0,
                            "chapter-1",
                            "OEBPS/chapter1.xhtml",
                            "application/xhtml+xml",
                            isLinear:
                                true)
                    ]),
                [],
                new ProcessingComponentIdentity(
                    "test-epub",
                    "test-epub-native-v1"),
                [
                    new StructuredNativeVisual(
                        "structured-visual-cover",
                        visualLocation,
                        "OEBPS/images/cover.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isPublicationCover:
                            true),
                    new StructuredNativeVisual(
                        "structured-visual-000001",
                        visualLocation,
                        "OEBPS/images/diagram.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isStructuredFigure:
                            true),
                    new StructuredNativeVisual(
                        "structured-visual-front",
                        visualLocation,
                        "OEBPS/images/title-page.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isPreliminaryMatter:
                            true,
                        hasBodyMatterBoundary:
                            true),
                    new StructuredNativeVisual(
                        "structured-visual-decoration",
                        visualLocation,
                        "OEBPS/images/decoration.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isExplicitlyPresentationOnly:
                            true),
                    new StructuredNativeVisual(
                        "structured-visual-separator",
                        visualLocation,
                        "OEBPS/images/separator.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isRepeatedPresentationVisual:
                            true),
                    new StructuredNativeVisual(
                        "structured-visual-promotion",
                        visualLocation,
                        "OEBPS/images/promotion.png",
                        "image/png",
                        isAuxiliary:
                            false,
                        isTerminalPresentationMatter:
                            true)
                ]);

        UserVisualAssetWriteRequest? observedRequest =
            null;

        var writerCalls =
            0;

        var fallbackWriterCalls =
            0;

        var destination =
            new MemoryStream();

        var engine =
            new DocumentProcessingEngine(
                [
                    new StubStructuredFormat(
                        evidence,
                        visualBytes)
                ],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity,
                userVisualAssetWriter:
                    (_, _, _) =>
                    {
                        fallbackWriterCalls++;

                        return ValueTask.FromResult<Stream>(
                            new MemoryStream());
                    });

        await using var stream =
            new MemoryStream(
                "structured source"u8.ToArray());

        var result =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream),
                new DocumentProcessingRequestOptions(
                    qualifyUnresolvedVisuals:
                        true,
                    userVisualAssetWriter:
                        (_, request, _) =>
                        {
                            writerCalls++;

                            observedRequest =
                                request;

                            return ValueTask.FromResult<Stream>(
                                destination);
                        }));

        var request =
            Assert.IsType<UserSourceVisualAssetWriteRequest>(
                observedRequest);

        Assert.Equal(
            "OEBPS/images/diagram.png",
            request.SourceResourceId);

        Assert.Equal(
            DocumentVisualQualification.Meaningful,
            request.Qualification);

        Assert.Equal(
            1,
            writerCalls);

        Assert.Equal(
            0,
            fallbackWriterCalls);

        Assert.Same(
            visualLocation,
            request.Location);

        var element =
            Assert.Single(
                result.Elements);

        Assert.Equal(
            DocumentElementKind.Visual,
            element.Kind);

        var asset =
            Assert.Single(
                result.VisualAssets);

        Assert.Equal(
            element.ElementId,
            asset.ElementId);

        Assert.Null(
            asset.RasterDerivation);

        Assert.Equal(
            DocumentVisualQualification.Meaningful,
            asset.Qualification);

        Assert.Equal(
            visualBytes,
            destination.ToArray());

        Assert.Contains(
            "test-structured-visual-raw-v1",
            result.ProcessingManifest.VisualPreservationProfileIds);

        Assert.Null(
            result.ProcessingManifest.LayoutAnalysis);

        await destination.DisposeAsync();
    }

    [Fact]
    public async Task ProcessDocumentAsync_UnknownVisualUsesPaddleOnlyWhenUserEnablesIt()
    {
        var pngBytes =
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        var visual =
            new StructuredNativeVisual(
                "structured-visual-unknown",
                new EpubVisualSourceLocation(
                    0,
                    "OEBPS/chapter.xhtml",
                    "OEBPS/images/unknown.png",
                    0,
                    fragmentId:
                        null,
                    isAuxiliary:
                        false),
                "OEBPS/images/unknown.png",
                "image/png",
                isAuxiliary:
                    false);

        var evidence =
            new StructuredNativeDocumentEvidence(
                new EpubDocumentSourceStructure(
                    "OEBPS/content.opf",
                    [
                        new EpubSpineItemDescriptor(
                            0,
                            "chapter",
                            "OEBPS/chapter.xhtml",
                            "application/xhtml+xml",
                            isLinear:
                                true)
                    ]),
                [],
                new ProcessingComponentIdentity(
                    "test-epub",
                    "test-epub-native-v1"),
                [
                    visual
                ]);

        var unqualifiedDestination =
            new MemoryStream();

        var defaultEngine =
            new DocumentProcessingEngine(
                [
                    new StubStructuredFormat(
                        evidence,
                        pngBytes)
                ],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity,
                userVisualAssetWriter:
                    (_, request, _) =>
                    {
                        Assert.Equal(
                            DocumentVisualQualification.Unqualified,
                            Assert.IsType<UserSourceVisualAssetWriteRequest>(
                                    request)
                                .Qualification);

                        return ValueTask.FromResult<Stream>(
                            unqualifiedDestination);
                    });

        await using (var defaultStream =
                     new MemoryStream(
                         "structured source"u8.ToArray()))
        {
            var defaultResult =
                await defaultEngine.ProcessDocumentAsync(
                    new DocumentSource(
                        defaultStream));

            Assert.Equal(
                DocumentVisualQualification.Unqualified,
                Assert.Single(
                        defaultResult.VisualAssets)
                    .Qualification);

            Assert.Null(
                defaultResult.ProcessingManifest.LayoutAnalysis);
        }

        await unqualifiedDestination.DisposeAsync();

        var layoutAnalyzer =
            new RecordingFigureLayoutAnalyzer();

        var destination =
            new MemoryStream();

        var engine =
            new DocumentProcessingEngine(
                [
                    new StubStructuredFormat(
                        evidence,
                        pngBytes)
                ],
                layoutAnalyzer,
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity,
                userVisualAssetWriter:
                    (_, request, _) =>
                    {
                        Assert.Equal(
                            DocumentVisualQualification.Meaningful,
                            Assert.IsType<UserSourceVisualAssetWriteRequest>(
                                    request)
                                .Qualification);

                        return ValueTask.FromResult<Stream>(
                            destination);
                    });

        await using var stream =
            new MemoryStream(
                "structured source"u8.ToArray());

        var result =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream),
                new DocumentProcessingRequestOptions(
                    qualifyUnresolvedVisuals:
                        true));

        Assert.Equal(
            1,
            layoutAnalyzer.CallCount);

        Assert.Equal(
            DocumentVisualQualification.Meaningful,
            Assert.Single(
                    result.VisualAssets)
                .Qualification);

        Assert.Equal(
            LayoutIdentity,
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Equal(
            pngBytes,
            destination.ToArray());

        await destination.DisposeAsync();
    }

    #endregion

    #region Methods Fixtures

    private static StructuredNativeTextBlock Block(
        StructuredNativeTextBlockKind kind,
        int blockIndex,
        string text) =>
        new(
            kind,
            new EpubDocumentSourceLocation(
                0,
                "OEBPS/chapter1.xhtml",
                blockIndex),
            text);

    #endregion

    #region Test Types

    private sealed class StubStructuredFormat(
        StructuredNativeDocumentEvidence evidence,
        byte[]? visualBytes = null)
        : IDocumentFormat,
          IStructuredNativeVisualMaterializer
    {
        public DocumentFormatId Format =>
            DocumentFormatId.Epub;

        public ValueTask<NativeEvidenceExtractionResult>
            TryExtractNativeEvidenceAsync(
                DocumentSource source,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<NativeEvidenceExtractionResult>(
                new NativeEvidenceExtractionResult.Success(
                    evidence));
        }

        public bool CanMaterialize(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Epub;

        public async ValueTask<StructuredNativeVisualMaterialization>
            MaterializeAsync(
                DocumentSource source,
                DocumentFormatId format,
                StructuredNativeVisual visual,
                Stream destination,
                CancellationToken cancellationToken = default)
        {
            var content =
                visualBytes ??
                throw new InvalidOperationException(
                    "No structured visual bytes were configured for this test.");

            await destination.WriteAsync(
                content,
                cancellationToken);

            return new StructuredNativeVisualMaterialization(
                "test-structured-visual-raw-v1",
                visual.MediaType,
                content.Length,
                Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            content))
                    .ToLowerInvariant());
        }
    }

    private sealed class UnexpectedLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Layout analysis must not run for structured native evidence.");
    }

    private sealed class RecordingFigureLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        public int CallCount { get; private set; }

        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return ValueTask.FromResult(
                new LayoutAnalysisResult(
                    "paddle-test",
                    physicalPageNumber,
                    [
                        new LayoutObservation(
                            physicalPageNumber,
                            observationSequence:
                                0,
                            readingOrder:
                                0,
                            LayoutObservationKind.Figure,
                            new NormalizedRectangle(
                                0,
                                0,
                                1,
                                1))
                    ]));
        }
    }

    private sealed class UnexpectedTextRecognizer
        : IRegionTextRecognizer
    {
        public ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "OCR must not run for structured native evidence.");
    }

    #endregion
}
