using System.Security.Cryptography;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class MissingNativeHybridPageExecutorTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task RecoveryRoute_ComposesTargetedOcrVisualPreservationAndDeferredEvidence()
    {
        var layout =
            new LayoutAnalysisResult(
                "fake-layout",
                1,
                [
                    Layout(
                        0,
                        0,
                        LayoutObservationKind.Heading,
                        new NormalizedRectangle(
                            0.10,
                            0.05,
                            0.90,
                            0.12)),
                    Layout(
                        1,
                        1,
                        LayoutObservationKind.Text,
                        new NormalizedRectangle(
                            0.10,
                            0.15,
                            0.90,
                            0.30)),
                    Layout(
                        2,
                        2,
                        LayoutObservationKind.Table,
                        new NormalizedRectangle(
                            0.10,
                            0.32,
                            0.90,
                            0.48)),
                    Layout(
                        3,
                        3,
                        LayoutObservationKind.Figure,
                        new NormalizedRectangle(
                            0.15,
                            0.50,
                            0.55,
                            0.80)),
                    Layout(
                        4,
                        4,
                        LayoutObservationKind.Unknown,
                        new NormalizedRectangle(
                            0.60,
                            0.50,
                            0.90,
                            0.80)),
                    Layout(
                        5,
                        5,
                        LayoutObservationKind.Caption,
                        new NormalizedRectangle(
                            0.15,
                            0.82,
                            0.90,
                            0.90))
                ]);

        var raster =
            new FakeRasterSession();

        var layoutAnalyzer =
            new FakeLayoutAnalyzer(
                layout);

        var recognizer =
            new FakeRecognizer();

        var executor =
            new MissingNativeHybridPageExecutor(
                layoutAnalyzer,
                recognizer,
                new VisualAssetPreserver());

        var visualDestinations =
            new Dictionary<int, MemoryStream>();

        var page =
            await executor
                .ExecuteAsync(
                    MissingPage(),
                    RecoveryDecision(),
                    raster,
                    SourceSha,
                    (observation, _) =>
                    {
                        var destination =
                            new MemoryStream();

                        visualDestinations.Add(
                            observation.ObservationSequence,
                            destination);

                        return ValueTask.FromResult<Stream>(
                            destination);
                    });

        Assert.Equal(
            1,
            raster.PageRenderCount);

        Assert.Equal(
            4,
            recognizer.CallCount);

        Assert.Equal(
            5,
            raster.RegionRenderCount);

        Assert.Equal(
            new[]
            {
                LayoutObservationKind.Heading,
                LayoutObservationKind.Text,
                LayoutObservationKind.Table,
                LayoutObservationKind.Caption
            },
            recognizer
                .ObservedKinds);

        Assert.DoesNotContain(
            LayoutObservationKind.Figure,
            recognizer.ObservedKinds);

        Assert.DoesNotContain(
            LayoutObservationKind.Unknown,
            recognizer.ObservedKinds);

        Assert.Equal(
            6,
            page.Elements.Count);

        Assert.Equal(
            new[]
            {
                HybridDocumentElementKind.Heading,
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Text,
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Deferred,
                HybridDocumentElementKind.Caption
            },
            page.Elements
                .Select(
                    element =>
                        element.Kind));

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.Ocr,
                TextSelectionOrigin.Ocr,
                TextSelectionOrigin.Ocr,
                TextSelectionOrigin.None,
                TextSelectionOrigin.None,
                TextSelectionOrigin.Ocr
            },
            page.Elements
                .Select(
                    element =>
                        element.TextOrigin));

        Assert.Equal(
            new[]
            {
                "ocr-heading",
                "ocr-text",
                "ocr-table",
                "ocr-caption"
            },
            page.AuthoritativeTextElements
                .Select(
                    element =>
                        element.Text));

        var figure =
            Assert.Single(
                page.VisualElements);

        Assert.NotNull(
            figure.PreservedVisual);

        Assert.Equal(
            SourceSha,
            figure
                .PreservedVisual!
                .SourceDocumentSha256);

        Assert.Equal(
            "fake-raster-profile-v1",
            figure
                .PreservedVisual!
                .ProfileId);

        Assert.Single(
            visualDestinations);

        var visualDestination =
            visualDestinations[3];

        Assert.True(
            visualDestination.Length >
            0);

        Assert.Equal(
            figure
                .PreservedVisual
                .ContentSha256,
            Hash(
                visualDestination.ToArray()));
    }

    [Fact]
    public async Task RecoveryRoute_WithCaptionedFigureButNoDestinationFactory_FailsBeforeAnyRegionExecution()
    {
        var layout =
            new LayoutAnalysisResult(
                "fake-layout",
                1,
                [
                    Layout(
                        0,
                        0,
                        LayoutObservationKind.Text,
                        new NormalizedRectangle(
                            0.10,
                            0.10,
                            0.90,
                            0.30)),
                    Layout(
                        1,
                        1,
                        LayoutObservationKind.Figure,
                        new NormalizedRectangle(
                            0.10,
                            0.40,
                            0.90,
                            0.80)),
                    Layout(
                        2,
                        2,
                        LayoutObservationKind.Caption,
                        new NormalizedRectangle(
                            0.12,
                            0.82,
                            0.88,
                            0.88))
                ]);

        var raster =
            new FakeRasterSession();

        var recognizer =
            new FakeRecognizer();

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakeLayoutAnalyzer(
                    layout),
                recognizer,
                new VisualAssetPreserver());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await executor
                    .ExecuteAsync(
                        MissingPage(),
                        RecoveryDecision(),
                        raster,
                        SourceSha));

        Assert.Equal(
            1,
            raster.PageRenderCount);

        Assert.Equal(
            0,
            raster.RegionRenderCount);

        Assert.Equal(
            0,
            recognizer.CallCount);
    }

    [Fact]
    public async Task RecoveryRoute_OcrWithNoText_RemainsUnresolved()
    {
        var layoutObservation =
            Layout(
                0,
                0,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.30));

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakeLayoutAnalyzer(
                    new LayoutAnalysisResult(
                        "fake-layout",
                        1,
                        [
                            layoutObservation
                        ])),
                new EmptyRecognizer(),
                new VisualAssetPreserver());

        var page =
            await executor
                .ExecuteAsync(
                    MissingPage(),
                    RecoveryDecision(),
                    new FakeRasterSession(),
                    SourceSha);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.UnresolvedText,
            element.Kind);

        Assert.False(
            element.HasAuthoritativeText);

        Assert.Equal(
            TextSelectionOrigin.None,
            element.TextOrigin);

        Assert.Equal(
            TextReconciliationDecision.NoTextRecovered,
            element
                .Reconciliation!
                .Decision);
    }

    [Fact]
    public async Task Executor_RejectsNonRecoveryRouteBeforeRasterExecution()
    {
        var raster =
            new FakeRasterSession();

        var executor =
            new MissingNativeHybridPageExecutor(
                new FakeLayoutAnalyzer(
                    new LayoutAnalysisResult(
                        "fake-layout",
                        1)),
                new FakeRecognizer(),
                new VisualAssetPreserver());

        var decision =
            new PageProcessingDecision(
                new PageProcessingAssessment(
                    1,
                    NativeTextStatus.Unverified),
                new PageProcessingPlan(
                    PageProcessingRoute.LayoutWithTargetedOcrReconciliation));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await executor
                    .ExecuteAsync(
                        MissingPage(),
                        decision,
                        raster,
                        SourceSha));

        Assert.Equal(
            0,
            raster.PageRenderCount);
    }

    private static DocumentExtractionPage MissingPage() =>
        new(
            physicalPageNumber:
                1,
            sourceText:
                string.Empty,
            contentViewport:
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
                1,
            sourceWidth:
                612,
            sourceHeight:
                792);

    private static PageProcessingDecision RecoveryDecision() =>
        new(
            new PageProcessingAssessment(
                1,
                NativeTextStatus.Missing),
            new PageProcessingPlan(
                PageProcessingRoute.LayoutWithTargetedOcrRecovery));

    private static LayoutObservation Layout(
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        NormalizedRectangle bounds) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            kind,
            bounds,
            rawLabel:
                kind.ToString());

    private static string Hash(
        byte[] bytes) =>
        Convert.ToHexString(
                SHA256.HashData(
                    bytes))
            .ToLowerInvariant();

    private sealed class FakeLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        private readonly LayoutAnalysisResult _result;

        public FakeLayoutAnalyzer(
            LayoutAnalysisResult result)
        {
            _result =
                result;
        }

        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(
                0,
                rasterImage.Position);

            Assert.Equal(
                1,
                physicalPageNumber);

            Assert.Equal(
                1000,
                pixelWidth);

            Assert.Equal(
                1200,
                pixelHeight);

            return ValueTask.FromResult(
                _result);
        }
    }

    private sealed class FakeRecognizer
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
            Assert.Equal(
                0,
                rasterRegion.Position);

            CallCount++;

            ObservedKinds.Add(
                sourceLayoutObservation.Kind);

            var text =
                sourceLayoutObservation.Kind switch
                {
                    LayoutObservationKind.Heading =>
                        "ocr-heading",

                    LayoutObservationKind.Text =>
                        "ocr-text",

                    LayoutObservationKind.Table =>
                        "ocr-table",

                    LayoutObservationKind.Caption =>
                        "ocr-caption",

                    _ =>
                        throw new InvalidOperationException(
                            "Recognizer was called for an unauthorized region.")
                };

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

    private sealed class EmptyRecognizer
        : IRegionTextRecognizer
    {
        public ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                new OcrRegionResult(
                    "fake-ocr",
                    "fake-ocr-v1",
                    sourceLayoutObservation));
    }

    private sealed class FakeRasterSession
        : IDocumentRasterizationSession
    {
        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            "fake-raster-profile-v1";

        public int Dpi =>
            300;

        public int PageRenderCount { get; private set; }

        public int RegionRenderCount { get; private set; }

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            PageRenderCount++;

            var bytes =
                "fake-page-raster"u8.ToArray();

            destination.Write(
                bytes);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    sourcePagePixelWidth:
                        1000,
                    sourcePagePixelHeight:
                        1200,
                    crop:
                        null,
                    outputPixelWidth:
                        1000,
                    outputPixelHeight:
                        1200,
                    mediaType:
                        "image/png",
                    ProfileId,
                    bytes.Length,
                    Hash(
                        bytes)));
        }

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            RegionRenderCount++;

            var bytes =
                System.Text.Encoding.UTF8.GetBytes(
                    $"crop:{crop.Left},{crop.Top},{crop.Right},{crop.Bottom}");

            destination.Write(
                bytes);

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
                    bytes.Length,
                    Hash(
                        bytes)));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
