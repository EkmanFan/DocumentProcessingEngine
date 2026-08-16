using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessorShadowPlanningTests
{
    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ProcessAsync_CompletedShadow_EmitsReportWithoutChangingNativeResult()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Alpha stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var baseline =
            await ProcessNativeAsync(
                extraction,
                shadow:
                    null);

        var observer =
            new RecordingObserver();

        var visualSource =
            new StubVisualSource(
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var shadow =
            Shadow(
                visualSource,
                observer);

        var result =
            await ProcessNativeAsync(
                extraction,
                shadow);

        AssertEquivalentNativeResult(
            baseline,
            result);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentShadowPlanningStatus.Completed,
            report.Status);

        Assert.True(
            report.LegacyPlanningAgreementExact);

        Assert.Equal(
            1,
            report.CandidateNativeTextPageCount);

        Assert.Equal(
            0,
            report.CandidateTargetedOcrPageCount);

        Assert.Equal(
            0,
            report.CandidateRemovesLegacyTextMlCount);

        Assert.Equal(
            1,
            visualSource.ObserveCallCount);
    }

    [Fact]
    public async Task ProcessAsync_ShadowRasterFailure_IsolatedFromLegacyResult()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Alpha stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var observer =
            new RecordingObserver();

        var result =
            await ProcessNativeAsync(
                extraction,
                Shadow(
                    new ThrowingVisualSource(),
                    observer));

        Assert.Single(
            result.Pages);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentShadowPlanningStatus.Failed,
            report.Status);

        var failure =
            Assert.IsType<DocumentShadowPlanningFailure>(
                report.Failure);

        Assert.Equal(
            DocumentShadowPlanningFailureStage.RasterObservation,
            failure.Stage);

        Assert.Contains(
            "synthetic shadow failure",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedShadowCapability_SkipsWithoutChangingLegacyResult()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Alpha stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var observer =
            new RecordingObserver();

        var source =
            new StubVisualSource(
                pages:
                    [],
                canObserve:
                    false);

        var result =
            await ProcessNativeAsync(
                extraction,
                Shadow(
                    source,
                    observer));

        Assert.Single(
            result.Pages);

        Assert.Equal(
            0,
            source.ObserveCallCount);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentShadowPlanningStatus.UnsupportedFormat,
            report.Status);

        Assert.Empty(
            report.Pages);

        Assert.Null(
            report.Failure);
    }

    [Fact]
    public async Task ProcessAsync_ShadowObserverFailure_IsolatedFromLegacyResult()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Alpha stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var result =
            await ProcessNativeAsync(
                extraction,
                Shadow(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [])
                        ]),
                    new ThrowingObserver()));

        Assert.Single(
            result.Pages);

        Assert.Single(
            result.Elements);

        Assert.Equal(
            "Alpha stable native text.",
            result.Elements[0]
                .NormalizedText);
    }

    [Fact]
    public async Task Runner_UnverifiedPresentationOnlyVisual_ReportsLegacyMlRemovalWithoutAuthority()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Native text remains authoritative after deterministic visual verification.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.67));

        var authoritative =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            Assert.Single(
                authoritative)
                .Plan
                .Route);

        var observer =
            new RecordingObserver();

        var runner =
            new DocumentShadowPlanningRunner(
                Shadow(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [
                                    BlankRaster()
                                ])
                        ]),
                    observer));

        await using var stream =
            new MemoryStream(
                "%PDF-shadow-runner"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    stream,
                    "shadow.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                extraction,
                authoritative,
                SourceSha);

        Assert.Equal(
            DocumentShadowPlanningStatus.Completed,
            report.Status);

        Assert.True(
            report.LegacyPlanningAgreementExact);

        Assert.Equal(
            1,
            report.CandidateRemovesLegacyTextMlCount);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            comparison.AuthoritativeLegacy.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            comparison.Shadow.Candidate.Plan.TextMode);

        Assert.False(
            comparison.Shadow.Candidate.Plan.RequiresTargetedOcr);
    }

    [Fact]
    public async Task Runner_SuspiciousNativeText_CannotBeDowngradedByPresentationOnlyVisual()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Native text contains replacement \uFFFD evidence.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.67));

        var authoritative =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction);

        var runner =
            new DocumentShadowPlanningRunner(
                Shadow(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [
                                    BlankRaster()
                                ])
                        ]),
                    new RecordingObserver()));

        await using var stream =
            new MemoryStream(
                "%PDF-shadow-suspicious"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    stream),
                DocumentFormatId.Pdf,
                extraction,
                authoritative,
                SourceSha);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            TextExecutionMode.TargetedOcrReconciliation,
            comparison.Shadow.Candidate.Plan.TextMode);

        Assert.False(
            comparison.CandidateRemovesLegacyTextMl);
    }

    [Fact]
    public async Task Runner_UnknownVisualOnUnverifiedText_FailsClosedToVerificationAndVisualAnalysis()
    {
        var extraction =
            Extraction(
                Page(
                    physicalPageNumber:
                        1,
                    "Native text requires deterministic visual verification.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.67));

        var authoritative =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction);

        var runner =
            new DocumentShadowPlanningRunner(
                Shadow(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [
                                    UnavailableRaster()
                                ])
                        ]),
                    new RecordingObserver()));

        await using var stream =
            new MemoryStream(
                "%PDF-shadow-unknown"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    stream),
                DocumentFormatId.Pdf,
                extraction,
                authoritative,
                SourceSha);

        var candidate =
            Assert.Single(
                report.Pages)
                .Shadow
                .Candidate
                .Plan;

        Assert.Equal(
            TextExecutionMode.TargetedOcrVerification,
            candidate.TextMode);

        Assert.True(
            candidate.RequiresVisualAnalysis);

        Assert.True(
            candidate.RequiresTargetedOcr);
    }

    private static DocumentShadowPlanningDependencies Shadow(
        IVisualRasterObservationSource source,
        IDocumentShadowPlanningObserver observer) =>
        new(
            source,
            observer);

    private static async Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessNativeAsync(
            DocumentExtractionResult extraction,
            DocumentShadowPlanningDependencies? shadow)
    {
        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                "test-engine-shadow-v1",
                NativeIdentity,
                shadow);

        await using var stream =
            new MemoryStream(
                "%PDF-shadow-document"u8.ToArray(),
                writable:
                    false);

        return await processor.ProcessAsync(
            new DocumentSource(
                stream,
                "shadow.pdf",
                "application/pdf"));
    }

    private static void AssertEquivalentNativeResult(
        DocumentProcessing.Core.Results.DocumentIngestionResult expected,
        DocumentProcessing.Core.Results.DocumentIngestionResult actual)
    {
        Assert.Equal(
            expected.Source,
            actual.Source);

        AssertSerializedEquivalent(
            expected.Pages,
            actual.Pages);

        AssertSerializedEquivalent(
            expected.Elements,
            actual.Elements);

        AssertSerializedEquivalent(
            expected.StructuralSegments,
            actual.StructuralSegments);

        AssertSerializedEquivalent(
            expected.ProcessingManifest,
            actual.ProcessingManifest);

        AssertSerializedEquivalent(
            expected.QualityObservations,
            actual.QualityObservations);
    }

    private static void AssertSerializedEquivalent<T>(
        T expected,
        T actual)
    {
        var expectedJson =
            System.Text.Json.JsonSerializer.Serialize(
                expected);

        var actualJson =
            System.Text.Json.JsonSerializer.Serialize(
                actual);

        Assert.Equal(
            expectedJson,
            actualJson);
    }

    private static DocumentExtractionResult Extraction(
        params DocumentExtractionPage[] pages) =>
        new(
            DocumentFormatId.Pdf,
            pages);

    private static DocumentExtractionPage Page(
        int physicalPageNumber,
        string text,
        int rasterImageCount,
        double largestRasterImageAreaRatio)
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
                                0.02,
                                0.20,
                                0.115 +
                                index *
                                0.02,
                                0.23),
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
                    0.35),
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
            rasterImageCount,
            largestRasterImageAreaRatio,
            sourceWidth:
                612,
            sourceHeight:
                792,
            words,
            blocks:
                [block]);
    }

    private static VisualRasterObservation BlankRaster() =>
        new(
            sourceVisualIndex:
                0,
            declaredPageBounds:
                new NormalizedRectangle(
                    0,
                    0,
                    1,
                    1),
            VisualRasterDecodeSource.RawEmbeddedImage,
            pixelWidth:
                16,
            pixelHeight:
                16,
            backgroundUniformity:
                1,
            VisualForegroundState.BlankCanvas,
            foregroundPixelRatio:
                0,
            VisualPixelInteractionKind.BlankCanvas,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                0,
            effectiveVisualBounds:
                null);

    private static VisualRasterObservation UnavailableRaster() =>
        new(
            sourceVisualIndex:
                0,
            declaredPageBounds:
                new NormalizedRectangle(
                    0,
                    0,
                    1,
                    1),
            VisualRasterDecodeSource.Unavailable,
            pixelWidth:
                null,
            pixelHeight:
                null,
            backgroundUniformity:
                null,
            VisualForegroundState.Unavailable,
            foregroundPixelRatio:
                null,
            VisualPixelInteractionKind.NotMeasured,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                null,
            effectiveVisualBounds:
                null);

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

    private sealed class StubPreflightAnalyzer
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

            return new DocumentPreflightResult(
                extraction.Format,
                extraction.Pages.Count,
                nativePages,
                extraction.Pages.Count -
                nativePages,
                nativePages *
                100.0 /
                extraction.Pages.Count,
                extraction.Pages
                    .Where(
                        page =>
                            page.WordCount ==
                            0)
                    .Select(
                        page =>
                            page.PhysicalPageNumber)
                    .ToArray(),
                [],
                DocumentPreflightClassification.HealthyBornDigital);
        }
    }

    private sealed class StubVisualSource(
        IReadOnlyList<PageVisualRasterObservations> pages,
        bool canObserve = true)
        : IVisualRasterObservationSource
    {
        public int ObserveCallCount { get; private set; }

        public bool CanObserve(
            DocumentFormatId format) =>
            canObserve &&
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<IReadOnlyList<PageVisualRasterObservations>> ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ObserveCallCount++;

            return ValueTask.FromResult(
                pages);
        }
    }

    private sealed class ThrowingVisualSource
        : IVisualRasterObservationSource
    {
        public bool CanObserve(
            DocumentFormatId format) =>
            true;

        public ValueTask<IReadOnlyList<PageVisualRasterObservations>> ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "synthetic shadow failure");
    }

    private sealed class RecordingObserver
        : IDocumentShadowPlanningObserver
    {
        public List<DocumentShadowPlanningReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentShadowPlanningReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingObserver
        : IDocumentShadowPlanningObserver
    {
        public ValueTask ObserveAsync(
            DocumentShadowPlanningReport report,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "synthetic observer failure");
    }
}
