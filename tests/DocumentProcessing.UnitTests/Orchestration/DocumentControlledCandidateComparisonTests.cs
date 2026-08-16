using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentControlledCandidateComparisonTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    [Fact]
    public async Task Runner_ExactCrossAxisEvidence_RemainsBlockedUntilOutputAndProvenanceExist()
    {
        var observer =
            new RecordingComparisonObserver();

        var report =
            await new DocumentControlledCandidateComparisonRunner(
                    new DocumentControlledCandidateComparisonDependencies(
                        observer))
                .RunAsync(
                    AuthoritativeResult(),
                    Shadow(
                        VisualExecutionAction.NoAdditionalSemanticProcessing),
                    TextReport(
                        selectedExact:
                            true,
                        projectionExact:
                            true,
                        candidateHasVisual:
                            false),
                    VisualReport(
                        VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);

        Assert.False(
            report.ReadyForGuardedCutover);

        Assert.False(
            report.PortableOutputCompared);

        Assert.False(
            report.ProvenanceCompared);

        Assert.Equal(
            1,
            report.ExactSelectedTextPageCount);

        Assert.Equal(
            1,
            report.ExactTextProjectionPageCount);

        Assert.Equal(
            1,
            report.ExactVisualPlanExecutionPageCount);

        Assert.Equal(
            [
                DocumentControlledCandidateCutoverBlocker.PortableOutputNotCompared,
                DocumentControlledCandidateCutoverBlocker.ProvenanceNotCompared
            ],
            report.CutoverBlockers);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_TextDivergence_AddsExplicitCutoverBlockers()
    {
        var report =
            await RunAsync(
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing),
                TextReport(
                    selectedExact:
                        false,
                    projectionExact:
                        false,
                    candidateHasVisual:
                        false),
                VisualReport(
                    VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker
                .SelectedTextSequenceDivergence,
            report.CutoverBlockers);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker
                .TextProjectionDivergence,
            report.CutoverBlockers);

        Assert.False(
            report.ReadyForGuardedCutover);
    }

    [Fact]
    public async Task Runner_Preservation_TracksLegacyNativeVisualDeltaAndPersistenceGap()
    {
        var report =
            await RunAsync(
                Shadow(
                    VisualExecutionAction.PreserveMeaningfulVisual),
                TextReport(
                    selectedExact:
                        true,
                    projectionExact:
                        true,
                    candidateHasVisual:
                        true),
                VisualReport(
                    VisualExecutionAction.PreserveMeaningfulVisual));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.CandidateAddsIndependentVisualWorkToLegacyNativePageCount);

        var page =
            Assert.Single(
                report.Pages);

        Assert.True(
            page.CandidateHasMeaningfulVisualPreservation);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker
                .CandidateVisualPersistenceNotCompared,
            report.CutoverBlockers);
    }

    [Fact]
    public async Task Runner_DeferredText_AddsIncompleteExecutionBlocker()
    {
        var report =
            await RunAsync(
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing,
                    textMode:
                        TextExecutionMode.TargetedOcrVerification),
                DeferredTextReport(),
                VisualReport(
                    VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker
                .TextExecutionIncomplete,
            report.CutoverBlockers);
    }

    [Fact]
    public async Task Runner_FailedTextExecution_IsCandidateExecutionUnavailable()
    {
        var text =
            new DocumentControlledCandidateTextExecutionReport(
                SourceSha,
                DocumentControlledCandidateTextExecutionStatus.Failed,
                pages:
                    [],
                new DocumentControlledCandidateTextExecutionFailure(
                    "Synthetic.TextFailure",
                    "synthetic text failure",
                    1));

        var report =
            await RunAsync(
                Shadow(
                    VisualExecutionAction.NoAdditionalSemanticProcessing),
                text,
                VisualReport(
                    VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus
                .CandidateExecutionUnavailable,
            report.Status);

        Assert.Empty(
            report.Pages);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker
                .TextExecutionUnavailable,
            report.CutoverBlockers);
    }

    [Fact]
    public async Task Runner_MismatchedVisualAction_IsIsolatedComparisonFailure()
    {
        var observer =
            new RecordingComparisonObserver();

        var report =
            await new DocumentControlledCandidateComparisonRunner(
                    new DocumentControlledCandidateComparisonDependencies(
                        observer))
                .RunAsync(
                    AuthoritativeResult(),
                    Shadow(
                        VisualExecutionAction.PreserveMeaningfulVisual),
                    TextReport(
                        selectedExact:
                            true,
                        projectionExact:
                            true,
                        candidateHasVisual:
                            true),
                    VisualReport(
                        VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Failed,
            report.Status);

        Assert.Empty(
            report.Pages);

        var failure =
            Assert.IsType<DocumentControlledCandidateComparisonFailure>(
                report.Failure);

        Assert.Contains(
            "executed action does not match H.4C",
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
        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                new DocumentControlledCandidateComparisonRunner(
                        new DocumentControlledCandidateComparisonDependencies(
                            new RecordingComparisonObserver()))
                    .RunAsync(
                        AuthoritativeResult(),
                        Shadow(
                            VisualExecutionAction.NoAdditionalSemanticProcessing),
                        TextReport(
                            selectedExact:
                                true,
                            projectionExact:
                                true,
                            candidateHasVisual:
                                false),
                        VisualReport(
                            VisualExecutionAction.NoAdditionalSemanticProcessing),
                        cancellation.Token)
                    .AsTask());
    }

    [Fact]
    public async Task Runner_ObserverOrdinaryFailure_IsBestEffort()
    {
        var report =
            await new DocumentControlledCandidateComparisonRunner(
                    new DocumentControlledCandidateComparisonDependencies(
                        new ThrowingComparisonObserver(
                            new InvalidOperationException(
                                "synthetic observer failure"))))
                .RunAsync(
                    AuthoritativeResult(),
                    Shadow(
                        VisualExecutionAction.NoAdditionalSemanticProcessing),
                    TextReport(
                        selectedExact:
                            true,
                        projectionExact:
                            true,
                        candidateHasVisual:
                            false),
                    VisualReport(
                        VisualExecutionAction.NoAdditionalSemanticProcessing));

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);
    }

    [Fact]
    public async Task ProcessAsync_ComparisonRunsAfterBothAxesAndAuthorityRemainsLegacy()
    {
        var extraction =
            Extraction(
                rasterImageCount:
                    1);

        var baseline =
            await ProcessAsync(
                extraction,
                comparisonObserver:
                    null);

        var comparisonObserver =
            new RecordingComparisonObserver();

        var actual =
            await ProcessAsync(
                extraction,
                comparisonObserver);

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                comparisonObserver.Reports);

        Assert.Equal(
            DocumentControlledCandidateComparisonStatus.Completed,
            report.Status);

        Assert.Equal(
            1,
            report.CandidateAddsIndependentVisualWorkToLegacyNativePageCount);

        Assert.False(
            report.ReadyForGuardedCutover);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker.PortableOutputNotCompared,
            report.CutoverBlockers);

        Assert.Contains(
            DocumentControlledCandidateCutoverBlocker.ProvenanceNotCompared,
            report.CutoverBlockers);
    }

    private static async ValueTask<DocumentControlledCandidateComparisonReport>
        RunAsync(
            DocumentShadowPlanningReport shadow,
            DocumentControlledCandidateTextExecutionReport text,
            DocumentControlledCandidateVisualExecutionReport visual) =>
        await new DocumentControlledCandidateComparisonRunner(
                new DocumentControlledCandidateComparisonDependencies(
                    new RecordingComparisonObserver()))
            .RunAsync(
                AuthoritativeResult(),
                shadow,
                text,
                visual);

    private static DocumentIngestionResult AuthoritativeResult()
    {
        var source =
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    100,
                physicalPageCount:
                    1,
                "comparison.pdf",
                "application/pdf");

        var manifest =
            new DocumentProcessingManifest(
                "test-engine-h4d4a-v1",
                NativeIdentity,
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    [],
                "assembly-v1",
                "normalization-v1",
                "segmentation-v1");

        return new DocumentIngestionResult(
            source,
            manifest,
            [
                new DocumentIngestionPage(
                    1,
                    new NormalizedRectangle(
                        0,
                        0,
                        1,
                        1),
                    orderedElementIds:
                        [])
            ],
            elements:
                [],
            structuralSegments:
                [],
            DocumentIngestionQualityObservations.Empty);
    }

    private static DocumentControlledCandidateTextExecutionReport TextReport(
        bool selectedExact,
        bool projectionExact,
        bool candidateHasVisual) =>
        new(
            SourceSha,
            DocumentControlledCandidateTextExecutionStatus.Completed,
            [
                new DocumentControlledCandidateTextPageComparison(
                    1,
                    PageProcessingRoute.NativeOnly,
                    TextExecutionMode.NativeText,
                    DocumentControlledCandidateTextPageStatus.ExecutedNativeText,
                    candidateRemovesLegacyTextMl:
                        false,
                    candidateHasIndependentVisualWork:
                        candidateHasVisual,
                    selectedExact,
                    projectionExact,
                    authoritativeTextElementCount:
                        1,
                    candidateTextElementCount:
                        1,
                    authoritativeReconciliationEvidenceCount:
                        0,
                    candidateReconciliationEvidenceCount:
                        0)
            ]);

    private static DocumentControlledCandidateTextExecutionReport DeferredTextReport() =>
        new(
            SourceSha,
            DocumentControlledCandidateTextExecutionStatus.Completed,
            [
                new DocumentControlledCandidateTextPageComparison(
                    1,
                    PageProcessingRoute.NativeOnly,
                    TextExecutionMode.TargetedOcrVerification,
                    DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode,
                    candidateRemovesLegacyTextMl:
                        false,
                    candidateHasIndependentVisualWork:
                        false)
            ]);

    private static DocumentControlledCandidateVisualExecutionReport VisualReport(
        VisualExecutionAction action)
    {
        DocumentControlledCandidateVisualElementExecution element =
            action switch
            {
                VisualExecutionAction.PreserveMeaningfulVisual =>
                    new DocumentControlledCandidateVisualElementExecution(
                        0,
                        action,
                        new SourceVisualAssetMaterialization(
                            1,
                            0,
                            new NormalizedRectangle(
                                0.1,
                                0.2,
                                0.7,
                                0.8),
                            "fake-source-visual-v1",
                            "image/jpeg",
                            123,
                            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")),

                _ =>
                    new DocumentControlledCandidateVisualElementExecution(
                        0,
                        action)
            };

        return new DocumentControlledCandidateVisualExecutionReport(
            SourceSha,
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            [
                new DocumentControlledCandidateVisualPageExecution(
                    1,
                    PageProcessingRoute.NativeOnly,
                    [
                        element
                    ])
            ]);
    }

    private static DocumentShadowPlanningReport Shadow(
        VisualExecutionAction action,
        TextExecutionMode textMode = TextExecutionMode.NativeText)
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
                [
                    new VisualElementEvidence(
                        0,
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
                        })
                ]);

        var requirements =
            new PageProcessingRequirements(
                1,
                textMode ==
                    TextExecutionMode.NativeText
                    ? TextProcessingRequirement.UseNativeText
                    : TextProcessingRequirement.VerifyNativeText,
                [
                    new VisualElementDisposition(
                        0,
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
                        })
                ]);

        var plan =
            new PageExecutionPlan(
                1,
                textMode,
                [
                    new VisualElementExecutionPlan(
                        0,
                        action)
                ]);

        var candidate =
            new PageExecutionPlanningDecision(
                assessment,
                evidence,
                requirements,
                plan);

        return new DocumentShadowPlanningReport(
            SourceSha,
            DocumentFormatId.Pdf,
            DocumentShadowPlanningStatus.Completed,
            [
                new DocumentShadowPageComparison(
                    legacy,
                    new GuardedPagePlanningDecision(
                        legacy,
                        candidate))
            ]);
    }

    private static DocumentExtractionResult Extraction(
        int rasterImageCount) =>
        new(
            DocumentFormatId.Pdf,
            [
                NativePage(
                    rasterImageCount)
            ]);

    private static DocumentExtractionPage NativePage(
        int rasterImageCount)
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
            1,
            text,
            wordCount:
                words.Length,
            rasterImageCount:
                rasterImageCount,
            largestRasterImageAreaRatio:
                0.10,
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

    private static async Task<DocumentIngestionResult> ProcessAsync(
        DocumentExtractionResult extraction,
        IDocumentControlledCandidateComparisonObserver? comparisonObserver)
    {
        var shadow =
            comparisonObserver is null
                ? null
                : new DocumentShadowPlanningDependencies(
                    new UnknownVisualRasterObservationSource(),
                    new NoOpShadowObserver());

        var textObserver =
            new RecordingTextObserver();

        var visualObserver =
            new RecordingVisualObserver();

        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                "test-engine-h4d4a-v1",
                NativeIdentity,
                shadowPlanning:
                    shadow,
                controlledCandidateTextExecution:
                    comparisonObserver is null
                        ? null
                        : new DocumentControlledCandidateTextExecutionDependencies(
                            textObserver),
                controlledCandidateVisualExecution:
                    comparisonObserver is null
                        ? null
                        : new DocumentControlledCandidateVisualExecutionDependencies(
                            visualObserver,
                            new ForbiddenSourceMaterializer(),
                            new CountingDocumentRasterizer(),
                            new CountingLayoutAnalyzer()),
                controlledCandidateComparison:
                    comparisonObserver is null
                        ? null
                        : new DocumentControlledCandidateComparisonDependencies(
                            comparisonObserver));

        await using var stream =
            new MemoryStream(
                "%PDF-h4d4a-authority"u8.ToArray(),
                writable:
                    false);

        return await processor
            .ProcessAsync(
                new DocumentSource(
                    stream,
                    "h4d4a.pdf",
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

    private sealed class RecordingComparisonObserver
        : IDocumentControlledCandidateComparisonObserver
    {
        public List<DocumentControlledCandidateComparisonReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentControlledCandidateComparisonReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingComparisonObserver(
        Exception exception)
        : IDocumentControlledCandidateComparisonObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateComparisonReport report,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class RecordingTextObserver
        : IDocumentControlledCandidateTextExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingVisualObserver
        : IDocumentControlledCandidateVisualExecutionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidateVisualExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ForbiddenSourceMaterializer
        : ISourceVisualAssetMaterializer
    {
        public bool CanMaterialize(
            DocumentFormatId format) =>
            throw new InvalidOperationException(
                "AnalyzeVisual must not query source preservation.");

        public ValueTask<SourceVisualAssetMaterialization> MaterializeAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            int physicalPageNumber,
            int sourceVisualIndex,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "AnalyzeVisual must not preserve a source visual.");
    }

    private sealed class CountingDocumentRasterizer
        : IDocumentRasterizer
    {
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

            return ValueTask.FromResult<IDocumentRasterizationSession>(
                new CountingRasterizationSession());
        }
    }

    private sealed class CountingRasterizationSession
        : IDocumentRasterizationSession
    {
        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            "fake-raster-v1";

        public int Dpi =>
            300;

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"));
        }

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "H.4D.4A integration test must not render OCR-style regions.");

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class CountingLayoutAnalyzer
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
                    []));
        }
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
