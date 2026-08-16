using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentControlledCandidateTextExecutionTests
{
    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ProcessAsync_ControlledNativeTextExecution_DoesNotChangeAuthoritativeResult()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var baseline =
            await ProcessNativeAsync(
                extraction,
                shadow:
                    null,
                controlled:
                    null);

        var candidateObserver =
            new RecordingCandidateObserver();

        var actual =
            await ProcessNativeAsync(
                extraction,
                Shadow(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [])
                        ])),
                new DocumentControlledCandidateTextExecutionDependencies(
                    candidateObserver));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                candidateObserver.Reports);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.ExecutedNativeTextPageCount);

        Assert.Equal(
            0,
            report.DeferredNonNativeTextPageCount);

        var page =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            DocumentControlledCandidateTextPageStatus.ExecutedNativeText,
            page.Status);

        Assert.True(
            page.SelectedTextSequenceExact is true);

        Assert.True(
            page.TextProjectionExact is true);

        Assert.False(
            page.CandidateHasIndependentVisualWork);
    }

    [Fact]
    public void Constructor_ControlledExecutionWithoutShadowPlanning_FailsFast()
    {
        var observer =
            new RecordingCandidateObserver();

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentProcessor(
                        new StubDetector(),
                        new StubExtractor(
                            Extraction(
                                Page(
                                    1,
                                    "Stable native text.",
                                    0,
                                    0))),
                        new StubPreflightAnalyzer(),
                        "test-engine-h4d1-v1",
                        NativeIdentity,
                        shadowPlanning:
                            null,
                        controlledCandidateTextExecution:
                            new DocumentControlledCandidateTextExecutionDependencies(
                                observer)));

        Assert.Contains(
            "requires H.4C shadow planning",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_FailedShadowPlanning_SkipsControlledExecutionAndKeepsAuthority()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var baseline =
            await ProcessNativeAsync(
                extraction,
                shadow:
                    null,
                controlled:
                    null);

        var candidateObserver =
            new RecordingCandidateObserver();

        var actual =
            await ProcessNativeAsync(
                extraction,
                Shadow(
                    new ThrowingVisualSource()),
                new DocumentControlledCandidateTextExecutionDependencies(
                    candidateObserver));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                candidateObserver.Reports);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.PlanningUnavailable,
            report.Status);

        Assert.Empty(
            report.Pages);

        Assert.Null(
            report.Failure);
    }

    [Fact]
    public async Task Runner_UnverifiedPresentationOnlyCandidate_ActuallyExecutesNativeTextAndRecordsLegacyMlRemoval()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Native text remains authoritative after deterministic visual verification.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.67));

        var shadow =
            await BuildShadowReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [
                            BlankRaster()
                        ])
                ]);

        var shadowPage =
            Assert.Single(
                shadow.Pages);

        Assert.True(
            shadowPage.CandidateRemovesLegacyTextMl);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            shadowPage.AuthoritativeLegacy.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            shadowPage.Shadow.Candidate.Plan.TextMode);

        var authoritative =
            AssembleNativeForTest(
                Assert.Single(
                    extraction.Pages));

        var observer =
            new RecordingCandidateObserver();

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    observer));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    authoritative
                ],
                shadow,
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.ExecutedCandidateRemovesLegacyTextMlCount);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            DocumentControlledCandidateTextPageStatus.ExecutedNativeText,
            comparison.Status);

        Assert.True(
            comparison.CandidateRemovesLegacyTextMl);

        Assert.True(
            comparison.SelectedTextSequenceExact is true);

        Assert.True(
            comparison.TextProjectionExact is true);

        Assert.Equal(
            0,
            comparison.CandidateReconciliationEvidenceCount);
    }

    [Fact]
    public async Task Runner_OcrBackedCandidate_IsExplicitlyDeferred()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Native text contains replacement \uFFFD evidence.",
                    rasterImageCount:
                        1,
                    largestRasterImageAreaRatio:
                        0.67));

        var shadow =
            await BuildShadowReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [
                            BlankRaster()
                        ])
                ]);

        var candidateMode =
            Assert.Single(
                shadow.Pages)
                .Shadow
                .Candidate
                .Plan
                .TextMode;

        Assert.Equal(
            TextExecutionMode.TargetedOcrReconciliation,
            candidateMode);

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver()));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    AssembleNativeForTest(
                        Assert.Single(
                            extraction.Pages))
                ],
                shadow,
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            0,
            report.ExecutedNativeTextPageCount);

        Assert.Equal(
            1,
            report.DeferredNonNativeTextPageCount);

        var page =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode,
            page.Status);

        Assert.Null(
            page.SelectedTextSequenceExact);

        Assert.Null(
            page.TextProjectionExact);
    }

    [Fact]
    public async Task Runner_OrdinaryCandidateExecutionFailure_IsReportedNotThrown()
    {
        var validExtraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    rasterImageCount:
                        0,
                    largestRasterImageAreaRatio:
                        0));

        var shadow =
            await BuildShadowReportAsync(
                validExtraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var inconsistentExtraction =
            Extraction(
                new DocumentExtractionPage(
                    1,
                    "Stable native text.",
                    wordCount:
                        1,
                    blocks:
                        []));

        var observer =
            new RecordingCandidateObserver();

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    observer));

        var report =
            await runner.RunAsync(
                inconsistentExtraction,
                [
                    new HybridDocumentPage(
                        1)
                ],
                shadow,
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Failed,
            report.Status);

        Assert.Empty(
            report.Pages);

        var failure =
            Assert.IsType<DocumentControlledCandidateTextExecutionFailure>(
                report.Failure);

        Assert.Equal(
            1,
            failure.PhysicalPageNumber);

        Assert.Contains(
            "contains no native text blocks",
            failure.Message,
            StringComparison.Ordinal);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_CallerCancellation_Propagates()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    0,
                    0));

        var shadow =
            await BuildShadowReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                runner.RunAsync(
                        extraction,
                        [
                            AssembleNativeForTest(
                                Assert.Single(
                                    extraction.Pages))
                        ],
                        shadow,
                        SourceSha,
                        cancellation.Token)
                    .AsTask());
    }

    [Fact]
    public async Task Runner_ObserverOutOfMemory_Propagates()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    0,
                    0));

        var shadow =
            await BuildShadowReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new OutOfMemoryCandidateObserver()));

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () =>
                runner.RunAsync(
                        extraction,
                        [
                            AssembleNativeForTest(
                                Assert.Single(
                                    extraction.Pages))
                        ],
                        shadow,
                        SourceSha)
                    .AsTask());
    }

    [Fact]
    public async Task Runner_OrdinaryObserverFailure_IsBestEffort()
    {
        var extraction =
            Extraction(
                Page(
                    1,
                    "Stable native text.",
                    0,
                    0));

        var shadow =
            await BuildShadowReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new ThrowingCandidateObserver()));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    AssembleNativeForTest(
                        Assert.Single(
                            extraction.Pages))
                ],
                shadow,
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);
    }

    private static DocumentShadowPlanningDependencies Shadow(
        IVisualRasterObservationSource source) =>
        new(
            source,
            new NoOpShadowObserver());

    private static async Task<DocumentShadowPlanningReport> BuildShadowReportAsync(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageVisualRasterObservations> rasterObservations)
    {
        var authoritative =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction);

        var runner =
            new DocumentShadowPlanningRunner(
                Shadow(
                    new StubVisualSource(
                        rasterObservations)));

        await using var stream =
            new MemoryStream(
                "%PDF-h4d1-shadow"u8.ToArray(),
                writable:
                    false);

        return await runner.RunAsync(
            new DocumentSource(
                stream,
                "h4d1.pdf",
                "application/pdf"),
            DocumentFormatId.Pdf,
            extraction,
            authoritative,
            SourceSha);
    }

    private static async Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessNativeAsync(
            DocumentExtractionResult extraction,
            DocumentShadowPlanningDependencies? shadow,
            DocumentControlledCandidateTextExecutionDependencies? controlled)
    {
        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                "test-engine-h4d1-v1",
                NativeIdentity,
                shadow,
                controlled);

        await using var stream =
            new MemoryStream(
                "%PDF-h4d1-document"u8.ToArray(),
                writable:
                    false);

        return await processor.ProcessAsync(
            new DocumentSource(
                stream,
                "h4d1.pdf",
                "application/pdf"));
    }

    private static HybridDocumentPage AssembleNativeForTest(
        DocumentExtractionPage page) =>
        HybridDocumentAssembler
            .AssemblePage(
                page,
                page.Blocks.Select(
                    block =>
                        HybridDocumentElementFactory
                            .FromNative(
                                page.PhysicalPageNumber,
                                block)));

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
        IReadOnlyList<PageVisualRasterObservations> pages)
        : IVisualRasterObservationSource
    {
        public bool CanObserve(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<IReadOnlyList<PageVisualRasterObservations>> ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

    private sealed class NoOpShadowObserver
        : IDocumentShadowPlanningObserver
    {
        public ValueTask ObserveAsync(
            DocumentShadowPlanningReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCandidateObserver
        : IDocumentControlledCandidateTextExecutionObserver
    {
        public List<DocumentControlledCandidateTextExecutionReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentControlledCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingCandidateObserver
        : IDocumentControlledCandidateTextExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "synthetic candidate observer failure");
    }

    private sealed class OutOfMemoryCandidateObserver
        : IDocumentControlledCandidateTextExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default) =>
            throw new OutOfMemoryException(
                "synthetic candidate observer OOM");
    }
}
