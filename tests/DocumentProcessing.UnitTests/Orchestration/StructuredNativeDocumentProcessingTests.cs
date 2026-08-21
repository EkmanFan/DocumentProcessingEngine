using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;
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
                        ])
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
        StructuredNativeDocumentEvidence evidence)
        : IDocumentFormat
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
                new NativeEvidenceExtractionResult.StructuredSuccess(
                    evidence));
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
