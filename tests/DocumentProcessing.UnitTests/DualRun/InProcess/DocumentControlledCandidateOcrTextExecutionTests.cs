using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentControlledCandidateOcrTextExecutionTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    [Theory]
    [InlineData(
        NativeTextStatus.Missing,
        TextExecutionMode.TargetedOcrRecovery,
        DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrRecovery)]
    [InlineData(
        NativeTextStatus.Unverified,
        TextExecutionMode.TargetedOcrVerification,
        DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrVerification)]
    [InlineData(
        NativeTextStatus.Suspicious,
        TextExecutionMode.TargetedOcrReconciliation,
        DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrReconciliation)]
    public async Task Runner_ExplicitOcrComposition_ExecutesEachCandidateModeExactly(
        NativeTextStatus nativeStatus,
        TextExecutionMode textMode,
        DocumentControlledCandidateTextPageStatus expectedStatus)
    {
        var page =
            nativeStatus ==
            NativeTextStatus.Missing
                ? MissingPage(
                    1)
                : NativePage(
                    1,
                    "Candidate and legacy text agree.");

        var layout =
            LayoutFor(
                page);

        var layoutAnalyzer =
            new FakePageLayoutAnalyzer(
                [
                    layout
                ]);

        var recognizer =
            new FakeRegionTextRecognizer(
                nativeStatus ==
                NativeTextStatus.Missing
                    ? "Recovered by OCR."
                    : page.Blocks[0].Text);

        var authoritative =
            await ExecuteLegacyAsync(
                page,
                nativeStatus,
                layoutAnalyzer,
                recognizer);

        var observer =
            new RecordingCandidateObserver();

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    observer,
                    new FakeDocumentRasterizer(),
                    layoutAnalyzer,
                    recognizer));

        var shadow =
            Shadow(
                nativeStatus,
                textMode);

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-ocr"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
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
            report.ExecutedOcrBackedTextPageCount);

        Assert.Equal(
            0,
            report.DeferredNonNativeTextPageCount);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.Equal(
            expectedStatus,
            comparison.Status);

        Assert.Equal(
            textMode,
            comparison.CandidateTextMode);

        Assert.True(
            comparison.SelectedTextSequenceExact is true);

        Assert.True(
            comparison.TextProjectionExact is true);

        Assert.Equal(
            1,
            comparison.CandidateReconciliationEvidenceCount);

        Assert.False(
            comparison.CandidateRemovesLegacyTextMl);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_OrdinaryCandidateRasterFailure_IsReportedAndDoesNotThrow()
    {
        var page =
            MissingPage(
                1);

        var observer =
            new RecordingCandidateObserver();

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    observer,
                    new ThrowingDocumentRasterizer(
                        new InvalidOperationException(
                            "synthetic candidate raster failure")),
                    new FakePageLayoutAnalyzer(
                        [
                            LayoutFor(
                                page)
                        ]),
                    new FakeRegionTextRecognizer(
                        "unused")));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-failure"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
                [
                    new HybridDocumentPage(
                        1)
                ],
                Shadow(
                    NativeTextStatus.Missing,
                    TextExecutionMode.TargetedOcrRecovery),
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
            "synthetic candidate raster failure",
            failure.Message,
            StringComparison.Ordinal);

        Assert.Same(
            report,
            Assert.Single(
                observer.Reports));
    }

    [Fact]
    public async Task Runner_CandidateRasterOutOfMemory_Propagates()
    {
        var page =
            MissingPage(
                1);

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver(),
                    new ThrowingDocumentRasterizer(
                        new OutOfMemoryException(
                            "synthetic candidate raster OOM")),
                    new FakePageLayoutAnalyzer(
                        [
                            LayoutFor(
                                page)
                        ]),
                    new FakeRegionTextRecognizer(
                        "unused")));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-oom"u8.ToArray(),
                writable:
                    false);

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () =>
                runner.RunAsync(
                        new DocumentSource(
                            sourceBytes,
                            "controlled.pdf",
                            "application/pdf"),
                        DocumentFormatId.Pdf,
                        new DocumentExtractionResult(
                            DocumentFormatId.Pdf,
                            [
                                page
                            ]),
                        [
                            new HybridDocumentPage(
                                1)
                        ],
                        Shadow(
                            NativeTextStatus.Missing,
                            TextExecutionMode.TargetedOcrRecovery),
                        SourceSha)
                    .AsTask());
    }

    [Fact]
    public async Task ProcessAsync_OrdinaryControlledOcrFailure_RemainsFailOpenAfterAuthority()
    {
        var page =
            MissingPage(
                1);

        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    page
                ]);

        var baseline =
            await ProcessAsync(
                extraction,
                controlled:
                    null);

        var observer =
            new RecordingCandidateObserver();

        var actual =
            await ProcessAsync(
                extraction,
                new DocumentControlledCandidateTextExecutionDependencies(
                    observer,
                    new ThrowingDocumentRasterizer(
                        new InvalidOperationException(
                            "synthetic post-authority candidate failure")),
                    new FakePageLayoutAnalyzer(
                        [
                            LayoutFor(
                                page)
                        ]),
                    new FakeRegionTextRecognizer(
                        "unused")));

        AssertSerializedEquivalent(
            baseline,
            actual);

        var report =
            Assert.Single(
                observer.Reports);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Failed,
            report.Status);

        var failure =
            Assert.IsType<DocumentControlledCandidateTextExecutionFailure>(
                report.Failure);

        Assert.Contains(
            "synthetic post-authority candidate failure",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_OcrComposition_IgnoresIndependentVisualLayoutRegions()
    {
        var page =
            MissingPage(
                1);

        var textObservation =
            LayoutFor(
                page);

        var figureObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.10,
                    0.40,
                    0.90,
                    0.90),
                "Figure");

        var authoritative =
            await ExecuteLegacyAsync(
                page,
                NativeTextStatus.Missing,
                new FakePageLayoutAnalyzer(
                    [
                        textObservation
                    ]),
                new FakeRegionTextRecognizer(
                    "Recovered by OCR."));

        var candidateRecognizer =
            new FakeRegionTextRecognizer(
                "Recovered by OCR.");

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver(),
                    new FakeDocumentRasterizer(),
                    new FakePageLayoutAnalyzer(
                        [
                            textObservation,
                            figureObservation
                        ]),
                    candidateRecognizer));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-visual-isolation"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
                [
                    authoritative
                ],
                Shadow(
                    NativeTextStatus.Missing,
                    TextExecutionMode.TargetedOcrRecovery,
                    candidateHasIndependentVisualWork:
                        true),
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.True(
            comparison.TextProjectionExact is true);

        Assert.True(
            comparison.CandidateHasIndependentVisualWork);

        var visualEvidence =
            Assert.Single(
                comparison.CandidateLayoutVisualEvidence);

        Assert.Equal(
            figureObservation,
            visualEvidence.Observation);

        Assert.Equal(
            VisualEvidenceKind.LargeIndependentVisual,
            visualEvidence.Kind);

        var preserved =
            Assert.Single(
                comparison.CandidatePreservedLayoutVisuals);

        Assert.Equal(
            figureObservation,
            preserved.SourceLayoutObservation);

        Assert.True(
            preserved.ContentLength >
            0);

        Assert.DoesNotContain(
            LayoutObservationKind.Figure,
            candidateRecognizer.ObservedKinds);
    }

    [Fact]
    public async Task Runner_NativePresentCandidate_RetainsDeferredAndSmallNeutralFigureEvidenceClassification()
    {
        var page =
            NativePage(
                1,
                "Candidate and legacy text agree.");

        var textObservation =
            LayoutFor(
                page);

        var deferredObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Unknown,
                new NormalizedRectangle(
                    0.10,
                    0.35,
                    0.90,
                    0.45),
                "Unknown");

        var figureObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    2,
                readingOrder:
                    2,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.10,
                    0.50,
                    0.90,
                    0.70),
                "Figure");

        var authoritative =
            await ExecuteLegacyAsync(
                page,
                NativeTextStatus.Suspicious,
                new FakePageLayoutAnalyzer(
                    [
                        textObservation
                    ]),
                new FakeRegionTextRecognizer(
                    page.Blocks[0].Text));

        var candidateRecognizer =
            new FakeRegionTextRecognizer(
                page.Blocks[0].Text);

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver(),
                    new FakeDocumentRasterizer(),
                    new FakePageLayoutAnalyzer(
                        [
                            textObservation,
                            deferredObservation,
                            figureObservation
                        ]),
                    candidateRecognizer));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-deferred-evidence"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
                [
                    authoritative
                ],
                Shadow(
                    NativeTextStatus.Suspicious,
                    TextExecutionMode.TargetedOcrReconciliation,
                    candidateHasIndependentVisualWork:
                        true),
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        var comparison =
            Assert.Single(
                report.Pages);

        Assert.True(
            comparison.SelectedTextSequenceExact is true);

        Assert.True(
            comparison.TextProjectionExact is true);

        var candidatePage =
            Assert.IsType<HybridDocumentPage>(
                comparison.CandidatePage);

        Assert.Equal(
            2,
            candidatePage.Elements.Count);

        var deferred =
            Assert.Single(
                candidatePage.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Deferred);

        Assert.NotNull(
            deferred.LayoutObservation);

        Assert.Equal(
            LayoutObservationKind.Unknown,
            deferred.LayoutObservation!.Kind);

        Assert.Equal(
            1,
            deferred.LayoutObservation.ObservationSequence);

        Assert.DoesNotContain(
            candidatePage.Elements,
            element =>
                element.LayoutObservation?.Kind ==
                LayoutObservationKind.Figure);

        var visualEvidence =
            Assert.Single(
                comparison.CandidateLayoutVisualEvidence);

        Assert.Equal(
            figureObservation,
            visualEvidence.Observation);

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            visualEvidence.Kind);

        Assert.Empty(
            comparison.CandidatePreservedLayoutVisuals);

        Assert.DoesNotContain(
            LayoutObservationKind.Figure,
            candidateRecognizer.ObservedKinds);
    }

    [Fact]
    public async Task Runner_MissingNativeFutureLayoutKind_RetainsDeferredEvidenceAndNeverOcrsIt()
    {
        var page =
            MissingPage(
                1);

        var textObservation =
            LayoutFor(
                page);

        var futureKind =
            (LayoutObservationKind)int.MaxValue;

        var futureObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                futureKind,
                new NormalizedRectangle(
                    0.10,
                    0.35,
                    0.90,
                    0.45),
                "future_backend_kind");

        var authoritative =
            await ExecuteLegacyAsync(
                page,
                NativeTextStatus.Missing,
                new FakePageLayoutAnalyzer(
                    [
                        textObservation
                    ]),
                new FakeRegionTextRecognizer(
                    "Recovered by OCR."));

        var candidateRecognizer =
            new FakeRegionTextRecognizer(
                "Recovered by OCR.");

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver(),
                    new FakeDocumentRasterizer(),
                    new FakePageLayoutAnalyzer(
                        [
                            textObservation,
                            futureObservation
                        ]),
                    candidateRecognizer));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-future-layout-kind"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
                [
                    authoritative
                ],
                Shadow(
                    NativeTextStatus.Missing,
                    TextExecutionMode.TargetedOcrRecovery),
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        var comparison =
            Assert.Single(
                report.Pages);

        var candidatePage =
            Assert.IsType<HybridDocumentPage>(
                comparison.CandidatePage);

        var deferred =
            Assert.Single(
                candidatePage.Elements,
                element =>
                    element.Kind ==
                    HybridDocumentElementKind.Deferred);

        Assert.NotNull(
            deferred.LayoutObservation);

        Assert.Equal(
            futureKind,
            deferred.LayoutObservation!.Kind);

        Assert.Equal(
            futureObservation,
            deferred.LayoutObservation);

        Assert.DoesNotContain(
            futureKind,
            candidateRecognizer.ObservedKinds);

        Assert.Empty(
            comparison.CandidateLayoutVisualEvidence);

        Assert.Empty(
            comparison.CandidatePreservedLayoutVisuals);
    }

    [Fact]
    public async Task Runner_MissingNativeCaptionedFigure_RetainsPreservedLayoutVisualAndNeverOcrsFigure()
    {
        var page =
            MissingPage(
                1);

        var textObservation =
            LayoutFor(
                page);

        var figureObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.20,
                    0.35,
                    0.80,
                    0.70),
                "image");

        var captionObservation =
            new LayoutObservation(
                1,
                observationSequence:
                    2,
                readingOrder:
                    2,
                LayoutObservationKind.Caption,
                new NormalizedRectangle(
                    0.20,
                    0.71,
                    0.80,
                    0.76),
                "figure_title");

        var authoritativeLayoutAnalyzer =
            new FakePageLayoutAnalyzer(
                [
                    textObservation
                ]);

        var candidateLayoutAnalyzer =
            new FakePageLayoutAnalyzer(
                [
                    textObservation,
                    figureObservation,
                    captionObservation
                ]);

        var authoritative =
            await ExecuteLegacyAsync(
                page,
                NativeTextStatus.Missing,
                authoritativeLayoutAnalyzer,
                new FakeRegionTextRecognizer(
                    "Recovered by OCR."));

        var candidateRecognizer =
            new FakeRegionTextRecognizer(
                "Recovered by OCR.");

        var runner =
            new DocumentControlledCandidateTextExecutionRunner(
                new DocumentControlledCandidateTextExecutionDependencies(
                    new RecordingCandidateObserver(),
                    new FakeDocumentRasterizer(),
                    candidateLayoutAnalyzer,
                    candidateRecognizer));

        await using var sourceBytes =
            new MemoryStream(
                "%PDF-controlled-captioned-layout-visual"u8.ToArray(),
                writable:
                    false);

        var report =
            await runner.RunAsync(
                new DocumentSource(
                    sourceBytes,
                    "controlled.pdf",
                    "application/pdf"),
                DocumentFormatId.Pdf,
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    [
                        page
                    ]),
                [
                    authoritative
                ],
                Shadow(
                    NativeTextStatus.Missing,
                    TextExecutionMode.TargetedOcrRecovery),
                SourceSha);

        Assert.Equal(
            DocumentControlledCandidateTextExecutionStatus.Completed,
            report.Status);

        var comparison =
            Assert.Single(
                report.Pages);

        var layoutEvidence =
            Assert.Single(
                comparison.CandidateLayoutVisualEvidence);

        Assert.Equal(
            figureObservation,
            layoutEvidence.Observation);

        Assert.Equal(
            VisualEvidenceKind.CaptionedMeaningfulVisual,
            layoutEvidence.Kind);

        var preserved =
            Assert.Single(
                comparison.CandidatePreservedLayoutVisuals);

        Assert.Equal(
            figureObservation,
            preserved.SourceLayoutObservation);

        Assert.True(
            preserved.ContentLength >
            0);

        Assert.Matches(
            "^[0-9a-f]{64}$",
            preserved.ContentSha256);

        Assert.DoesNotContain(
            LayoutObservationKind.Figure,
            candidateRecognizer.ObservedKinds);
    }

    private static async Task<DocumentProcessing.Core.Results.DocumentIngestionResult>
        ProcessAsync(
            DocumentExtractionResult extraction,
            DocumentControlledCandidateTextExecutionDependencies? controlled)
    {
        var page =
            Assert.Single(
                extraction.Pages);

        var layout =
            LayoutFor(
                page);

        var layoutAnalyzer =
            new FakePageLayoutAnalyzer(
                [
                    layout
                ]);

        var recognizer =
            new FakeRegionTextRecognizer(
                "Recovered by OCR.");

        var hybrid =
            new DocumentHybridExecutionDependencies(
                new FakeDocumentRasterizer(),
                new MissingNativeHybridPageExecutor(
                    layoutAnalyzer,
                    recognizer,
                    new VisualAssetPreserver()),
                new NativePresentHybridPageExecutor(
                    layoutAnalyzer,
                    recognizer,
                    new VisualAssetPreserver()),
                new ProcessingComponentIdentity(
                    "fake-layout",
                    "fake-layout-v1"),
                new ProcessingComponentIdentity(
                    "fake-reconciliation",
                    "fake-reconciliation-v1"));

        var shadow =
            controlled is null
                ? null
                : new DocumentShadowPlanningDependencies(
                    new FakeVisualRasterObservationSource(
                        [
                            new PageVisualRasterObservations(
                                1,
                                [])
                        ]),
                    new NoOpShadowObserver());

        var processor =
            new DocumentProcessor(
                new StubDetector(),
                new StubExtractor(
                    extraction),
                new StubPreflightAnalyzer(),
                DocumentPageProcessingPlanner.CreateDefault(),
                hybrid,
                "test-engine-h4d2b-v1",
                NativeIdentity,
                shadow,
                controlled);

        await using var stream =
            new MemoryStream(
                "%PDF-h4d2b-authority"u8.ToArray(),
                writable:
                    false);

        return await processor.ProcessAsync(
            new DocumentSource(
                stream,
                "h4d2b.pdf",
                "application/pdf"));
    }

    private static async Task<HybridDocumentPage> ExecuteLegacyAsync(
        DocumentExtractionPage page,
        NativeTextStatus nativeStatus,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer)
    {
        await using var raster =
            new FakeRasterizationSession();

        var decision =
            LegacyDecision(
                nativeStatus);

        if (nativeStatus ==
            NativeTextStatus.Missing)
        {
            return await new MissingNativeHybridPageExecutor(
                    layoutAnalyzer,
                    textRecognizer,
                    new VisualAssetPreserver())
                .ExecuteAsync(
                    page,
                    decision,
                    raster,
                    SourceSha);
        }

        return await new NativePresentHybridPageExecutor(
                layoutAnalyzer,
                textRecognizer,
                new VisualAssetPreserver())
            .ExecuteAsync(
                page,
                decision,
                raster,
                SourceSha);
    }

    private static PageProcessingDecision LegacyDecision(
        NativeTextStatus nativeStatus) =>
        new(
            new PageProcessingAssessment(
                1,
                nativeStatus),
            new PageProcessingPlan(
                nativeStatus ==
                    NativeTextStatus.Missing
                    ? PageProcessingRoute.LayoutWithTargetedOcrRecovery
                    : PageProcessingRoute.LayoutWithTargetedOcrReconciliation));

    private static DocumentShadowPlanningReport Shadow(
        NativeTextStatus nativeStatus,
        TextExecutionMode textMode,
        bool candidateHasIndependentVisualWork = false)
    {
        var legacy =
            LegacyDecision(
                nativeStatus);

        var textAuthority =
            nativeStatus switch
            {
                NativeTextStatus.Missing =>
                    TextAuthority.Missing,

                NativeTextStatus.Unverified =>
                    TextAuthority.NeedsVerification,

                NativeTextStatus.Suspicious =>
                    TextAuthority.Corrupted,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(nativeStatus))
            };

        var textRequirement =
            textMode switch
            {
                TextExecutionMode.TargetedOcrRecovery =>
                    TextProcessingRequirement.RecoverMissingNativeText,

                TextExecutionMode.TargetedOcrVerification =>
                    TextProcessingRequirement.VerifyNativeText,

                TextExecutionMode.TargetedOcrReconciliation =>
                    TextProcessingRequirement.ReconcileCorruptedNativeText,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(textMode))
            };

        var visualEvidence =
            candidateHasIndependentVisualWork
                ? new[]
                {
                    new VisualElementEvidence(
                        0,
                        VisualEvidenceKind.LargeIndependentVisual)
                }
                : [];

        var visualDisposition =
            candidateHasIndependentVisualWork
                ? new[]
                {
                    new VisualElementDisposition(
                        0,
                        VisualDisposition.PreserveMeaningfulVisual)
                }
                : [];

        var visualExecution =
            candidateHasIndependentVisualWork
                ? new[]
                {
                    new VisualElementExecutionPlan(
                        0,
                        VisualExecutionAction.PreserveMeaningfulVisual)
                }
                : [];

        var evidence =
            new PageProcessingEvidence(
                1,
                textAuthority,
                visualEvidence);

        var requirements =
            new PageProcessingRequirements(
                1,
                textRequirement,
                visualDisposition);

        var plan =
            new PageExecutionPlan(
                1,
                textMode,
                visualExecution);

        var candidate =
            new PageExecutionPlanningDecision(
                legacy.Assessment,
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

    private static DocumentExtractionPage MissingPage(
        int physicalPageNumber) =>
        new(
            physicalPageNumber,
            sourceText:
                string.Empty,
            wordCount:
                0,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                [],
            blocks:
                []);

    private static DocumentExtractionPage NativePage(
        int physicalPageNumber,
        string text)
    {
        var words =
            text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    (token, index) =>
                        new DocumentWord(
                            index,
                            token,
                            new NormalizedRectangle(
                                0.10 +
                                index *
                                0.03,
                                0.10,
                                0.125 +
                                index *
                                0.03,
                                0.14),
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
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
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
            words,
            blocks:
                [
                    block
                ]);
    }

    private static LayoutObservation LayoutFor(
        DocumentExtractionPage page) =>
        new(
            page.PhysicalPageNumber,
            observationSequence:
                0,
            readingOrder:
                0,
            LayoutObservationKind.Text,
            page.Blocks.Count >
                0
                ? page.Blocks[0].Bounds
                : new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.30),
            "Text");

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

    private sealed class FakeDocumentRasterizer
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
                new FakeRasterizationSession());
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

    private sealed class FakeRasterizationSession
        : IDocumentRasterizationSession
    {
        private const string RasterSha =
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
                    RasterSha));
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
                    RasterSha));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class FakePageLayoutAnalyzer(
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

    private sealed class FakeRegionTextRecognizer(
        string text)
        : IRegionTextRecognizer
    {
        public List<LayoutObservationKind> ObservedKinds { get; } =
            [];

        public ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ObservedKinds.Add(
                sourceLayoutObservation.Kind);

            return ValueTask.FromResult(
                new OcrRegionResult(
                    "fake-ocr",
                    "fake-ocr-profile-v1",
                    sourceLayoutObservation,
                    [
                        new OcrTextObservation(
                            sourceLayoutObservation.PhysicalPageNumber,
                            sourceLayoutObservation.ObservationSequence,
                            0,
                            text,
                            0.95,
                            sourceLayoutObservation.Bounds)
                    ]));
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

    private sealed class FakeVisualRasterObservationSource(
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
