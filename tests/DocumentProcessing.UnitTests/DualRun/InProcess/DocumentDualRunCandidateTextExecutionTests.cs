using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.DualRun.InProcess;

namespace DocumentProcessing.UnitTests.DualRun.InProcess;

public sealed class DocumentDualRunCandidateTextExecutionTests
{
    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ProcessAsync_DualRunCandidateNativeTextExecution_DoesNotChangeAuthoritativeResult()
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
                dualRun:
                    null,
                dualRunCandidateTextExecution:
                    null);

        var candidateObserver =
            new RecordingCandidateObserver();

        var actual =
            await ProcessNativeAsync(
                extraction,
                DualRunPlanningDependencies(
                    new StubVisualSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [])
                        ])),
                new DocumentDualRunCandidateTextExecutionDependencies(
                    candidateObserver));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                candidateObserver.Reports);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Completed,
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
            DocumentDualRunCandidateTextPageStatus.ExecutedNativeText,
            page.Status);

        Assert.True(
            page.SelectedTextSequenceExact is true);

        Assert.True(
            page.TextProjectionExact is true);

        Assert.False(
            page.CandidateHasIndependentVisualWork);
    }

    [Fact]
    public void Constructor_DualRunCandidateExecutionWithoutPlanning_FailsFast()
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
                        dualRunPlanning:
                            null,
                        dualRunCandidateTextExecution:
                            new DocumentDualRunCandidateTextExecutionDependencies(
                                observer)));

        Assert.Contains(
            "requires Dual Run planning",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_FailedDualRunPlanning_SkipsCandidateExecutionAndKeepsAuthority()
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
                dualRun:
                    null,
                dualRunCandidateTextExecution:
                    null);

        var candidateObserver =
            new RecordingCandidateObserver();

        var actual =
            await ProcessNativeAsync(
                extraction,
                DualRunPlanningDependencies(
                    new ThrowingVisualSource()),
                new DocumentDualRunCandidateTextExecutionDependencies(
                    candidateObserver));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                candidateObserver.Reports);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.PlanningUnavailable,
            report.Status);

        Assert.Empty(
            report.Pages);

        Assert.Null(
            report.Failure);
    }

    [Fact]
    public async Task Runner_UnverifiedPresentationOnlyCandidate_ActuallyExecutesNativeTextAndRecordsAuthoritativeMlRemoval()
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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [
                            BlankRaster()
                        ])
                ]);

        var dualRunPage =
            Assert.Single(
                dualRunPlanning.Pages);

        Assert.True(
            dualRunPage.CandidateRemovesAuthoritativeTextMl);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            dualRunPage.Authoritative.Plan.Route);

        Assert.Equal(
            TextExecutionMode.NativeText,
            dualRunPage.DualRun.Candidate.Plan.TextMode);

        var authoritative =
            AssembleNativeForTest(
                Assert.Single(
                    extraction.Pages));

        var observer =
            new RecordingCandidateObserver();

        var runner =
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
                    observer));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    authoritative
                ],
                dualRunPlanning,
                SourceSha);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.ExecutedCandidateRemovesAuthoritativeTextMlCount);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            DocumentDualRunCandidateTextPageStatus.ExecutedNativeText,
            comparison.Status);

        Assert.True(
            comparison.CandidateRemovesAuthoritativeTextMl);

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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
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
                dualRunPlanning.Pages)
                .DualRun
                .Candidate
                .Plan
                .TextMode;

        Assert.Equal(
            TextExecutionMode.TargetedOcrReconciliation,
            candidateMode);

        var runner =
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver()));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    AssembleNativeForTest(
                        Assert.Single(
                            extraction.Pages))
                ],
                dualRunPlanning,
                SourceSha);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Completed,
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
            DocumentDualRunCandidateTextPageStatus.DeferredNonNativeTextMode,
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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
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
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
                    observer));

        var report =
            await runner.RunAsync(
                inconsistentExtraction,
                [
                    new HybridDocumentPage(
                        1)
                ],
                dualRunPlanning,
                SourceSha);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Failed,
            report.Status);

        Assert.Empty(
            report.Pages);

        var failure =
            Assert.IsType<DocumentDualRunCandidateTextExecutionFailure>(
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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
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
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
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
                        dualRunPlanning,
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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var runner =
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
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
                        dualRunPlanning,
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

        var dualRunPlanning =
            await BuildDualRunPlanningReportAsync(
                extraction,
                [
                    new PageVisualRasterObservations(
                        1,
                        [])
                ]);

        var runner =
            new DocumentDualRunCandidateTextExecutionRunner(
                new DocumentDualRunCandidateTextExecutionDependencies(
                    new ThrowingCandidateObserver()));

        var report =
            await runner.RunAsync(
                extraction,
                [
                    AssembleNativeForTest(
                        Assert.Single(
                            extraction.Pages))
                ],
                dualRunPlanning,
                SourceSha);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Completed,
            report.Status);
    }

    private static DocumentDualRunPlanningDependencies DualRunPlanningDependencies(
        IVisualRasterObservationSource source) =>
        new(
            source,
            new NoOpDualRunPlanningObserver());

    private static async Task<DocumentDualRunPlanningReport> BuildDualRunPlanningReportAsync(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageVisualRasterObservations> rasterObservations)
    {
        var authoritative =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction);

        var runner =
            new DocumentDualRunPlanningRunner(
                DualRunPlanningDependencies(
                    new StubVisualSource(
                        rasterObservations)));

        await using var stream =
            new MemoryStream(
                "%PDF-h4d1-dual-run"u8.ToArray(),
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
            DocumentDualRunPlanningDependencies? dualRun,
            DocumentDualRunCandidateTextExecutionDependencies? dualRunCandidateTextExecution)
    {
        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                "test-engine-h4d1-v1",
                NativeIdentity,
                dualRun,
                dualRunCandidateTextExecution);

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
                "synthetic dualRun failure");
    }

    private sealed class NoOpDualRunPlanningObserver
        : IDocumentDualRunPlanningObserver
    {
        public ValueTask ObserveAsync(
            DocumentDualRunPlanningReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCandidateObserver
        : IDocumentDualRunCandidateTextExecutionObserver
    {
        public List<DocumentDualRunCandidateTextExecutionReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentDualRunCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingCandidateObserver
        : IDocumentDualRunCandidateTextExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentDualRunCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "synthetic candidate observer failure");
    }

    private sealed class OutOfMemoryCandidateObserver
        : IDocumentDualRunCandidateTextExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentDualRunCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default) =>
            throw new OutOfMemoryException(
                "synthetic candidate observer OOM");
    }
}
