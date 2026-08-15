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

public sealed class NativePresentHybridPageExecutorTests
{
    private const string SourceSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ExecuteAsync_UnverifiedAgreementSelectsNativePdf()
    {
        var layout =
            Layout(
                405,
                9,
                9,
                LayoutObservationKind.Text,
                0.10,
                0.10,
                0.90,
                0.30);

        var block =
            Block(
                6,
                6,
                Word(0, "After", 0.12, 0.12, 0.20, 0.16),
                Word(1, "leaving", 0.21, 0.12, 0.31, 0.16),
                Word(2, "Thessalonica,", 0.32, 0.12, 0.48, 0.16));

        var executor =
            Executor(
                new[]
                {
                    layout
                },
                new Dictionary<int, string>
                {
                    [9] =
                        "After leaving Thessalonica,"
                },
                out var recognizer);

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                Page(
                    405,
                    block),
                Decision(
                    405,
                    NativeTextStatus.Unverified),
                raster,
                SourceSha256);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.Text,
            element.Kind);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            element.TextOrigin);

        Assert.Equal(
            "After leaving Thessalonica,",
            element.Text);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            element.Reconciliation?.Decision);

        Assert.Equal(
            1,
            recognizer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnverifiedConflictProducesUnresolvedText()
    {
        var layout =
            Layout(
                380,
                5,
                5,
                LayoutObservationKind.Text,
                0.10,
                0.10,
                0.90,
                0.30);

        var block =
            Block(
                2,
                2,
                Word(0, "conversion", 0.12, 0.12, 0.28, 0.16));

        var executor =
            Executor(
                new[]
                {
                    layout
                },
                new Dictionary<int, string>
                {
                    [5] =
                        "conversior"
                },
                out _);

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                Page(
                    380,
                    block),
                Decision(
                    380,
                    NativeTextStatus.Unverified),
                raster,
                SourceSha256);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            HybridDocumentElementKind.UnresolvedText,
            element.Kind);

        Assert.False(
            element.HasAuthoritativeText);

        Assert.Null(
            element.Text);

        Assert.Equal(
            TextSelectionOrigin.None,
            element.TextOrigin);

        Assert.Equal(
            TextReconciliationDecision.Conflict,
            element.Reconciliation?.Decision);

        Assert.True(
            page.HasUnresolvedEvidence);
    }

    [Fact]
    public async Task ExecuteAsync_TargetWithoutNativeProjectionUsesOcrRecovery()
    {
        var layout =
            Layout(
                50,
                3,
                3,
                LayoutObservationKind.Text,
                0.60,
                0.60,
                0.90,
                0.80);

        var unrelatedNativeBlock =
            Block(
                1,
                1,
                Word(0, "native", 0.10, 0.10, 0.20, 0.15));

        var executor =
            Executor(
                new[]
                {
                    layout
                },
                new Dictionary<int, string>
                {
                    [3] =
                        "OCR recovered region"
                },
                out _);

        await using var raster =
            new FakeRasterizationSession();

        var page =
            await executor.ExecuteAsync(
                Page(
                    50,
                    unrelatedNativeBlock),
                Decision(
                    50,
                    NativeTextStatus.Unverified),
                raster,
                SourceSha256);

        var element =
            Assert.Single(
                page.Elements);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            element.TextOrigin);

        Assert.Equal(
            "OCR recovered region",
            element.Text);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            element.Reconciliation?.Decision);

        Assert.Equal(
            NativeTextStatus.Missing,
            element.Reconciliation?
                .Input
                .NativeStatus);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousWordOwnershipFailsClosedBeforeOcr()
    {
        var firstLayout =
            Layout(
                60,
                0,
                0,
                LayoutObservationKind.Text,
                0.40,
                0.10,
                0.52,
                0.20);

        var secondLayout =
            Layout(
                60,
                1,
                1,
                LayoutObservationKind.Text,
                0.50,
                0.10,
                0.62,
                0.20);

        var block =
            Block(
                0,
                0,
                Word(0, "shared", 0.45, 0.12, 0.55, 0.16));

        var executor =
            Executor(
                new[]
                {
                    firstLayout,
                    secondLayout
                },
                new Dictionary<int, string>
                {
                    [0] =
                        "shared",
                    [1] =
                        "shared"
                },
                out var recognizer);

        await using var raster =
            new FakeRasterizationSession();

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await executor.ExecuteAsync(
                    Page(
                        60,
                        block),
                    Decision(
                        60,
                        NativeTextStatus.Unverified),
                    raster,
                    SourceSha256));

        Assert.Equal(
            0,
            recognizer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsHealthyNativeRoute()
    {
        var layout =
            Layout(
                70,
                0,
                0,
                LayoutObservationKind.Text,
                0.10,
                0.10,
                0.90,
                0.30);

        var block =
            Block(
                0,
                0,
                Word(0, "healthy", 0.12, 0.12, 0.22, 0.16));

        var executor =
            Executor(
                new[]
                {
                    layout
                },
                new Dictionary<int, string>
                {
                    [0] =
                        "healthy"
                },
                out _);

        await using var raster =
            new FakeRasterizationSession();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await executor.ExecuteAsync(
                    Page(
                        70,
                        block),
                    Decision(
                        70,
                        NativeTextStatus.Healthy),
                    raster,
                    SourceSha256));
    }

    private static NativePresentHybridPageExecutor Executor(
        IReadOnlyList<LayoutObservation> observations,
        IReadOnlyDictionary<int, string> ocrText,
        out FakeRegionTextRecognizer recognizer)
    {
        recognizer =
            new FakeRegionTextRecognizer(
                ocrText);

        return new NativePresentHybridPageExecutor(
            new FakePageLayoutAnalyzer(
                observations),
            recognizer,
            new VisualAssetPreserver());
    }

    private static PageProcessingDecision Decision(
        int physicalPageNumber,
        NativeTextStatus nativeTextStatus) =>
        new(
            new PageProcessingAssessment(
                physicalPageNumber,
                nativeTextStatus),
            new PageProcessingPlan(
                PageProcessingRoute
                    .LayoutWithTargetedOcrReconciliation));

    private static DocumentExtractionPage Page(
        int physicalPageNumber,
        params DocumentTextBlock[] blocks)
    {
        var words =
            blocks
                .SelectMany(
                    block =>
                        block.Words)
                .DistinctBy(
                    word =>
                        word.SourceSequence)
                .ToArray();

        return new DocumentExtractionPage(
            physicalPageNumber,
            string.Join(
                "\n",
                blocks.Select(
                    block =>
                        block.Text)),
            wordCount:
                words.Length,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                words,
            blocks:
                blocks);
    }

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom));

    private static DocumentTextBlock Block(
        int sourceSequence,
        int readingOrder,
        params DocumentWord[] words) =>
        new(
            sourceSequence,
            readingOrder,
            string.Join(
                " ",
                words.Select(
                    word =>
                        word.Text)),
            new NormalizedRectangle(
                words.Min(word => word.Bounds.Left),
                words.Min(word => word.Bounds.Top),
                words.Max(word => word.Bounds.Right),
                words.Max(word => word.Bounds.Bottom)),
            words);

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

    private sealed class FakePageLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        private readonly IReadOnlyList<LayoutObservation> _observations;

        public FakePageLayoutAnalyzer(
            IReadOnlyList<LayoutObservation> observations)
        {
            _observations =
                observations;
        }

        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                new LayoutAnalysisResult(
                    "fake-layout",
                    physicalPageNumber,
                    _observations));
        }
    }

    private sealed class FakeRegionTextRecognizer
        : IRegionTextRecognizer
    {
        private readonly IReadOnlyDictionary<int, string> _ocrText;

        public FakeRegionTextRecognizer(
            IReadOnlyDictionary<int, string> ocrText)
        {
            _ocrText =
                ocrText;
        }

        public int CallCount { get; private set; }

        public ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CallCount++;

            if (!_ocrText.TryGetValue(
                    sourceLayoutObservation.ObservationSequence,
                    out var text))
            {
                throw new InvalidOperationException(
                    "No fake OCR text configured for layout observation.");
            }

            return ValueTask.FromResult(
                new OcrRegionResult(
                    "fake-ocr",
                    "fake-ocr-profile-v1",
                    sourceLayoutObservation,
                    new[]
                    {
                        new OcrTextObservation(
                            sourceLayoutObservation.PhysicalPageNumber,
                            sourceLayoutObservation.ObservationSequence,
                            0,
                            text,
                            0.95,
                            sourceLayoutObservation.Bounds)
                    }));
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
            cancellationToken
                .ThrowIfCancellationRequested();

            destination.WriteByte(
                1);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    1000,
                    1000,
                    crop: null,
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
            cancellationToken
                .ThrowIfCancellationRequested();

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
