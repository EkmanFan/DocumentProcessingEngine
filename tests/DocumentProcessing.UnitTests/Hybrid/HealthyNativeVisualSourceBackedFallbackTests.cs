using System.Security.Cryptography;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class HealthyNativeVisualSourceBackedFallbackTests
{
    #region Variables and Constants

    private static readonly string SourceSha =
        new(
            'a',
            64);

    #endregion

    #region ctor

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ExecuteWithPrecomputedLayoutAsync_SourceBackedSingletonUnknownFigure_PreservesWithoutOcr()
    {
        var sourcePage =
            NativePage(
                rasterImageCount:
                    1);

        var figure =
            Layout(
                sequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                left:
                    0.12,
                top:
                    0.22,
                right:
                    0.87,
                bottom:
                    0.30);

        var text =
            Layout(
                sequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Text,
                left:
                    0.10,
                top:
                    0.65,
                right:
                    0.55,
                bottom:
                    0.75);

        var executor =
            new HealthyNativeVisualPageExecutor(
                new ThrowingLayoutAnalyzer(),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        await using var destination =
            new MemoryStream();

        var page =
            await executor
                .ExecuteWithPrecomputedLayoutAsync(
                    sourcePage,
                    HealthyNativeOnlyDecision(),
                    PreservePlan(),
                    session,
                    FullPageRaster(),
                    new LayoutAnalysisResult(
                        "fake-layout",
                        physicalPageNumber:
                            1,
                        [
                            figure,
                            text
                        ]),
                    SourceSha,
                    (_, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        return ValueTask.FromResult<Stream>(
                            destination);
                    });

        Assert.Equal(
            0,
            session.FullPageRenderCount);

        Assert.Equal(
            1,
            session.RegionRenderCount);

        Assert.Equal(
            [
                HybridDocumentElementKind.Visual,
                HybridDocumentElementKind.Text
            ],
            page.Elements
                .Select(
                    element =>
                        element.Kind));

        var visual =
            page.Elements[0];

        Assert.Same(
            figure,
            visual.LayoutObservation);

        Assert.NotNull(
            visual.PreservedVisual);

        var native =
            page.Elements[1];

        Assert.Equal(
            TextSelectionOrigin.Native,
            native.TextOrigin);

        Assert.Equal(
            "Native text.",
            native.Text);

        Assert.Same(
            sourcePage.Blocks[0],
            native.NativeBlock);
    }

    [Fact]
    public async Task ExecuteWithPrecomputedLayoutAsync_MultipleSourceVisualPlans_KeepUnknownFigureFailClosed()
    {
        var sourcePage =
            NativePage(
                rasterImageCount:
                    2);

        var figure =
            Layout(
                0,
                0,
                LayoutObservationKind.Figure,
                0.12,
                0.22,
                0.87,
                0.30);

        var text =
            Layout(
                1,
                1,
                LayoutObservationKind.Text,
                0.10,
                0.65,
                0.55,
                0.75);

        var plan =
            new PageExecutionPlan(
                physicalPageNumber:
                    1,
                TextExecutionMode.NativeText,
                [
                    new VisualElementExecutionPlan(
                        sourceVisualIndex:
                            0,
                        VisualExecutionAction.PreserveMeaningfulVisual),
                    new VisualElementExecutionPlan(
                        sourceVisualIndex:
                            1,
                        VisualExecutionAction.NoAdditionalSemanticProcessing)
                ]);

        var executor =
            new HealthyNativeVisualPageExecutor(
                new ThrowingLayoutAnalyzer(),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await executor
                        .ExecuteWithPrecomputedLayoutAsync(
                            sourcePage,
                            HealthyNativeOnlyDecision(),
                            plan,
                            session,
                            FullPageRaster(),
                            new LayoutAnalysisResult(
                                "fake-layout",
                                1,
                                [
                                    figure,
                                    text
                                ]),
                            SourceSha,
                            (_, _) =>
                                ValueTask.FromResult<Stream>(
                                    new MemoryStream())));

        Assert.Contains(
            "unresolved Figure evidence",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(
            0,
            session.RegionRenderCount);
    }

    [Fact]
    public async Task ExecuteWithPrecomputedLayoutAsync_UnknownFigureIntersectingSemanticText_FailsClosed()
    {
        var sourcePage =
            NativePage(
                rasterImageCount:
                    1);

        var figure =
            Layout(
                0,
                0,
                LayoutObservationKind.Figure,
                0.12,
                0.22,
                0.87,
                0.30);

        var overlappingText =
            Layout(
                1,
                1,
                LayoutObservationKind.Text,
                0.20,
                0.24,
                0.70,
                0.28);

        var executor =
            new HealthyNativeVisualPageExecutor(
                new ThrowingLayoutAnalyzer(),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await executor
                        .ExecuteWithPrecomputedLayoutAsync(
                            sourcePage,
                            HealthyNativeOnlyDecision(),
                            PreservePlan(),
                            session,
                            FullPageRaster(),
                            new LayoutAnalysisResult(
                                "fake-layout",
                                1,
                                [
                                    figure,
                                    overlappingText
                                ]),
                            SourceSha,
                            (_, _) =>
                                ValueTask.FromResult<Stream>(
                                    new MemoryStream())));

        Assert.Contains(
            "unresolved Figure evidence",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(
            0,
            session.RegionRenderCount);
    }

    #endregion

    #region Methods Helpers

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

    private static DocumentExtractionPage NativePage(
        int rasterImageCount)
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
                        0.25,
                        0.70)),
                new DocumentWord(
                    sourceSequence:
                        1,
                    "text.",
                    new NormalizedRectangle(
                        0.27,
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
            rasterImageCount,
            largestRasterImageAreaRatio:
                0.10,
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

    private static LayoutObservation Layout(
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            kind.ToString());

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
                "fake-raster-v1",
            contentLength:
                1,
            contentSha256:
                Convert.ToHexString(
                        SHA256.HashData(
                            [
                                1
                            ]))
                    .ToLowerInvariant());

    #endregion

    #region Nested Types

    private sealed class ThrowingLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Precomputed-layout test must not invoke layout analysis.");
    }

    private sealed class FakeRasterizationSession
        : IDocumentRasterizationSession
    {
        private static readonly byte[] RegionBytes =
        [
            2
        ];

        private static readonly string RegionSha =
            Convert.ToHexString(
                    SHA256.HashData(
                        RegionBytes))
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
            FullPageRenderCount++;

            throw new InvalidOperationException(
                "Precomputed-layout execution must not render a full page.");
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
                    ProfileId,
                    RegionBytes.Length,
                    RegionSha));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    #endregion
}
