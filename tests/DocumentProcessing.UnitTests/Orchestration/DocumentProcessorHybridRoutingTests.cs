using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessorHybridRoutingTests
{
    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    private static readonly ProcessingComponentIdentity LayoutIdentity =
        new(
            "fake-layout",
            "fake-layout-profile-v1");

    private static readonly ProcessingComponentIdentity ReconciliationIdentity =
        new(
            "native-ocr-text-reconciler",
            "native-ocr-reconciliation-v1");

    #region Route composition

    [Fact]
    public async Task ProcessAsync_MixedRoutes_ExecutesAllRoutesWithOneRasterSession()
    {
        var extraction =
            MixedExtraction();

        var rasterizer =
            new FakeDocumentRasterizer();

        var layoutAnalyzer =
            new FakePageLayoutAnalyzer(
                new Dictionary<int, IReadOnlyList<LayoutObservation>>
                {
                    [2] =
                    [
                        Layout(
                            2,
                            0,
                            0,
                            LayoutObservationKind.Text,
                            0.10,
                            0.10,
                            0.50,
                            0.25),
                        Layout(
                            2,
                            1,
                            1,
                            LayoutObservationKind.Figure,
                            0.60,
                            0.10,
                            0.90,
                            0.40),
                        Layout(
                            2,
                            2,
                            2,
                            LayoutObservationKind.Caption,
                            0.62,
                            0.42,
                            0.88,
                            0.47)
                    ],
                    [3] =
                    [
                        Layout(
                            3,
                            0,
                            0,
                            LayoutObservationKind.Text,
                            0.10,
                            0.10,
                            0.55,
                            0.25)
                    ]
                });

        var recognizer =
            new FakeRegionTextRecognizer(
                new Dictionary<(int Page, int Sequence), string>
                {
                    [(2, 0)] =
                        "Recovered page two.",
                    [(2, 2)] =
                        "Recovered caption.",
                    [(3, 0)] =
                        "Verified beta."
                });

        var processor =
            CreateHybridProcessor(
                extraction,
                DocumentPreflightClassification.Hybrid,
                rasterizer,
                layoutAnalyzer,
                recognizer);

        await using var visualStore =
            new VisualDestinationStore();

        await using var sourceStream =
            new MemoryStream(
                "%PDF-mixed-route-test"u8.ToArray(),
                writable:
                    false);

        var result =
            await processor.ProcessAsync(
                new DocumentSource(
                    sourceStream,
                    "mixed.pdf",
                    "application/pdf"),
                visualStore.OpenAsync);

        Assert.Equal(
            3,
            result.Pages.Count);

        Assert.Equal(
            5,
            result.Elements.Count);

        var page1 =
            Assert.Single(
                result.Elements,
                element =>
                    element.PhysicalPageNumber ==
                    1);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            page1.TextOrigin);

        Assert.Null(
            page1.LayoutObservationSequence);

        var page2Ocr =
            Assert.Single(
                result.Elements,
                element =>
                    element.PhysicalPageNumber ==
                        2 &&
                    element.Kind ==
                        HybridDocumentElementKind.Text);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            page2Ocr.ReconciliationDecision);

        Assert.Equal(
            "Recovered page two.",
            page2Ocr.NormalizedText);

        var page2Visual =
            Assert.Single(
                result.Elements,
                element =>
                    element.PhysicalPageNumber ==
                        2 &&
                    element.Kind ==
                        HybridDocumentElementKind.Visual);

        Assert.NotNull(
            page2Visual.PreservedVisual);

        Assert.Null(
            page2Visual.NormalizedText);

        var page2Caption =
            Assert.Single(
                result.Elements,
                element =>
                    element.PhysicalPageNumber ==
                        2 &&
                    element.Kind ==
                        HybridDocumentElementKind.Caption);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            page2Caption.TextOrigin);

        Assert.Equal(
            "Recovered caption.",
            page2Caption.NormalizedText);

        var page3 =
            Assert.Single(
                result.Elements,
                element =>
                    element.PhysicalPageNumber ==
                    3);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            page3.TextOrigin);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            page3.ReconciliationDecision);

        Assert.False(
            page3.HasReconciliationDivergence);

        Assert.Equal(
            1,
            rasterizer.OpenCount);

        var session =
            Assert.Single(
                rasterizer.OpenedSessions);

        Assert.Equal(
            2,
            session.FullPageRenderCount);

        Assert.Equal(
            4,
            session.RegionRenderCount);

        Assert.Equal(
            3,
            recognizer.CallCount);

        Assert.Equal(
            1,
            visualStore.StreamCount);

        Assert.Equal(
            new ProcessingComponentIdentity(
                "fake-raster",
                "fake-raster-v1"),
            result.ProcessingManifest.Rasterization);

        Assert.Equal(
            LayoutIdentity,
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Equal(
            ReconciliationIdentity,
            result.ProcessingManifest.Reconciliation);

        Assert.Contains(
            result.ProcessingManifest.Ocr,
            identity =>
                identity ==
                new ProcessingComponentIdentity(
                    "fake-ocr",
                    "fake-ocr-v1"));

        Assert.Contains(
            "fake-raster-v1",
            result.ProcessingManifest
                .VisualPreservationProfileIds);
    }

    [Fact]
    public async Task ProcessAsync_AllNativeRoutes_DoesNotOpenRasterRuntimeOrClaimHybridProvenance()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    NativePage(
                        1,
                        "Only native text.",
                        imageBacked:
                            false)
                ]);

        var rasterizer =
            new FakeDocumentRasterizer();

        var processor =
            CreateHybridProcessor(
                extraction,
                DocumentPreflightClassification.HealthyBornDigital,
                rasterizer,
                new FakePageLayoutAnalyzer(
                    new Dictionary<int, IReadOnlyList<LayoutObservation>>()),
                new FakeRegionTextRecognizer(
                    new Dictionary<(int Page, int Sequence), string>()));

        await using var sourceStream =
            new MemoryStream(
                "%PDF-native-route-test"u8.ToArray(),
                writable:
                    false);

        var result =
            await processor.ProcessAsync(
                new DocumentSource(
                    sourceStream));

        Assert.Equal(
            0,
            rasterizer.OpenCount);

        Assert.Null(
            result.ProcessingManifest.Rasterization);

        Assert.Null(
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Empty(
            result.ProcessingManifest.Ocr);

        Assert.Null(
            result.ProcessingManifest.Reconciliation);
    }

    [Fact]
    public async Task ProcessAsync_HybridRouteWithoutConfiguredRuntime_FailsBeforePageExecution()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    MissingPage(
                        1)
                ]);

        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(
                    DocumentPreflightClassification.RasterOrScanned),
                "test-engine-v1",
                NativeIdentity);

        await using var sourceStream =
            new MemoryStream(
                "%PDF-no-hybrid-runtime"u8.ToArray(),
                writable:
                    false);

        var exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    processor.ProcessAsync(
                        new DocumentSource(
                            sourceStream)));

        Assert.Contains(
            "Physical page 1",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            nameof(
                PageProcessingRoute
                    .LayoutWithTargetedOcrRecovery),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CaptionedFigureWithoutDestination_FailsBeforeRegionOcr()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    MissingPage(
                        1)
                ]);

        var rasterizer =
            new FakeDocumentRasterizer();

        var recognizer =
            new FakeRegionTextRecognizer(
                new Dictionary<(int Page, int Sequence), string>
                {
                    [(1, 0)] =
                        "Would be OCR",
                    [(1, 2)] =
                        "Would be caption OCR"
                });

        var processor =
            CreateHybridProcessor(
                extraction,
                DocumentPreflightClassification.RasterOrScanned,
                rasterizer,
                new FakePageLayoutAnalyzer(
                    new Dictionary<int, IReadOnlyList<LayoutObservation>>
                    {
                        [1] =
                        [
                            Layout(
                                1,
                                0,
                                0,
                                LayoutObservationKind.Text,
                                0.10,
                                0.10,
                                0.50,
                                0.25),
                            Layout(
                                1,
                                1,
                                1,
                                LayoutObservationKind.Figure,
                                0.60,
                                0.10,
                                0.90,
                                0.40),
                            Layout(
                                1,
                                2,
                                2,
                                LayoutObservationKind.Caption,
                                0.62,
                                0.42,
                                0.88,
                                0.47)
                        ]
                    }),
                recognizer);

        await using var sourceStream =
            new MemoryStream(
                "%PDF-no-visual-destination"u8.ToArray(),
                writable:
                    false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        sourceStream)));

        Assert.Equal(
            0,
            recognizer.CallCount);

        var session =
            Assert.Single(
                rasterizer.OpenedSessions);

        Assert.Equal(
            1,
            session.FullPageRenderCount);

        Assert.Equal(
            0,
            session.RegionRenderCount);
    }

    [Fact]
    public async Task ProcessAsync_UncaptionedFigureWithoutDestination_IsDeferredAndTextStillOcrs()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    MissingPage(
                        1)
                ]);

        var rasterizer =
            new FakeDocumentRasterizer();

        var recognizer =
            new FakeRegionTextRecognizer(
                new Dictionary<(int Page, int Sequence), string>
                {
                    [(1, 0)] =
                        "Recovered text."
                });

        var processor =
            CreateHybridProcessor(
                extraction,
                DocumentPreflightClassification.RasterOrScanned,
                rasterizer,
                new FakePageLayoutAnalyzer(
                    new Dictionary<int, IReadOnlyList<LayoutObservation>>
                    {
                        [1] =
                        [
                            Layout(
                                1,
                                0,
                                0,
                                LayoutObservationKind.Text,
                                0.10,
                                0.10,
                                0.50,
                                0.25),
                            Layout(
                                1,
                                1,
                                1,
                                LayoutObservationKind.Figure,
                                0.60,
                                0.10,
                                0.90,
                                0.40)
                        ]
                    }),
                recognizer);

        await using var sourceStream =
            new MemoryStream(
                "%PDF-unresolved-visual-no-destination"u8.ToArray(),
                writable:
                    false);

        var result =
            await processor.ProcessAsync(
                new DocumentSource(
                    sourceStream));

        Assert.Equal(
            2,
            result.Elements.Count);

        var text =
            Assert.Single(
                result.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Text);

        Assert.Equal(
            "Recovered text.",
            text.NormalizedText);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            text.TextOrigin);

        var deferred =
            Assert.Single(
                result.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Deferred);

        Assert.Equal(
            LayoutObservationKind.Figure,
            deferred.LayoutKind);

        Assert.False(
            deferred.IsResolved);

        Assert.Null(
            deferred.PreservedVisual);

        Assert.Equal(
            1,
            recognizer.CallCount);

        var session =
            Assert.Single(
                rasterizer.OpenedSessions);

        Assert.Equal(
            1,
            session.FullPageRenderCount);

        Assert.Equal(
            1,
            session.RegionRenderCount);

        Assert.Empty(
            result.ProcessingManifest
                .VisualPreservationProfileIds);
    }

    #endregion

    #region Composition helpers

    private static DocumentProcessor CreateHybridProcessor(
        DocumentExtractionResult extraction,
        DocumentPreflightClassification classification,
        FakeDocumentRasterizer rasterizer,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer recognizer)
    {
        var visualPreserver =
            new VisualAssetPreserver();

        var hybridExecution =
            new DocumentHybridExecutionDependencies(
                rasterizer,
                new MissingNativeHybridPageExecutor(
                    layoutAnalyzer,
                    recognizer,
                    visualPreserver),
                new NativePresentHybridPageExecutor(
                    layoutAnalyzer,
                    recognizer,
                    visualPreserver),
                LayoutIdentity,
                ReconciliationIdentity);

        return new DocumentProcessor(
            new StubDetector(),
            new StubExtractor(
                extraction),
            new StubPreflightAnalyzer(
                classification),
            DocumentPageProcessingPlanner.CreateDefault(),
            hybridExecution,
            "test-engine-v1",
            NativeIdentity);
    }

    private static DocumentExtractionResult MixedExtraction() =>
        new(
            DocumentFormatId.Pdf,
            [
                NativePage(
                    1,
                    "Native alpha.",
                    imageBacked:
                        false),
                MissingPage(
                    2),
                NativePage(
                    3,
                    "Verified beta.",
                    imageBacked:
                        true)
            ]);

    private static DocumentExtractionPage NativePage(
        int physicalPageNumber,
        string text,
        bool imageBacked)
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
                                0.12 +
                                index *
                                0.12,
                                0.12,
                                0.20 +
                                index *
                                0.12,
                                0.16),
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
                    0.10,
                    0.55,
                    0.25),
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
                imageBacked
                    ? 1
                    : 0,
            largestRasterImageAreaRatio:
                imageBacked
                    ? 0.80
                    : 0,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words,
            blocks:
                [block]);
    }

    private static DocumentExtractionPage MissingPage(
        int physicalPageNumber) =>
        new(
            physicalPageNumber,
            string.Empty,
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
            wordCount:
                0,
            rasterImageCount:
                1,
            largestRasterImageAreaRatio:
                0.90,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                [],
            blocks:
                []);

    private static LayoutObservation Layout(
        int physicalPageNumber,
        int observationSequence,
        int readingOrder,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber,
            observationSequence,
            readingOrder,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            kind.ToString());

    #endregion

    #region Test doubles

    private sealed class StubDetector
        : IDocumentTypeDetector
    {
        public ValueTask<DocumentTypeDetectionResult> DetectAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                new DocumentTypeDetectionResult(
                    DocumentFormatId.Pdf,
                    "application/pdf",
                    IsSupported:
                        true));
        }
    }

    private sealed class StubExtractor(
        DocumentExtractionResult extraction)
        : IDocumentExtractor
    {
        public bool CanExtract(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                extraction);
        }
    }

    private sealed class StubPreflightAnalyzer(
        DocumentPreflightClassification classification)
        : IDocumentPreflightAnalyzer
    {
        public bool CanAnalyze(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public DocumentPreflightResult Analyze(
            DocumentExtractionResult extraction)
        {
            var nativePages =
                extraction.Pages.Count(
                    page =>
                        page.WordCount >
                        0);

            var missingPages =
                extraction.Pages
                    .Where(
                        page =>
                            page.WordCount ==
                            0)
                    .Select(
                        page =>
                            page.PhysicalPageNumber)
                    .ToArray();

            return new DocumentPreflightResult(
                extraction.Format,
                extraction.Pages.Count,
                nativePages,
                extraction.Pages.Count -
                nativePages,
                extraction.Pages.Count ==
                        0
                    ? 0
                    : nativePages *
                      100.0 /
                      extraction.Pages.Count,
                missingPages,
                missingPages,
                classification);
        }
    }

    private sealed class FakeDocumentRasterizer
        : IDocumentRasterizer
    {
        public int OpenCount { get; private set; }

        public List<FakeRasterizationSession> OpenedSessions { get; } =
            [];

        public bool CanRasterize(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<IDocumentRasterizationSession> OpenAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!source.Content.CanSeek)
            {
                throw new InvalidOperationException(
                    "Prepared source must be seekable.");
            }

            OpenCount++;

            var session =
                new FakeRasterizationSession();

            OpenedSessions.Add(
                session);

            return ValueTask.FromResult<IDocumentRasterizationSession>(
                session);
        }
    }

    private sealed class FakeRasterizationSession
        : IDocumentRasterizationSession
    {
        private static readonly string RasterSha256 =
            Convert.ToHexString(
                    SHA256.HashData(
                        new byte[]
                        {
                            2
                        }))
                .ToLowerInvariant();

        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            "fake-raster-v1";

        public int Dpi =>
            300;

        public int FullPageRenderCount { get; private set; }

        public int RegionRenderCount { get; private set; }

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FullPageRenderCount++;

            destination.WriteByte(
                1);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    1000,
                    1000,
                    crop:
                        null,
                    1000,
                    1000,
                    "image/png",
                    ProfileId,
                    contentLength:
                        1,
                    contentSha256:
                        RasterSha256));
        }

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RegionRenderCount++;

            destination.WriteByte(
                2);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    sourcePagePixelWidth,
                    sourcePagePixelHeight,
                    crop,
                    crop.Width,
                    crop.Height,
                    "image/png",
                    ProfileId,
                    contentLength:
                        1,
                    contentSha256:
                        RasterSha256));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class FakePageLayoutAnalyzer(
        IReadOnlyDictionary<int, IReadOnlyList<LayoutObservation>> pages)
        : IPageLayoutAnalyzer
    {
        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pages.TryGetValue(
                    physicalPageNumber,
                    out var observations))
            {
                throw new InvalidOperationException(
                    $"No fake layout configured for page {physicalPageNumber}.");
            }

            return ValueTask.FromResult(
                new LayoutAnalysisResult(
                    "fake-layout",
                    physicalPageNumber,
                    observations));
        }
    }

    private sealed class FakeRegionTextRecognizer(
        IReadOnlyDictionary<(int Page, int Sequence), string> texts)
        : IRegionTextRecognizer
    {
        public int CallCount { get; private set; }

        public ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            var key =
                (
                    sourceLayoutObservation.PhysicalPageNumber,
                    sourceLayoutObservation.ObservationSequence
                );

            if (!texts.TryGetValue(
                    key,
                    out var text))
            {
                throw new InvalidOperationException(
                    $"No fake OCR text configured for p{key.PhysicalPageNumber}/" +
                    $"seq{key.ObservationSequence}.");
            }

            return ValueTask.FromResult(
                new OcrRegionResult(
                    "fake-ocr",
                    "fake-ocr-v1",
                    sourceLayoutObservation,
                    [
                        new OcrTextObservation(
                            sourceLayoutObservation.PhysicalPageNumber,
                            sourceLayoutObservation.ObservationSequence,
                            observationSequence:
                                0,
                            text,
                            confidence:
                                0.99,
                            sourceLayoutObservation.Bounds)
                    ]));
        }
    }

    private sealed class VisualDestinationStore
        : IAsyncDisposable
    {
        private readonly List<MemoryStream> _streams =
            [];

        public int StreamCount =>
            _streams.Count;

        public ValueTask<Stream> OpenAsync(
            LayoutObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stream =
                new MemoryStream();

            _streams.Add(
                stream);

            return ValueTask.FromResult<Stream>(
                stream);
        }

        public ValueTask DisposeAsync()
        {
            foreach (var stream in
                     _streams)
            {
                stream.Dispose();
            }

            _streams.Clear();

            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
