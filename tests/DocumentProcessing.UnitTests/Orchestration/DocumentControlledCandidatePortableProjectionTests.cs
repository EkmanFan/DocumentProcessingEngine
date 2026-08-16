using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentControlledCandidatePortableProjectionTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    [Fact]
    public async Task TextRunner_ExecutedNativeText_RetainsCandidateHybridPage()
    {
        var extraction =
            Extraction();

        var authoritativePage =
            NativeCandidatePage();

        var report =
            await new DocumentControlledCandidateTextExecutionRunner(
                    new DocumentControlledCandidateTextExecutionDependencies(
                        new NoOpTextObserver()))
                .RunAsync(
                    extraction,
                    [
                        authoritativePage
                    ],
                    NativeShadow(),
                    SourceSha);

        var page =
            Assert.Single(
                report.Pages);

        Assert.NotNull(
            page.CandidatePage);

        Assert.Equal(
            authoritativePage.PhysicalPageNumber,
            page.CandidatePage!.PhysicalPageNumber);

        Assert.True(
            page.SelectedTextSequenceExact ==
            true);

        Assert.True(
            page.TextProjectionExact ==
            true);
    }

    [Fact]
    public async Task Runner_NativeCandidate_BuildsCanonicalPortableDocument()
    {
        var report =
            await RunAsync(
                CandidateTextReport(
                    NativeCandidatePage()),
                EmptyVisualReport());

        Assert.Equal(
            DocumentControlledCandidatePortableProjectionStatus.Completed,
            report.Status);

        var output =
            Assert.IsType<DocumentControlledCandidatePortableOutput>(
                report.Output);

        Assert.Equal(
            SourceSha,
            output.CandidateDocument.Source.Sha256);

        Assert.Single(
            output.CandidateDocument.Pages);

        Assert.Single(
            output.CandidateDocument.Elements);

        Assert.Single(
            output.CandidateDocument.StructuralSegments);

        Assert.Empty(
            output.SourceVisuals);

        Assert.Empty(
            output.VisualAnalyses);

        Assert.True(
            report.CandidateDocumentBuilt);

        Assert.True(
            report.CandidateProvenanceBuilt);

        Assert.True(
            report.ReadyForFinalCutoverComparison);
    }

    [Fact]
    public async Task Runner_PreservedSourceVisual_RemainsNeutralSidecar()
    {
        var materialization =
            new SourceVisualAssetMaterialization(
                1,
                0,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.70,
                    0.80),
                "fake-source-visual-v1",
                "image/jpeg",
                123,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var report =
            await RunAsync(
                CandidateTextReport(
                    NativeCandidatePage()),
                VisualReport(
                    new DocumentControlledCandidateVisualElementExecution(
                        0,
                        VisualExecutionAction.PreserveMeaningfulVisual,
                        materialization)));

        var output =
            Assert.IsType<DocumentControlledCandidatePortableOutput>(
                report.Output);

        var sidecar =
            Assert.Single(
                output.SourceVisuals);

        Assert.Equal(
            SourceSha,
            sidecar.SourceDocumentSha256);

        Assert.Same(
            materialization,
            sidecar.Materialization);

        Assert.Empty(
            output.VisualAnalyses);

        Assert.DoesNotContain(
            output.CandidateDocument.Elements,
            element =>
                element.Kind ==
                HybridDocumentElementKind.Visual);

        Assert.True(
            output.HasUnpersistedSourceVisualAssets);

        Assert.False(
            report.ReadyForFinalCutoverComparison);
    }

    [Fact]
    public async Task Runner_AnalyzeVisual_RemainsUnresolvedNeutralSidecar()
    {
        var layout =
            new LayoutAnalysisResult(
                "fake-layout",
                1,
                [
                    new LayoutObservation(
                        1,
                        0,
                        readingOrder:
                            0,
                        LayoutObservationKind.Figure,
                        new NormalizedRectangle(
                            0.20,
                            0.20,
                            0.80,
                            0.80),
                        "image")
                ]);

        var raster =
            new RasterRenderResult(
                1,
                1000,
                1200,
                crop:
                    null,
                1000,
                1200,
                "image/png",
                "fake-raster-v1",
                4,
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");

        var report =
            await RunAsync(
                CandidateTextReport(
                    NativeCandidatePage()),
                VisualReport(
                    new DocumentControlledCandidateVisualElementExecution(
                        0,
                        VisualExecutionAction.AnalyzeVisual),
                    raster,
                    layout));

        var output =
            Assert.IsType<DocumentControlledCandidatePortableOutput>(
                report.Output);

        var analysis =
            Assert.Single(
                output.VisualAnalyses);

        Assert.False(
            analysis.IsResolved);

        Assert.Equal(
            1,
            analysis.LayoutObservationCount);

        Assert.Equal(
            1,
            analysis.FigureObservationCount);

        Assert.Empty(
            output.SourceVisuals);

        Assert.DoesNotContain(
            output.CandidateDocument.Elements,
            element =>
                element.Kind ==
                HybridDocumentElementKind.Visual);

        Assert.True(
            output.HasUnresolvedVisualAnalysis);

        Assert.False(
            report.ReadyForFinalCutoverComparison);
    }

    [Fact]
    public async Task Runner_MissingRetainedCandidatePage_IsInputUnavailable()
    {
        var report =
            await RunAsync(
                CandidateTextReport(
                    candidatePage:
                        null),
                EmptyVisualReport());

        Assert.Equal(
            DocumentControlledCandidatePortableProjectionStatus.InputUnavailable,
            report.Status);

        Assert.Null(
            report.Output);

        Assert.Null(
            report.Failure);
    }

    [Fact]
    public async Task Runner_LayoutBackedCandidateWithoutExplicitIdentities_FailsClosed()
    {
        var report =
            await RunAsync(
                CandidateTextReport(
                    LayoutBackedCandidatePage()),
                EmptyVisualReport());

        Assert.Equal(
            DocumentControlledCandidatePortableProjectionStatus.Failed,
            report.Status);

        Assert.Contains(
            "explicit candidate rasterization identity",
            Assert.IsType<DocumentControlledCandidatePortableProjectionFailure>(
                    report.Failure)
                .Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_ExplicitLayoutIdentities_EnableHonestLayoutProvenance()
    {
        var report =
            await RunAsync(
                CandidateTextReport(
                    LayoutBackedCandidatePage()),
                EmptyVisualReport(),
                new DocumentControlledCandidatePortableProjectionDependencies(
                    new RecordingProjectionObserver(),
                    rasterizationIdentity:
                        new ProcessingComponentIdentity(
                            "fake-raster",
                            "fake-raster-v1"),
                    layoutAnalysisIdentity:
                        new ProcessingComponentIdentity(
                            "fake-layout",
                            "fake-layout-v1")));

        Assert.Equal(
            DocumentControlledCandidatePortableProjectionStatus.Completed,
            report.Status);

        var candidate =
            Assert.IsType<DocumentControlledCandidatePortableOutput>(
                    report.Output)
                .CandidateDocument;

        var element =
            Assert.Single(
                candidate.Elements);

        Assert.Equal<int?>(
            0,
            element.LayoutObservationSequence);

        Assert.Equal<LayoutObservationKind?>(
            LayoutObservationKind.Text,
            element.LayoutKind);
    }

    [Fact]
    public async Task Runner_ObserverOrdinaryFailure_IsBestEffort()
    {
        var report =
            await RunAsync(
                CandidateTextReport(
                    NativeCandidatePage()),
                EmptyVisualReport(),
                new DocumentControlledCandidatePortableProjectionDependencies(
                    new ThrowingProjectionObserver(
                        new InvalidOperationException(
                            "synthetic observer failure"))));

        Assert.Equal(
            DocumentControlledCandidatePortableProjectionStatus.Completed,
            report.Status);
    }

    [Fact]
    public async Task Runner_CallerCancellation_Propagates()
    {
        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                new DocumentControlledCandidatePortableProjectionRunner(
                        new DocumentControlledCandidatePortableProjectionDependencies(
                            new RecordingProjectionObserver()))
                    .RunAsync(
                        AuthoritativeResult(),
                        CandidateTextReport(
                            NativeCandidatePage()),
                        EmptyVisualReport(),
                        "test-engine-h4d4b1-v1",
                        NativeIdentity,
                        cancellation.Token)
                    .AsTask());
    }

    private static async ValueTask<DocumentControlledCandidatePortableProjectionReport>
        RunAsync(
            DocumentControlledCandidateTextExecutionReport text,
            DocumentControlledCandidateVisualExecutionReport visual,
            DocumentControlledCandidatePortableProjectionDependencies?
                dependencies = null) =>
        await new DocumentControlledCandidatePortableProjectionRunner(
                dependencies ??
                new DocumentControlledCandidatePortableProjectionDependencies(
                    new RecordingProjectionObserver()))
            .RunAsync(
                AuthoritativeResult(),
                text,
                visual,
                "test-engine-h4d4b1-v1",
                NativeIdentity);

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
                "candidate.pdf",
                "application/pdf");

        var manifest =
            new DocumentProcessingManifest(
                "test-engine-authoritative-v1",
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

    private static DocumentControlledCandidateTextExecutionReport
        CandidateTextReport(
            HybridDocumentPage? candidatePage) =>
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
                        false,
                    selectedTextSequenceExact:
                        true,
                    textProjectionExact:
                        true,
                    authoritativeTextElementCount:
                        1,
                    candidateTextElementCount:
                        1,
                    authoritativeReconciliationEvidenceCount:
                        0,
                    candidateReconciliationEvidenceCount:
                        0,
                    candidatePage:
                        candidatePage)
            ]);

    private static DocumentControlledCandidateVisualExecutionReport
        EmptyVisualReport() =>
        new(
            SourceSha,
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            [
                new DocumentControlledCandidateVisualPageExecution(
                    1,
                    PageProcessingRoute.NativeOnly,
                    elements:
                        [])
            ]);

    private static DocumentControlledCandidateVisualExecutionReport
        VisualReport(
            DocumentControlledCandidateVisualElementExecution element,
            RasterRenderResult? raster = null,
            LayoutAnalysisResult? layout = null) =>
        new(
            SourceSha,
            DocumentControlledCandidateVisualExecutionStatus.Completed,
            [
                new DocumentControlledCandidateVisualPageExecution(
                    1,
                    PageProcessingRoute.NativeOnly,
                    [
                        element
                    ],
                    raster,
                    layout)
            ]);

    private static HybridDocumentPage NativeCandidatePage()
    {
        var sourcePage =
            Extraction()
                .Pages[0];

        var elements =
            sourcePage
                .Blocks
                .Select(
                    block =>
                        HybridDocumentElementFactory
                            .FromNative(
                                sourcePage.PhysicalPageNumber,
                                block))
                .ToArray();

        return HybridDocumentAssembler
            .AssemblePage(
                sourcePage,
                elements);
    }

    private static HybridDocumentPage LayoutBackedCandidatePage()
    {
        var native =
            NativeCandidatePage();

        var source =
            Assert.Single(
                native.Elements);

        var layout =
            new LayoutObservation(
                1,
                0,
                readingOrder:
                    0,
                LayoutObservationKind.Text,
                source.Bounds,
                "text");

        return new HybridDocumentPage(
            1,
            native.ContentViewport,
            [
                new HybridDocumentElement(
                    1,
                    0,
                    source.Kind,
                    source.Bounds,
                    source.Text,
                    source.TextOrigin,
                    source.NativeBlock,
                    layout)
            ]);
    }

    private static DocumentExtractionResult Extraction() =>
        new(
            DocumentFormatId.Pdf,
            [
                NativePage()
            ]);

    private static DocumentExtractionPage NativePage()
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
                0,
            largestRasterImageAreaRatio:
                0,
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

    private static DocumentShadowPlanningReport NativeShadow()
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
                visualElements:
                    []);

        var requirements =
            new PageProcessingRequirements(
                1,
                TextProcessingRequirement.UseNativeText,
                visualElements:
                    []);

        var plan =
            new PageExecutionPlan(
                1,
                TextExecutionMode.NativeText,
                visualElements:
                    []);

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

    private sealed class RecordingProjectionObserver
        : IDocumentControlledCandidatePortableProjectionObserver
    {
        public List<DocumentControlledCandidatePortableProjectionReport> Reports { get; } =
            [];

        public ValueTask ObserveAsync(
            DocumentControlledCandidatePortableProjectionReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Reports.Add(
                report);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingProjectionObserver(
        Exception exception)
        : IDocumentControlledCandidatePortableProjectionObserver
    {
        public ValueTask ObserveAsync(
            DocumentControlledCandidatePortableProjectionReport report,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class NoOpTextObserver
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
}
