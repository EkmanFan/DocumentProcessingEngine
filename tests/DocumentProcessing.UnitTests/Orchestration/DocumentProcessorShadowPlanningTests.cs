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
    public async Task ProcessAsync_CoordinatedShadow_UsesPrecomputedRasterObservations()
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

        var visualSource =
            new StubVisualSource(
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var coordinatedExtractor =
            CoordinatedStubExtractor.Success(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var result =
            await ProcessNativeAsync(
                coordinatedExtractor,
                Shadow(
                    visualSource,
                    observer));

        Assert.Single(
            result.Pages);

        Assert.Equal(
            1,
            coordinatedExtractor
                .CoordinatedExtractCallCount);

        Assert.Equal(
            0,
            coordinatedExtractor
                .FallbackExtractCallCount);

        Assert.Equal(
            0,
            visualSource.ObserveCallCount);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentShadowPlanningStatus.Completed,
            report.Status);

        Assert.True(
            report.LegacyPlanningAgreementExact);
    }

    [Fact]
    public async Task ProcessAsync_CoordinatedRasterFailure_IsolatedFromLegacyResult()
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

        var visualSource =
            new StubVisualSource(
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var coordinatedExtractor =
            CoordinatedStubExtractor.FailedRasterObservation(
                extraction,
                new RasterObservationAcquisitionFailure(
                    typeof(InvalidOperationException)
                        .FullName!,
                    "synthetic coordinated shadow failure"));

        var result =
            await ProcessNativeAsync(
                coordinatedExtractor,
                Shadow(
                    visualSource,
                    observer));

        Assert.Single(
            result.Pages);

        Assert.Equal(
            0,
            visualSource.ObserveCallCount);

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
            "synthetic coordinated shadow failure",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CoordinatedExtractionFailure_RemainsAuthoritative()
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

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    ProcessNativeAsync(
                        new FaultingCoordinatedExtractor(
                            extraction,
                            _ =>
                                throw new InvalidDataException(
                                    "synthetic authoritative extraction failure")),
                        Shadow(
                            new StubVisualSource(
                                [
                                    new PageVisualRasterObservations(
                                        1,
                                        [])
                                ]),
                            observer)));

        Assert.Contains(
            "authoritative extraction failure",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Empty(
            observer.Reports);
    }

    [Fact]
    public async Task ProcessAsync_CoordinatedCallerCancellation_Propagates()
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

        using var cancellation =
            new CancellationTokenSource();

        var extractor =
            new FaultingCoordinatedExtractor(
                extraction,
                cancellationToken =>
                {
                    cancellation.Cancel();
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    throw new InvalidOperationException(
                        "unreachable");
                });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                ProcessNativeAsync(
                    extractor,
                    Shadow(
                        new StubVisualSource(
                            [
                                new PageVisualRasterObservations(
                                    1,
                                    [])
                            ]),
                        new RecordingObserver()),
                    cancellation.Token));
    }

    [Fact]
    public async Task ProcessAsync_CoordinatedOutOfMemory_Propagates()
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

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () =>
                ProcessNativeAsync(
                    new FaultingCoordinatedExtractor(
                        extraction,
                        _ =>
                            throw new OutOfMemoryException(
                                "synthetic fatal allocation failure")),
                    Shadow(
                        new StubVisualSource(
                            [
                                new PageVisualRasterObservations(
                                    1,
                                    [])
                            ]),
                        new RecordingObserver())));
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

    private static Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessNativeAsync(
            DocumentExtractionResult extraction,
            DocumentShadowPlanningDependencies? shadow,
            CancellationToken cancellationToken = default) =>
        ProcessNativeAsync(
            new StubExtractor(
                extraction),
            shadow,
            cancellationToken);

    private static async Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessNativeAsync(
            IDocumentExtractor extractor,
            DocumentShadowPlanningDependencies? shadow,
            CancellationToken cancellationToken = default)
    {
        var processor =
            new DocumentProcessor(
                new StubDetector(),
                extractor,
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
                "application/pdf"),
            cancellationToken);
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

    private sealed class CoordinatedStubExtractor
        : IDocumentExtractorWithRasterObservations
    {
        private readonly DocumentExtractionResult _extraction;
        private readonly IReadOnlyList<PageVisualRasterObservations>?
            _rasterObservations;
        private readonly RasterObservationAcquisitionFailure?
            _rasterObservationFailure;

        private CoordinatedStubExtractor(
            DocumentExtractionResult extraction,
            IReadOnlyList<PageVisualRasterObservations>? rasterObservations,
            RasterObservationAcquisitionFailure? rasterObservationFailure)
        {
            _extraction =
                extraction;

            _rasterObservations =
                rasterObservations;

            _rasterObservationFailure =
                rasterObservationFailure;
        }

        public int FallbackExtractCallCount { get; private set; }

        public int CoordinatedExtractCallCount { get; private set; }

        public static CoordinatedStubExtractor Success(
            DocumentExtractionResult extraction,
            IReadOnlyList<PageVisualRasterObservations> rasterObservations) =>
            new(
                extraction,
                rasterObservations,
                rasterObservationFailure:
                    null);

        public static CoordinatedStubExtractor FailedRasterObservation(
            DocumentExtractionResult extraction,
            RasterObservationAcquisitionFailure failure) =>
            new(
                extraction,
                rasterObservations:
                    null,
                rasterObservationFailure:
                    failure);

        public bool CanExtract(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public bool CanExtractWithRasterObservations(
            DocumentFormatId format,
            IVisualRasterObservationSource rasterObservationSource) =>
            CanExtract(
                format) &&
            rasterObservationSource.CanObserve(
                format);

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FallbackExtractCallCount++;

            return ValueTask.FromResult(
                _extraction);
        }

        public ValueTask<DocumentExtractionWithRasterObservationsResult>
            ExtractWithRasterObservationsAsync(
                DocumentSource source,
                DocumentFormatId format,
                IVisualRasterObservationSource rasterObservationSource,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CoordinatedExtractCallCount++;

            return ValueTask.FromResult(
                new DocumentExtractionWithRasterObservationsResult(
                    _extraction,
                    _rasterObservations,
                    _rasterObservationFailure));
        }
    }

    private sealed class FaultingCoordinatedExtractor(
        DocumentExtractionResult extraction,
        Func<CancellationToken, DocumentExtractionWithRasterObservationsResult>
            fault)
        : IDocumentExtractorWithRasterObservations
    {
        public bool CanExtract(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public bool CanExtractWithRasterObservations(
            DocumentFormatId format,
            IVisualRasterObservationSource rasterObservationSource) =>
            CanExtract(
                format) &&
            rasterObservationSource.CanObserve(
                format);

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Fallback extraction must not run for this coordinated test.");

        public ValueTask<DocumentExtractionWithRasterObservationsResult>
            ExtractWithRasterObservationsAsync(
                DocumentSource source,
                DocumentFormatId format,
                IVisualRasterObservationSource rasterObservationSource,
                CancellationToken cancellationToken = default)
        {
            _ =
                extraction;

            return ValueTask.FromResult(
                fault(
                    cancellationToken));
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
