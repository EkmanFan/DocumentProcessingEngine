using System.Security.Cryptography;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class HealthyNativeVisualPageExecutorTests
{
    private static readonly string SourceSha =
        new(
            'a',
            64);

    [Fact]
    public async Task ExecuteAsync_ResolvedMeaningfulFigure_PreservesNativeAuthorityWithoutOcr()
    {
        var sourcePage =
            NativePage();

        var figure =
            Layout(
                sequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                left:
                    0.05,
                top:
                    0.05,
                right:
                    0.95,
                bottom:
                    0.40);

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
                new FakeLayoutAnalyzer(
                    [
                        figure,
                        text
                    ]),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        await using var destination =
            new MemoryStream();

        var page =
            await executor
                .ExecuteAsync(
                    sourcePage,
                    HealthyNativeOnlyDecision(),
                    PreservePlan(),
                    session,
                    SourceSha,
                    (_, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        return ValueTask.FromResult<Stream>(
                            destination);
                    });

        Assert.Equal(
            1,
            session.FullPageRenderCount);

        Assert.Equal(
            1,
            session.RegionRenderCount);

        Assert.Equal(
            2,
            page.Elements.Count);

        var visual =
            page.Elements[0];

        Assert.Equal(
            HybridDocumentElementKind.Visual,
            visual.Kind);

        Assert.Equal(
            0,
            visual.ReadingOrder);

        Assert.NotNull(
            visual.PreservedVisual);

        var native =
            page.Elements[1];

        Assert.Equal(
            HybridDocumentElementKind.Text,
            native.Kind);

        Assert.Equal(
            1,
            native.ReadingOrder);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            native.TextOrigin);

        Assert.Equal(
            "Native text.",
            native.Text);

        Assert.Same(
            sourcePage.Blocks[0],
            native.NativeBlock);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedLayoutFigure_FailsClosedBeforePreservation()
    {
        var sourcePage =
            NativePage();

        var smallUnknownFigure =
            Layout(
                sequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Figure,
                left:
                    0.05,
                top:
                    0.05,
                right:
                    0.25,
                bottom:
                    0.25);

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
                new FakeLayoutAnalyzer(
                    [
                        smallUnknownFigure,
                        text
                    ]),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        var destinationOpenCount =
            0;

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await executor
                        .ExecuteAsync(
                            sourcePage,
                            HealthyNativeOnlyDecision(),
                            PreservePlan(),
                            session,
                            SourceSha,
                            (_, _) =>
                            {
                                destinationOpenCount++;

                                return ValueTask.FromResult<Stream>(
                                    new MemoryStream());
                            }));

        Assert.Contains(
            "unresolved Figure evidence",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(
            1,
            session.FullPageRenderCount);

        Assert.Equal(
            0,
            session.RegionRenderCount);

        Assert.Equal(
            0,
            destinationOpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_CandidateAnalyzeVisual_IsRejectedBeforeRasterization()
    {
        var sourcePage =
            NativePage();

        var executor =
            new HealthyNativeVisualPageExecutor(
                new FakeLayoutAnalyzer(
                    []),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        var candidate =
            new PageExecutionPlan(
                physicalPageNumber:
                    1,
                TextExecutionMode.NativeText,
                [
                    new VisualElementExecutionPlan(
                        sourceVisualIndex:
                            0,
                        VisualExecutionAction.AnalyzeVisual)
                ]);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await executor
                        .ExecuteAsync(
                            sourcePage,
                            HealthyNativeOnlyDecision(),
                            candidate,
                            session,
                            SourceSha,
                            openVisualDestinationAsync:
                                null));

        Assert.Contains(
            "resolved meaningful-visual preservation",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(
            0,
            session.FullPageRenderCount);

        Assert.Equal(
            0,
            session.RegionRenderCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreservableFigureWithoutDestination_FailsBeforeCrop()
    {
        var sourcePage =
            NativePage();

        var executor =
            new HealthyNativeVisualPageExecutor(
                new FakeLayoutAnalyzer(
                    [
                        Layout(
                            0,
                            0,
                            LayoutObservationKind.Figure,
                            0.05,
                            0.05,
                            0.95,
                            0.40),
                        Layout(
                            1,
                            1,
                            LayoutObservationKind.Text,
                            0.10,
                            0.65,
                            0.55,
                            0.75)
                    ]),
                new VisualAssetPreserver());

        var session =
            new FakeRasterizationSession();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await executor
                        .ExecuteAsync(
                            sourcePage,
                            HealthyNativeOnlyDecision(),
                            PreservePlan(),
                            session,
                            SourceSha,
                            openVisualDestinationAsync:
                                null));

        Assert.Contains(
            "caller-owned destination",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(
            1,
            session.FullPageRenderCount);

        Assert.Equal(
            0,
            session.RegionRenderCount);
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

    private sealed class FakeLayoutAnalyzer(
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

    private sealed class FakeRasterizationSession
        : IDocumentRasterizationSession
    {
        private static readonly byte[] PageBytes =
        [
            1
        ];

        private static readonly byte[] RegionBytes =
        [
            2
        ];

        private static readonly string PageSha =
            Convert.ToHexString(
                    SHA256.HashData(
                        PageBytes))
                .ToLowerInvariant();

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
            cancellationToken.ThrowIfCancellationRequested();

            FullPageRenderCount++;

            destination.Write(
                PageBytes);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
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
                    ProfileId,
                    contentLength:
                        PageBytes.Length,
                    contentSha256:
                        PageSha));
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
                    contentLength:
                        RegionBytes.Length,
                    contentSha256:
                        RegionSha));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
