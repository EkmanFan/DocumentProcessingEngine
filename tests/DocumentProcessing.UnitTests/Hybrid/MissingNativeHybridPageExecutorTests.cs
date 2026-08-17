using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class MissingNativeHybridPageExecutorTests
{
    private const string SourceSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ExecuteAsync_MissingNativeText_RecoversTargetedOcrText()
    {
        var layout =
            new LayoutObservation(
                physicalPageNumber:
                    233,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.30),
                "Text");

        var recognizer =
            new FakeRegionTextRecognizer(
                "Recovered from OCR.");

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakePageLayoutAnalyzer(
                    [
                        layout
                    ]),
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                new DocumentExtractionPage(
                    physicalPageNumber:
                        233,
                    sourceText:
                        string.Empty,
                    wordCount:
                        0,
                    sourceWidth:
                        1000,
                    sourceHeight:
                        1000,
                    words:
                        [],
                    blocks:
                        []),
                new PageProcessingDecision(
                    new PageProcessingAssessment(
                        233,
                        NativeTextStatus.Missing),
                    new PageProcessingPlan(
                        PageProcessingRoute.LayoutWithTargetedOcrRecovery)),
                raster,
                SourceSha256);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.Text,
            element.Kind);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            element.TextOrigin);

        Assert.Equal(
            "Recovered from OCR.",
            element.Text);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            element.Reconciliation?.Decision);

        Assert.Equal(
            NativeTextStatus.Missing,
            element.Reconciliation?
                .Input
                .NativeStatus);

        Assert.Equal(
            1,
            recognizer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CaptionedFigure_PreservesSemanticVisualAndNeverOcrsFigure()
    {
        var figure =
            new LayoutObservation(
                233,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.60,
                    0.50),
                "image");

        var caption =
            new LayoutObservation(
                233,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Caption,
                new NormalizedRectangle(
                    0.12,
                    0.52,
                    0.58,
                    0.58),
                "figure_title");

        var recognizer =
            new FakeRegionTextRecognizer(
                "Recovered caption.");

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakePageLayoutAnalyzer(
                    [
                        figure,
                        caption
                    ]),
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new FakeRasterizationSession();

        await using var destination =
            new MemoryStream();

        var destinationCalls =
            0;

        var page =
            await executor.ExecuteAsync(
                MissingPage(
                    233),
                MissingDecision(
                    233),
                raster,
                SourceSha256,
                (observation, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    destinationCalls++;
                    Assert.Same(
                        figure,
                        observation);

                    return ValueTask.FromResult<Stream>(
                        destination);
                });

        Assert.Equal(
            2,
            page.Elements.Count);

        var visual =
            Assert.Single(
                page.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Visual);

        Assert.Same(
            figure,
            visual.LayoutObservation);

        Assert.NotNull(
            visual.PreservedVisual);

        Assert.Equal(
            new byte[] { 2 },
            destination.ToArray());

        var recoveredCaption =
            Assert.Single(
                page.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Caption);

        Assert.Equal(
            "Recovered caption.",
            recoveredCaption.Text);

        Assert.Equal(
            1,
            destinationCalls);

        Assert.Equal(
            1,
            recognizer.CallCount);

        Assert.Equal(
            [
                LayoutObservationKind.Caption
            ],
            recognizer.ObservedKinds);

        Assert.DoesNotContain(
            LayoutObservationKind.Figure,
            recognizer.ObservedKinds);
    }

    [Fact]
    public async Task ExecuteAsync_UncaptionedFigure_IsDeferredWithoutDestinationAndNeverOcrsFigure()
    {
        var figure =
            new LayoutObservation(
                233,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.60,
                    0.50),
                "image");

        var recognizer =
            new FakeRegionTextRecognizer(
                "must-not-be-used");

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakePageLayoutAnalyzer(
                    [
                        figure
                    ]),
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                MissingPage(
                    233),
                MissingDecision(
                    233),
                raster,
                SourceSha256);

        var deferred =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.Deferred,
            deferred.Kind);

        Assert.Same(
            figure,
            deferred.LayoutObservation);

        Assert.Equal(
            0,
            recognizer.CallCount);

        Assert.Empty(
            recognizer.ObservedKinds);
    }

    [Fact]
    public async Task ExecuteAsync_CaptionedFigureWithoutDestination_FailsBeforeOcr()
    {
        var figure =
            new LayoutObservation(
                233,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.60,
                    0.50),
                "image");

        var caption =
            new LayoutObservation(
                233,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Caption,
                new NormalizedRectangle(
                    0.12,
                    0.52,
                    0.58,
                    0.58),
                "figure_title");

        var recognizer =
            new FakeRegionTextRecognizer(
                "must-not-be-used");

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakePageLayoutAnalyzer(
                    [
                        figure,
                        caption
                    ]),
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new FakeRasterizationSession();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await executor.ExecuteAsync(
                    MissingPage(
                        233),
                    MissingDecision(
                        233),
                    raster,
                    SourceSha256));

        Assert.Equal(
            0,
            recognizer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_FutureNonTextKind_IsDeferred()
    {
        var futureKind =
            (LayoutObservationKind)int.MaxValue;

        var observation =
            new LayoutObservation(
                233,
                observationSequence:
                    0,
                readingOrder:
                    0,
                futureKind,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.60,
                    0.20),
                "future_kind");

        var recognizer =
            new FakeRegionTextRecognizer(
                "must-not-be-used");

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakePageLayoutAnalyzer(
                    [
                        observation
                    ]),
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                MissingPage(
                    233),
                MissingDecision(
                    233),
                raster,
                SourceSha256);

        var deferred =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.Deferred,
            deferred.Kind);

        Assert.Same(
            observation,
            deferred.LayoutObservation);

        Assert.Equal(
            0,
            recognizer.CallCount);
    }

    private static DocumentExtractionPage MissingPage(
        int physicalPageNumber) =>
        new(
            physicalPageNumber,
            sourceText:
                string.Empty,
            wordCount:
                0,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                [],
            blocks:
                []);

    private static PageProcessingDecision MissingDecision(
        int physicalPageNumber) =>
        new(
            new PageProcessingAssessment(
                physicalPageNumber,
                NativeTextStatus.Missing),
            new PageProcessingPlan(
                PageProcessingRoute.LayoutWithTargetedOcrRecovery));

    private sealed class FakePageLayoutAnalyzer(
        IReadOnlyList<LayoutObservation> observations)
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

            return ValueTask.FromResult(
                new LayoutAnalysisResult(
                    "fake-layout",
                    physicalPageNumber,
                    observations));
        }
    }

    private sealed class FakeRegionTextRecognizer(
        string text)
        : IRegionTextRecognizer
    {
        public int CallCount { get; private set; }

        public List<LayoutObservationKind> ObservedKinds { get; } =
            [];

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
            ObservedKinds.Add(
                sourceLayoutObservation.Kind);

            return ValueTask.FromResult(
                new OcrRegionResult(
                    "fake-ocr",
                    "fake-ocr-profile-v1",
                    sourceLayoutObservation,
                    [
                        new OcrTextObservation(
                            sourceLayoutObservation.PhysicalPageNumber,
                            sourceLayoutObservation.ObservationSequence,
                            0,
                            text,
                            0.95,
                            sourceLayoutObservation.Bounds)
                    ]));
        }
    }

    private sealed class FakeRasterizationSession
        : IDocumentRasterizationSession
    {
        private const string RasterSha256 =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            "fake-raster-profile-v1";

        public int Dpi =>
            300;

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    1,
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
                    1,
                    RasterSha256));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
