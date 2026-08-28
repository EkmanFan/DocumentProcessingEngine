using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessingEngineOwnedPathTests
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        NativeIdentity =
            new(
                "fake-native",
                "fake-native-v1");

    private static readonly ProcessingComponentIdentity
        LayoutIdentity =
            new(
                "fake-layout",
                "fake-layout-v1");

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessDocumentAsync_OwnsSelectionAndProcessesPreacquiredNativeEvidence()
    {
        var format =
            new StubDocumentFormat(
                new NativeEvidenceExtractionResult.Success(
                    CreateNativeEvidence()));

        var engine =
            new DocumentProcessingEngine(
                [format],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                "%PDF-engine-owned-path"u8.ToArray(),
                writable:
                    false);

        var result =
            await engine
                .ProcessDocumentAsync(
                    new DocumentSource(
                        stream,
                        "engine-owned.pdf",
                        "application/pdf"));

        Assert.Equal(
            DocumentFormatId.Pdf,
            result.Source.Format);

        Assert.Equal(
            NativeIdentity,
            result.ProcessingManifest.NativeExtraction);

        Assert.Null(
            result.ProcessingManifest.Rasterization);

        Assert.Null(
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Equal(
            2,
            result.Elements.Count);

        Assert.Equal(
            1,
            format.AcquisitionCallCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ConsumesAdapterConcludedNotes()
    {
        var format =
            new StubDocumentFormat(
                new NativeEvidenceExtractionResult.Success(
                    CreateNativeEvidence(
                        [
                            CreateNativeNote()
                        ])));

        var engine =
            new DocumentProcessingEngine(
                [format],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                "%PDF-engine-note-path"u8.ToArray(),
                writable:
                    false);

        var result =
            await engine
                .ProcessDocumentAsync(
                    new DocumentSource(
                        stream,
                        "engine-note.pdf",
                        "application/pdf"));

        var note =
            Assert.Single(
                result.Notes);

        Assert.Equal(
            "1",
            note.Label);

        Assert.Equal(
            "Beta native paragraph.",
            note.Text);

        var payloadElement =
            Assert.Single(
                result.Elements,
                element =>
                    Assert.IsType<PagedDocumentSourceLocation>(
                            element.Location)
                        .PhysicalPageNumber ==
                    2);

        var payloadEvidence =
            Assert.Single(
                result.ElementProcessingEvidence,
                evidence =>
                    evidence.ElementId ==
                    payloadElement.ElementId);

        Assert.Equal(
            DocumentBlockExclusionReason.NoteContent,
            payloadEvidence.ExclusionReason);

        var reference =
            Assert.Single(
                note.References);

        Assert.Contains(
            result.Elements,
            element =>
                Assert.IsType<PagedDocumentSourceLocation>(
                        element.Location)
                    .PhysicalPageNumber ==
                    1 &&
                element.ElementId ==
                    reference.Provenance.ElementId);
    }

    [Fact]
    public async Task ProcessDocumentAsync_NoFormatRecognition_FailsClosed()
    {
        var format =
            new StubDocumentFormat(
                new NativeEvidenceExtractionResult.NotRecognized());

        var engine =
            new DocumentProcessingEngine(
                [format],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                [1, 2, 3],
                writable:
                    false);

        var exception =
            await Assert.ThrowsAsync<DocumentFormatSelectionException>(
                () =>
                    engine.ProcessDocumentAsync(
                        new DocumentSource(
                            stream)));

        Assert.Contains(
            "not supported",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            1,
            format.AcquisitionCallCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_FormatUnavailable_ExposesOnlyConsumerSafeReason()
    {
        const string publicReason =
            "La validation EPUB est temporairement indisponible.";

        var format =
            new StubDocumentFormat(
                new NativeEvidenceExtractionResult.Unavailable(
                    publicReason));

        var engine =
            new DocumentProcessingEngine(
                [format],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                [1, 2, 3],
                writable:
                    false);

        var exception =
            await Assert.ThrowsAsync<DocumentFormatSelectionException>(
                () =>
                    engine.ProcessDocumentAsync(
                        new DocumentSource(
                            stream)));

        Assert.Equal(
            publicReason,
            exception.Message);

        Assert.DoesNotContain(
            "java",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "epubcheck.jar",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ConsumerSafeInvalidReasonIsNotPrefixed()
    {
        const string publicReason =
            "Le fichier EPUB n’est pas conforme.";

        var format =
            new StubDocumentFormat(
                new NativeEvidenceExtractionResult.Invalid(
                    publicReason,
                    isConsumerSafeReason:
                        true));

        var engine =
            new DocumentProcessingEngine(
                [format],
                new UnexpectedLayoutAnalyzer(),
                new UnexpectedTextRecognizer(),
                "test-engine-v1",
                LayoutIdentity);

        await using var stream =
            new MemoryStream(
                [1, 2, 3],
                writable:
                    false);

        var exception =
            await Assert.ThrowsAsync<DocumentFormatSelectionException>(
                () =>
                    engine.ProcessDocumentAsync(
                        new DocumentSource(
                            stream)));

        Assert.Equal(
            publicReason,
            exception.Message);
    }

    #endregion

    #region Methods Fixtures

    private static PagedNativeDocumentEvidence CreateNativeEvidence(
        IReadOnlyList<NativeDocumentNote>? documentNotes = null)
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    CreatePage(
                        1,
                        "Alpha native paragraph."),
                    CreatePage(
                        2,
                        "Beta native paragraph.")
                ]);

        var coordinated =
            new DocumentExtractionWithRasterObservationsResult(
                extraction,
                extraction.Pages
                    .Select(
                        page =>
                            new PageVisualRasterObservations(
                                page.PhysicalPageNumber,
                                [])),
                rasterObservationFailure:
                    null);

        return new PagedNativeDocumentEvidence(
            coordinated,
            NativeIdentity,
            documentNotes ??
            []);
    }

    private static PagedNativeDocumentNote CreateNativeNote() =>
        new(
            "1",
            [
                new PagedNativeNoteReference(
                    "1",
                    physicalPageNumber:
                        1,
                    sourceBlockSequence:
                        0,
                    wordSourceSequence:
                        0,
                    new NormalizedRectangle(
                        0.10,
                        0.20,
                        0.14,
                        0.24))
            ],
            [
                new PagedNativeNotePayloadLine(
                    physicalPageNumber:
                        2,
                    text:
                        "Beta native paragraph.",
                    new NormalizedRectangle(
                        0.10,
                        0.20,
                        0.90,
                        0.40),
                    sourceBlockSequences:
                        [0],
                    wordSourceSequences:
                        [0, 1, 2])
            ],
            [
                new PagedNativeNoteSourceBlock(
                    physicalPageNumber:
                        2,
                    sourceSequence:
                        0)
            ]);

    private static DocumentExtractionPage CreatePage(
        int physicalPageNumber,
        string text)
    {
        var tokens =
            text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var words =
            tokens
                .Select(
                    (token, index) =>
                        new DocumentWord(
                            index,
                            token,
                            new NormalizedRectangle(
                                0.10 +
                                index *
                                0.05,
                                0.20,
                                0.14 +
                                index *
                                0.05,
                                0.24),
                            "Body",
                            10))
                .ToArray();

        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.90,
                    0.40),
                words,
                dominantFontName:
                    "Body",
                medianPointSize:
                    10,
                lineCount:
                    1);

        return new DocumentExtractionPage(
            physicalPageNumber,
            text,
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
            wordCount:
                words.Length,
            rasterImageCount:
                0,
            largestRasterImageAreaRatio:
                0,
            sourceWidth:
                612,
            sourceHeight:
                792,
            words,
            blocks:
                [block]);
    }

    #endregion

    #region Test Types

    private sealed class StubDocumentFormat(
        NativeEvidenceExtractionResult outcome)
        : IDocumentFormat
    {
        public DocumentFormatId Format =>
            DocumentFormatId.Pdf;

        public int AcquisitionCallCount { get; private set; }

        public ValueTask<NativeEvidenceExtractionResult>
            TryExtractNativeEvidenceAsync(
                DocumentSource source,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            AcquisitionCallCount++;

            return ValueTask.FromResult(
                outcome);
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
                "Layout analysis must not run for healthy native-only evidence.");
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
                "OCR must not run for healthy native-only evidence.");
    }

    #endregion
}
