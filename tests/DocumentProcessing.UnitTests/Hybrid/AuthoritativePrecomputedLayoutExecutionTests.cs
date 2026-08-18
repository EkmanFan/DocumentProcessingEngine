using System.Security.Cryptography;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class AuthoritativePrecomputedLayoutExecutionTests
{
    #region Variables and Constants

    private const string SourceSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly byte[] RegionBytes =
    [
        2
    ];

    private static readonly string RegionSha256 =
        Convert.ToHexString(
                SHA256.HashData(
                    RegionBytes))
            .ToLowerInvariant();

    #endregion

    #region Tests

    [Fact]
    public async Task MissingNative_PrecomputedLayout_SkipsLayoutAndFullPageRaster()
    {
        var observation =
            Layout(
                LayoutObservationKind.Text,
                left:
                    0.10,
                top:
                    0.10,
                right:
                    0.90,
                bottom:
                    0.30);

        var layout =
            LayoutResult(
                observation);

        var analyzer =
            new ThrowingPageLayoutAnalyzer();

        var recognizer =
            new FakeRegionTextRecognizer(
                "Recovered from OCR.");

        var executor =
            new MissingNativeHybridPageExecutor(
                analyzer,
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new RegionOnlyRasterizationSession();

        var page =
            await executor
                .ExecuteWithPrecomputedLayoutAsync(
                    MissingPage(),
                    new PageProcessingDecision(
                        new PageProcessingAssessment(
                            1,
                            NativeTextStatus.Missing),
                        new PageProcessingPlan(
                            PageProcessingRoute.LayoutWithTargetedOcrRecovery)),
                    raster,
                    FullPageRaster(),
                    layout,
                    SourceSha256);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            "Recovered from OCR.",
            element.Text);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            element.TextOrigin);

        Assert.Equal(
            0,
            analyzer.CallCount);

        Assert.Equal(
            0,
            raster.FullPageRenderCount);

        Assert.Equal(
            1,
            raster.RegionRenderCount);

        Assert.Equal(
            1,
            recognizer.CallCount);
    }

    [Fact]
    public async Task NativePresent_PrecomputedLayout_SkipsLayoutAndFullPageRaster()
    {
        var observation =
            Layout(
                LayoutObservationKind.Text,
                left:
                    0.10,
                top:
                    0.65,
                right:
                    0.40,
                bottom:
                    0.70);

        var layout =
            LayoutResult(
                observation);

        var analyzer =
            new ThrowingPageLayoutAnalyzer();

        var recognizer =
            new FakeRegionTextRecognizer(
                "Native text.");

        var executor =
            new NativePresentHybridPageExecutor(
                analyzer,
                recognizer,
                new VisualAssetPreserver());

        await using var raster =
            new RegionOnlyRasterizationSession();

        var page =
            await executor
                .ExecuteWithPrecomputedLayoutAsync(
                    NativePage(),
                    new PageProcessingDecision(
                        new PageProcessingAssessment(
                            1,
                            NativeTextStatus.Unverified),
                        new PageProcessingPlan(
                            PageProcessingRoute.LayoutWithTargetedOcrReconciliation)),
                    raster,
                    FullPageRaster(),
                    layout,
                    SourceSha256);

        Assert.Single(
            page.Elements);

        Assert.Equal(
            0,
            analyzer.CallCount);

        Assert.Equal(
            0,
            raster.FullPageRenderCount);

        Assert.Equal(
            1,
            raster.RegionRenderCount);

        Assert.Equal(
            1,
            recognizer.CallCount);
    }

    [Fact]
    public async Task HealthyNativeVisual_PrecomputedLayout_SkipsLayoutAndFullPageRaster()
    {
        var figure =
            new LayoutObservation(
                physicalPageNumber:
                    1,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.05,
                    0.05,
                    0.95,
                    0.40),
                "image");

        var text =
            new LayoutObservation(
                physicalPageNumber:
                    1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.65,
                    0.40,
                    0.70),
                "text");

        var layout =
            new LayoutAnalysisResult(
                "fake-layout",
                physicalPageNumber:
                    1,
                [
                    figure,
                    text
                ]);

        var analyzer =
            new ThrowingPageLayoutAnalyzer();

        var executor =
            new HealthyNativeVisualPageExecutor(
                analyzer,
                new VisualAssetPreserver());

        await using var raster =
            new RegionOnlyRasterizationSession();

        await using var destination =
            new MemoryStream();

        var page =
            await executor
                .ExecuteWithPrecomputedLayoutAsync(
                    NativePage(),
                    HealthyNativeOnlyDecision(),
                    PreservePlan(),
                    raster,
                    FullPageRaster(),
                    layout,
                    SourceSha256,
                    (observation, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        Assert.Same(
                            figure,
                            observation);

                        return ValueTask.FromResult<Stream>(
                            destination);
                    });

        Assert.Contains(
            page.Elements,
            element =>
                element.Kind ==
                HybridDocumentElementKind.Visual);

        Assert.Equal(
            RegionBytes,
            destination.ToArray());

        Assert.Equal(
            0,
            analyzer.CallCount);

        Assert.Equal(
            0,
            raster.FullPageRenderCount);

        Assert.Equal(
            1,
            raster.RegionRenderCount);
    }

    #endregion

    #region Methods Helpers

    private static LayoutObservation Layout(
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                0,
            readingOrder:
                0,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            kind.ToString());

    private static LayoutAnalysisResult LayoutResult(
        params LayoutObservation[] observations) =>
        new(
            "fake-layout",
            physicalPageNumber:
                1,
            observations);

    private static DocumentExtractionPage MissingPage() =>
        new(
            physicalPageNumber:
                1,
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

    private static DocumentExtractionPage NativePage()
    {
        var words =
            new[]
            {
                new DocumentWord(
                    sourceSequence:
                        0,
                    "Native",
                    new NormalizedRectangle(
                        0.10,
                        0.65,
                        0.22,
                        0.70)),
                new DocumentWord(
                    sourceSequence:
                        1,
                    "text.",
                    new NormalizedRectangle(
                        0.23,
                        0.65,
                        0.40,
                        0.70))
            };

        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                "Native text.",
                new NormalizedRectangle(
                    0.10,
                    0.65,
                    0.40,
                    0.70),
                words);

        return new DocumentExtractionPage(
            physicalPageNumber:
                1,
            sourceText:
                "Native text.",
            wordCount:
                words.Length,
            rasterImageCount:
                1,
            largestRasterImageAreaRatio:
                0.40,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words,
            blocks:
                [
                    block
                ]);
    }

    private static PageProcessingDecision HealthyNativeOnlyDecision() =>
        new(
            new PageProcessingAssessment(
                physicalPageNumber:
                    1,
                NativeTextStatus.Healthy),
            new PageProcessingPlan(
                PageProcessingRoute.NativeOnly));

    private static PageExecutionPlan PreservePlan() =>
        new(
            physicalPageNumber:
                1,
            TextExecutionMode.NativeText,
            [
                new VisualElementExecutionPlan(
                    sourceVisualIndex:
                        0,
                    VisualExecutionAction.PreserveMeaningfulVisual)
            ]);

    private static RasterRenderResult FullPageRaster() =>
        new(
            physicalPageNumber:
                1,
            sourcePagePixelWidth:
                1000,
            sourcePagePixelHeight:
                1000,
            crop:
                null,
            outputPixelWidth:
                1000,
            outputPixelHeight:
                1000,
            mediaType:
                "image/png",
            profileId:
                RegionOnlyRasterizationSession.Profile,
            contentLength:
                1,
            contentSha256:
                RegionSha256);

    #endregion

    #region Internal Test Doubles

    private sealed class ThrowingPageLayoutAnalyzer
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

            throw new InvalidOperationException(
                "Precomputed layout execution must not call the layout analyzer.");
        }
    }

    private sealed class FakeRegionTextRecognizer(
        string text)
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

    private sealed class RegionOnlyRasterizationSession
        : IDocumentRasterizationSession
    {
        public const string Profile =
            "fake-raster-profile-v1";

        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            Profile;

        public int Dpi =>
            300;

        public int FullPageRenderCount { get; private set; }

        public int RegionRenderCount { get; private set; }

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            FullPageRenderCount++;

            throw new InvalidOperationException(
                "Precomputed layout execution must not rerender the full page.");
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

            destination.Write(
                RegionBytes);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    sourcePagePixelWidth,
                    sourcePagePixelHeight,
                    crop,
                    crop.Width,
                    crop.Height,
                    "image/png",
                    Profile,
                    RegionBytes.Length,
                    RegionSha256));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    #endregion
}
