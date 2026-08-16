using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentControlledCandidateVisualExecutionTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string VisualSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    [Fact]
    public async Task Runner_MixedVisualActions_ExecutesIndependentBranches()
    {
        var materializer =
            new CountingSourceVisualAssetMaterializer();

        var rasterizer =
            new CountingDocumentRasterizer();

        var layoutAnalyzer =
            new CountingLayoutAnalyzer();

        var observer =
            new RecordingVisualObserver();

        var runner =
            CreateRunner(
                observer,
                materializer,
                rasterizer,
                layoutAnalyzer);

        var extraction =
            Extraction(
                rasterImageCount:
                    3);

        var report =
            await RunAsync(
                runner,
                extraction,
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing,
                    VisualExecutionAction.PreserveMeaningfulVisual,
                    VisualExecutionAction.AnalyzeVisual));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.NoAdditionalSemanticProcessingElementCount);

        Assert.Equal(
            1,
            report.PreservationElementCount);

        Assert.Equal(
            1,
            report.AnalysisElementCount);

        Assert.Equal(
            1,
            report.AnalyzedPageCount);

        Assert.Equal(
            1,
            report.CandidateAddsIndependentVisualWorkToLegacyNativePageCount);

        Assert.Equal(
            (
                1,
                1),
            Assert.Single(
                materializer.Calls));

        Assert.Equal(
            1,
            rasterizer.OpenCount);

        Assert.Equal(
            1,
            rasterizer.Session.RenderPageCount);

        Assert.Equal(
            1,
            layoutAnalyzer.CallCount);

        var page =
            Assert.Single(
                report.Pages);

        Assert.NotNull(
            page.AnalysisRaster);

        Assert.NotNull(
            page.AnalysisLayout);

        Assert.Null(
            page.Elements[0].Materialization);

        Assert.Equal(
            VisualSha,
            Assert.IsType<SourceVisualAssetMaterialization>(
                    page.Elements[1].Materialization)
                .ContentSha256);

        Assert.Null(
            page.Elements[2].Materialization);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_PreserveOnly_DoesNotOpenRasterOrInvokeLayout()
    {
        var materializer =
            new CountingSourceVisualAssetMaterializer();

        var rasterizer =
            new CountingDocumentRasterizer();

        var layoutAnalyzer =
            new CountingLayoutAnalyzer();

        var report =
            await RunAsync(
                CreateRunner(
                    new RecordingVisualObserver(),
                    materializer,
                    rasterizer,
                    layoutAnalyzer),
                Extraction(
                    rasterImageCount:
                        1),
                Shadow(
                    VisualExecutionAction.PreserveMeaningfulVisual));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.PreservationElementCount);

        Assert.Equal(
            (
                1,
                0),
            Assert.Single(
                materializer.Calls));

        Assert.Equal(
            0,
            rasterizer.OpenCount);

        Assert.Equal(
            0,
            rasterizer.Session.RenderPageCount);

        Assert.Equal(
            0,
            layoutAnalyzer.CallCount);
    }

    [Fact]
    public async Task Runner_MultipleAnalyzeVisuals_RasterizesAndAnalyzesPageOnce()
    {
        var rasterizer =
            new CountingDocumentRasterizer();

        var layoutAnalyzer =
            new CountingLayoutAnalyzer();

        var report =
            await RunAsync(
                CreateRunner(
                    new RecordingVisualObserver(),
                    new CountingSourceVisualAssetMaterializer(),
                    rasterizer,
                    layoutAnalyzer),
                Extraction(
                    rasterImageCount:
                        2),
                Shadow(
                    VisualExecutionAction.AnalyzeVisual,
                    VisualExecutionAction.AnalyzeVisual));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            2,
            report.AnalysisElementCount);

        Assert.Equal(
            1,
            report.AnalyzedPageCount);

        Assert.Equal(
            1,
            rasterizer.OpenCount);

        Assert.Equal(
            1,
            rasterizer.Session.RenderPageCount);

        Assert.Equal(
            1,
            layoutAnalyzer.CallCount);
    }

    [Fact]
    public async Task Runner_NoAdditionalSemanticProcessing_PerformsNoVisualIo()
    {
        var materializer =
            new CountingSourceVisualAssetMaterializer();

        var rasterizer =
            new CountingDocumentRasterizer();

        var layoutAnalyzer =
            new CountingLayoutAnalyzer();

        var report =
            await RunAsync(
                CreateRunner(
                    new RecordingVisualObserver(),
                    materializer,
                    rasterizer,
                    layoutAnalyzer),
                Extraction(
                    rasterImageCount:
                        2),
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing,
                    VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            report.Status);

        Assert.Equal(
            2,
            report.NoAdditionalSemanticProcessingElementCount);

        Assert.Empty(
            materializer.Calls);

        Assert.Equal(
            0,
            rasterizer.OpenCount);

        Assert.Equal(
            0,
            layoutAnalyzer.CallCount);
    }

    [Fact]
    public async Task Runner_OrdinaryPreservationFailure_IsReportedAndDoesNotThrow()
    {
        var observer =
            new RecordingVisualObserver();

        var runner =
            CreateRunner(
                observer,
                new ThrowingSourceVisualAssetMaterializer(
                    new InvalidOperationException(
                        "synthetic preservation failure")),
                new CountingDocumentRasterizer(),
                new CountingLayoutAnalyzer());

        var report =
            await RunAsync(
                runner,
                Extraction(
                    rasterImageCount:
                        1),
                Shadow(
                    VisualExecutionAction.PreserveMeaningfulVisual));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Failed,
            report.Status);

        Assert.Empty(
            report.Pages);

        var failure =
            Assert.IsType<DocumentControlledCandidateVisualExecutionFailure>(
                report.Failure);

        Assert.Equal(
            1,
            failure.PhysicalPageNumber);

        Assert.Equal(
            0,
            failure.SourceVisualIndex);

        Assert.Contains(
            "synthetic preservation failure",
            failure.Message,
            StringComparison.Ordinal);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_OrdinaryAnalysisFailure_IsReportedAndDoesNotThrow()
    {
        var observer =
            new RecordingVisualObserver();

        var runner =
            CreateRunner(
                observer,
                new CountingSourceVisualAssetMaterializer(),
                new CountingDocumentRasterizer(),
                new ThrowingLayoutAnalyzer(
                    new InvalidOperationException(
                        "synthetic visual analysis failure")));

        var report =
            await RunAsync(
                runner,
                Extraction(
                    rasterImageCount:
                        1),
                Shadow(
                    VisualExecutionAction.AnalyzeVisual));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Failed,
            report.Status);

        var failure =
            Assert.IsType<DocumentControlledCandidateVisualExecutionFailure>(
                report.Failure);

        Assert.Equal(
            1,
            failure.PhysicalPageNumber);

        Assert.Equal(
            0,
            failure.SourceVisualIndex);

        Assert.Contains(
            "synthetic visual analysis failure",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_SourceMaterializationOutOfMemory_Propagates()
    {
        var runner =
            CreateRunner(
                new RecordingVisualObserver(),
                new ThrowingSourceVisualAssetMaterializer(
                    new OutOfMemoryException(
                        "synthetic visual OOM")),
                new CountingDocumentRasterizer(),
                new CountingLayoutAnalyzer());

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () =>
                RunAsync(
                        runner,
                        Extraction(
                            rasterImageCount:
                                1),
                        Shadow(
                            VisualExecutionAction.PreserveMeaningfulVisual))
                    .AsTask());
    }

    [Fact]
    public async Task Runner_CallerCancellation_Propagates()
    {
        var runner =
            CreateRunner(
                new RecordingVisualObserver(),
                new CountingSourceVisualAssetMaterializer(),
                new CountingDocumentRasterizer(),
                new CountingLayoutAnalyzer());

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RunAsync(
                        runner,
                        Extraction(
                            rasterImageCount:
                                1),
                        Shadow(
                            VisualExecutionAction.AnalyzeVisual),
                        cancellation.Token)
                    .AsTask());
    }

    [Fact]
    public async Task Runner_ObserverOrdinaryFailure_IsBestEffort()
    {
        var runner =
            CreateRunner(
                new ThrowingVisualObserver(
                    new InvalidOperationException(
                        "synthetic observer failure")),
                new CountingSourceVisualAssetMaterializer(),
                new CountingDocumentRasterizer(),
                new CountingLayoutAnalyzer());

        var report =
            await RunAsync(
                runner,
                Extraction(
                    rasterImageCount:
                        1),
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            report.Status);
    }

    [Fact]
    public async Task ProcessAsync_OrdinaryControlledVisualFailure_RemainsFailOpenAfterAuthority()
    {
        var extraction =
            Extraction(
                rasterImageCount:
                    1,
                largestRasterImageAreaRatio:
                    0.10);

        var baseline =
            await ProcessAsync(
                extraction,
                controlledVisual:
                    null);

        var observer =
            new RecordingVisualObserver();

        var actual =
            await ProcessAsync(
                extraction,
                new DocumentControlledCandidateVisualExecutionDependencies(
                    observer,
                    new CountingSourceVisualAssetMaterializer(),
                    new ThrowingDocumentRasterizer(
                        new InvalidOperationException(
                            "synthetic post-authority visual failure")),
                    new CountingLayoutAnalyzer()));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentControlledCandidateVisualExecutionStatus.Failed,
            report.Status);

        var failure =
            Assert.IsType<DocumentControlledCandidateVisualExecutionFailure>(
                report.Failure);

        Assert.Equal(
            1,
            failure.PhysicalPageNumber);

        Assert.Equal(
            0,
            failure.SourceVisualIndex);

        Assert.Contains(
            "synthetic post-authority visual failure",
            failure.Message,
            StringComparison.Ordinal);
    }

    private static DocumentControlledCandidateVisualExecutionRunner CreateRunner(
        IDocumentControlledCandidateVisualExecutionObserver observer,
        ISourceVisualAssetMaterializer materializer,
        IDocumentRasterizer rasterizer,
        IPageLayoutAnalyzer layoutAnalyzer) =>
        new(
            new DocumentControlledCandidateVisualExecutionDependencies(
                observer,
                materializer,
                rasterizer,
                layoutAnalyzer));

    private static async ValueTask<DocumentControlledCandidateVisualExecutionReport>
        RunAsync(
            DocumentControlledCandidateVisualExecutionRunner runner,
            DocumentExtractionResult extraction,
            DocumentShadowPlanningReport shadow,
            CancellationToken cancellationToken = default)
    {
        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-visual"u8.ToArray(),
                writable:
                    false);

        return await runner
            .RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled-visual.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                extraction,
                shadow,
                SourceSha,
                cancellationToken);
    }

    private static DocumentExtractionResult Extraction(
        int rasterImageCount,
        double largestRasterImageAreaRatio = 0.10) =>
        new(
            DocumentFormatId.Pdf,
            [
                NativePage(
                    1,
                    rasterImageCount,
                    largestRasterImageAreaRatio)
            ]);

    private static DocumentExtractionPage NativePage(
        int physicalPageNumber,
        int rasterImageCount,
        double largestRasterImageAreaRatio)
    {
        const string text =
            "Native text.";

        var words =
            new[]
            {
                new DocumentWord(
                    0,
                    "Native",
                    new NormalizedRectangle(
                        0.10,
                        0.10,
                        0.20,
                        0.14),
                    "Body",
                    10),
                new DocumentWord(
                    1,
                    "text.",
                    new NormalizedRectangle(
                        0.21,
                        0.10,
                        0.30,
                        0.14),
                    "Body",
                    10)
            };

        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.30),
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
            wordCount:
                words.Length,
            rasterImageCount:
                rasterImageCount,
            largestRasterImageAreaRatio:
                largestRasterImageAreaRatio,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                words,
            blocks:
                [
                    block
                ]);
    }

    private static DocumentShadowPlanningReport Shadow(
        params VisualExecutionAction[] actions)
    {
        var assessment =
            new PageProcessingAssessment(
                1,
                NativeTextStatus.Healthy);

        var legacy =
            new PageProcessingDecision(
                assessment,
                new PageProcessingPlan(
                    PageProcessingRoute.NativeOnly));

        var evidence =
            new PageProcessingEvidence(
                1,
                TextAuthority.Trusted,
                actions.Select(
                    (action, index) =>
                        new VisualElementEvidence(
                            index,
                            EvidenceFor(
                                action))));

        var requirements =
            new PageProcessingRequirements(
                1,
                TextProcessingRequirement.UseNativeText,
                actions.Select(
                    (action, index) =>
                        new VisualElementDisposition(
                            index,
                            DispositionFor(
                                action))));

        var plan =
            new PageExecutionPlan(
                1,
                TextExecutionMode.NativeText,
                actions.Select(
                    (action, index) =>
                        new VisualElementExecutionPlan(
                            index,
                            action)));

        var candidate =
            new PageExecutionPlanningDecision(
                assessment,
                evidence,
                requirements,
                plan);

        var guarded =
            new GuardedPagePlanningDecision(
                legacy,
                candidate);

        return new DocumentShadowPlanningReport(
            SourceSha,
            DocumentFormatId.Pdf,
            DocumentShadowPlanningStatus.Completed,
            [
                new DocumentShadowPageComparison(
                    legacy,
                    guarded)
            ]);
    }

    private static VisualEvidenceKind EvidenceFor(
        VisualExecutionAction action) =>
        action switch
        {
            VisualExecutionAction.NoAdditionalSemanticProcessing =>
                VisualEvidenceKind.TinyOrNoise,

            VisualExecutionAction.PreserveMeaningfulVisual =>
                VisualEvidenceKind.LargeIndependentVisual,

            VisualExecutionAction.AnalyzeVisual =>
                VisualEvidenceKind.Unknown,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(action))
        };

    private static VisualDisposition DispositionFor(
        VisualExecutionAction action) =>
        action switch
        {
            VisualExecutionAction.NoAdditionalSemanticProcessing =>
                VisualDisposition.PresentationOnly,

            VisualExecutionAction.PreserveMeaningfulVisual =>
                VisualDisposition.PreserveMeaningfulVisual,

            VisualExecutionAction.AnalyzeVisual =>
                VisualDisposition.RequiresVisualAnalysis,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(action))
        };

    private static async Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessAsync(
            DocumentExtractionResult extraction,
            DocumentControlledCandidateVisualExecutionDependencies?
                controlledVisual)
    {
        var shadow =
            controlledVisual is null
                ? null
                : new DocumentShadowPlanningDependencies(
                    new UnknownVisualRasterObservationSource(),
                    new NoOpShadowObserver());

        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                "test-engine-h4d3b-v1",
                NativeIdentity,
                shadowPlanning:
                    shadow,
                controlledCandidateTextExecution:
                    null,
                controlledCandidateVisualExecution:
                    controlledVisual);

        await using var stream =
            new MemoryStream(
                "%PDF-h4d3b-authority"u8.ToArray(),
                writable:
                    false);

        return await processor
            .ProcessAsync(
                new DocumentSource(
                    stream,
                    "h4d3b.pdf",
                    "application/pdf"));
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

    private sealed class CountingSourceVisualAssetMaterializer
        : ISourceVisualAssetMaterializer
    {
        public List<(int PhysicalPageNumber, int SourceVisualIndex)> Calls { get; } =
            [];

        public bool CanMaterialize(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public async ValueTask<SourceVisualAssetMaterialization> MaterializeAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            int physicalPageNumber,
            int sourceVisualIndex,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls.Add(
                (
                    physicalPageNumber,
                    sourceVisualIndex));

            await destination
                .WriteAsync(
                    new byte[]
                    {
                        7
                    },
                    cancellationToken);

            return new SourceVisualAssetMaterialization(
                physicalPageNumber,
                sourceVisualIndex,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.70,
                    0.80),
                "fake-source-visual-v1",
                "image/png",
                1,
                VisualSha);
        }
    }

    private sealed class ThrowingSourceVisualAssetMaterializer(
        Exception exception)
        : ISourceVisualAssetMaterializer
    {
        public bool CanMaterialize(
            DocumentFormatId format) =>
            true;

        public ValueTask<SourceVisualAssetMaterialization> MaterializeAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            int physicalPageNumber,
            int sourceVisualIndex,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class CountingDocumentRasterizer
        : IDocumentRasterizer
    {
        public CountingRasterizationSession Session { get; } =
            new();

        public int OpenCount { get; private set; }

        public bool CanRasterize(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<IDocumentRasterizationSession> OpenAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OpenCount++;

            return ValueTask.FromResult<IDocumentRasterizationSession>(
                Session);
        }
    }

    private sealed class ThrowingDocumentRasterizer(
        Exception exception)
        : IDocumentRasterizer
    {
        public bool CanRasterize(
            DocumentFormatId format) =>
            true;

        public ValueTask<IDocumentRasterizationSession> OpenAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class CountingRasterizationSession
        : IDocumentRasterizationSession
    {
        private const string RasterSha =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public int RenderPageCount { get; private set; }

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

            RenderPageCount++;

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
                    RasterSha));
        }

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "H.4D.3B AnalyzeVisual must not render OCR-style regions.");

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class CountingLayoutAnalyzer
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
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return ValueTask.FromResult(
                new LayoutAnalysisResult(
                    "fake-layout",
                    physicalPageNumber,
                    []));
        }
    }

    private sealed class ThrowingLayoutAnalyzer(
        Exception exception)
        : IPageLayoutAnalyzer
    {
        public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterImage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class RecordingVisualObserver
        : IDocumentControlledCandidateVisualExecutionObserver
    {
        public List<DocumentControlledCandidateVisualExecutionReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentControlledCandidateVisualExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingVisualObserver(
        Exception exception)
        : IDocumentControlledCandidateVisualExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateVisualExecutionReport report,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

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
            DocumentExtractionResult extraction) =>
            new(
                extraction.Format,
                extraction.Pages.Count,
                extraction.Pages.Count,
                0,
                100,
                [],
                [],
                DocumentPreflightClassification.HealthyBornDigital);
    }

    private sealed class UnknownVisualRasterObservationSource
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

            return ValueTask.FromResult<IReadOnlyList<PageVisualRasterObservations>>(
                [
                    new PageVisualRasterObservations(
                        1,
                        [
                            new VisualRasterObservation(
                                0,
                                new NormalizedRectangle(
                                    0.10,
                                    0.20,
                                    0.70,
                                    0.80),
                                VisualRasterDecodeSource.Unavailable,
                                null,
                                null,
                                null,
                                VisualForegroundState.Unavailable,
                                null,
                                VisualPixelInteractionKind.NotMeasured,
                                0,
                                null,
                                null)
                        ])
                ]);
        }
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
}
