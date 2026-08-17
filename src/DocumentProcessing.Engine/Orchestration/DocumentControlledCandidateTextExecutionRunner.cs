using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Controlled non-authoritative candidate text execution.
///
/// H.4D.1 executes NativeText and defers OCR-backed modes unless OCR capability
/// is explicitly composed.
///
/// OCR-backed modes execute through the controlled candidate
/// raster/layout/OCR runtime. The same page execution may materialize
/// semantically authorized layout visual regions, but those preserved values
/// remain non-authoritative comparison evidence.
///
/// The legacy page list is already complete before this runner is invoked. No
/// candidate result is returned to authoritative orchestration.
/// </summary>
public sealed class DocumentControlledCandidateTextExecutionRunner
{
    private readonly DocumentControlledCandidateTextExecutionDependencies
        _dependencies;

    private readonly DocumentControlledCandidateOcrTextPageExecutor?
        _ocrTextPageExecutor;

    public DocumentControlledCandidateTextExecutionRunner(
        DocumentControlledCandidateTextExecutionDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));

        _ocrTextPageExecutor =
            dependencies.CanExecuteOcrBackedText
                ? new DocumentControlledCandidateOcrTextPageExecutor(
                    dependencies.LayoutAnalyzer!,
                    dependencies.TextRecognizer!)
                : null;
    }

    internal bool CanExecuteOcrBackedText =>
        _ocrTextPageExecutor is not null;

    /// <summary>
    /// Backward-compatible H.4D.1 entry point. OCR-backed modes remain deferred
    /// when only NativeText execution was composed.
    /// </summary>
    public ValueTask<DocumentControlledCandidateTextExecutionReport>
        RunAsync(
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
            DocumentShadowPlanningReport shadowPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default) =>
        RunCoreAsync(
            source:
                null,
            format:
                null,
            extraction,
            authoritativeLegacyPages,
            shadowPlanning,
            sourceDocumentSha256,
            cancellationToken);

    /// <summary>
    /// H.4D.2B entry point. The document source is used only by the explicitly
    /// configured controlled candidate rasterizer.
    /// </summary>
    public ValueTask<DocumentControlledCandidateTextExecutionReport>
        RunAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
            DocumentShadowPlanningReport shadowPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return RunCoreAsync(
            source,
            format,
            extraction,
            authoritativeLegacyPages,
            shadowPlanning,
            sourceDocumentSha256,
            cancellationToken);
    }

    private async ValueTask<DocumentControlledCandidateTextExecutionReport>
        RunCoreAsync(
            DocumentSource? source,
            DocumentFormatId? format,
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
            DocumentShadowPlanningReport shadowPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            authoritativeLegacyPages);

        ArgumentNullException.ThrowIfNull(
            shadowPlanning);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        cancellationToken.ThrowIfCancellationRequested();

        DocumentControlledCandidateTextExecutionReport report;

        int? currentPhysicalPageNumber =
            null;

        try
        {
            if (!shadowPlanning.IsCompleted)
            {
                report =
                    new DocumentControlledCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateTextExecutionStatus
                            .PlanningUnavailable,
                        pages:
                            []);
            }
            else
            {
                ValidateCoverage(
                    extraction,
                    authoritativeLegacyPages,
                    shadowPlanning,
                    sourceDocumentSha256);

                var comparisons =
                    new List<DocumentControlledCandidateTextPageComparison>(
                        extraction.Pages.Count);

                IDocumentRasterizationSession? rasterSession =
                    null;

                try
                {
                    var firstExecutableOcrPage =
                        _ocrTextPageExecutor is null
                            ? null
                            : shadowPlanning.Pages
                                .FirstOrDefault(
                                    page =>
                                        page
                                            .Shadow
                                            .Candidate
                                            .Plan
                                            .TextMode !=
                                        TextExecutionMode.NativeText);

                    if (firstExecutableOcrPage is not null)
                    {
                        currentPhysicalPageNumber =
                            firstExecutableOcrPage.PhysicalPageNumber;

                        if (source is null ||
                            format is null)
                        {
                            throw new InvalidOperationException(
                                "Controlled OCR-backed candidate execution requires " +
                                "the prepared document source and detected format.");
                        }

                        if (shadowPlanning.Format !=
                            format.Value)
                        {
                            throw new InvalidDataException(
                                $"Controlled candidate format '{format.Value}' does not " +
                                $"match shadow-planning format '{shadowPlanning.Format}'.");
                        }

                        var rasterizer =
                            _dependencies.DocumentRasterizer ??
                            throw new InvalidOperationException(
                                "Controlled OCR-backed candidate execution has no rasterizer.");

                        if (!rasterizer.CanRasterize(
                                format.Value))
                        {
                            throw new NotSupportedException(
                                $"The controlled candidate rasterizer cannot process " +
                                $"format '{format.Value}'.");
                        }

                        rasterSession =
                            await rasterizer
                                .OpenAsync(
                                    source,
                                    format.Value,
                                    cancellationToken)
                                .ConfigureAwait(false);

                        if (rasterSession is null)
                        {
                            throw new InvalidDataException(
                                "Controlled candidate rasterizer returned no session.");
                        }
                    }

                    for (var index = 0;
                         index <
                         extraction.Pages.Count;
                         index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var extractionPage =
                            extraction.Pages[index];

                        var authoritativePage =
                            authoritativeLegacyPages[index];

                        var shadowPage =
                            shadowPlanning.Pages[index];

                        currentPhysicalPageNumber =
                            extractionPage.PhysicalPageNumber;

                        var candidatePlan =
                            shadowPage
                                .Shadow
                                .Candidate
                                .Plan;

                        HybridDocumentPage candidatePage;

                        IReadOnlyList<LayoutVisualEvidence>
                            layoutVisualEvidence =
                                [];

                        IReadOnlyList<PreservedVisualEvidence>
                            preservedLayoutVisuals =
                                [];

                        DocumentControlledCandidateTextPageStatus pageStatus;

                        if (candidatePlan.TextMode ==
                            TextExecutionMode.NativeText)
                        {
                            candidatePage =
                                NativeHybridPageAssembler
                                    .Assemble(
                                        extractionPage);

                            pageStatus =
                                DocumentControlledCandidateTextPageStatus
                                    .ExecutedNativeText;
                        }
                        else if (_ocrTextPageExecutor is null)
                        {
                            comparisons.Add(
                                new DocumentControlledCandidateTextPageComparison(
                                    extractionPage.PhysicalPageNumber,
                                    shadowPage
                                        .AuthoritativeLegacy
                                        .Plan
                                        .Route,
                                    candidatePlan.TextMode,
                                    DocumentControlledCandidateTextPageStatus
                                        .DeferredNonNativeTextMode,
                                    candidateRemovesLegacyTextMl:
                                        false,
                                    candidateHasIndependentVisualWork:
                                        shadowPage
                                            .Shadow
                                            .CandidateHasIndependentVisualWork));

                            continue;
                        }
                        else
                        {
                            if (rasterSession is null)
                            {
                                throw new InvalidOperationException(
                                    "Controlled OCR-backed candidate page reached " +
                                    "execution without a document-scoped raster session.");
                            }

                            var ocrExecution =
                                await _ocrTextPageExecutor
                                    .ExecuteAsync(
                                        extractionPage,
                                        shadowPage
                                            .Shadow
                                            .Candidate
                                            .NativeAssessment
                                            .NativeTextStatus,
                                        candidatePlan.TextMode,
                                        rasterSession,
                                        sourceDocumentSha256,
                                        cancellationToken)
                                    .ConfigureAwait(false);

                            candidatePage =
                                ocrExecution.Page;

                            layoutVisualEvidence =
                                ocrExecution.LayoutVisualEvidence;

                            preservedLayoutVisuals =
                                ocrExecution.PreservedLayoutVisuals;

                            pageStatus =
                                ExecutedStatus(
                                    candidatePlan.TextMode);
                        }

                        comparisons.Add(
                            Compare(
                                authoritativePage,
                                candidatePage,
                                shadowPage,
                                pageStatus,
                                layoutVisualEvidence,
                                preservedLayoutVisuals));
                    }
                }
                finally
                {
                    if (rasterSession is not null)
                    {
                        await rasterSession
                            .DisposeAsync()
                            .ConfigureAwait(false);
                    }
                }

                report =
                    new DocumentControlledCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateTextExecutionStatus.Completed,
                        comparisons);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            report =
                new DocumentControlledCandidateTextExecutionReport(
                    sourceDocumentSha256,
                    DocumentControlledCandidateTextExecutionStatus.Failed,
                    pages:
                        [],
                    new DocumentControlledCandidateTextExecutionFailure(
                        exception.GetType().FullName ??
                        exception.GetType().Name,
                        exception.Message,
                        currentPhysicalPageNumber));
        }

        await DeliverBestEffortAsync(
                report,
                cancellationToken)
            .ConfigureAwait(false);

        return report;
    }

    private static DocumentControlledCandidateTextPageComparison Compare(
        HybridDocumentPage authoritativePage,
        HybridDocumentPage candidatePage,
        DocumentShadowPageComparison shadowPage,
        DocumentControlledCandidateTextPageStatus status,
        IReadOnlyList<LayoutVisualEvidence>
            layoutVisualEvidence,
        IReadOnlyList<PreservedVisualEvidence>
            preservedLayoutVisuals)
    {
        var authoritativeText =
            authoritativePage
                .AuthoritativeTextElements;

        var candidateText =
            candidatePage
                .AuthoritativeTextElements;

        return new DocumentControlledCandidateTextPageComparison(
            shadowPage.PhysicalPageNumber,
            shadowPage
                .AuthoritativeLegacy
                .Plan
                .Route,
            shadowPage
                .Shadow
                .Candidate
                .Plan
                .TextMode,
            status,
            candidateRemovesLegacyTextMl:
                shadowPage
                    .CandidateRemovesLegacyTextMl,
            candidateHasIndependentVisualWork:
                shadowPage
                    .Shadow
                    .CandidateHasIndependentVisualWork,
            SelectedTextSequenceExact(
                authoritativeText,
                candidateText),
            TextProjectionExact(
                authoritativeText,
                candidateText),
            authoritativeText.Count,
            candidateText.Count,
            authoritativeText.Count(
                element =>
                    element.Reconciliation is not null),
            candidateText.Count(
                element =>
                    element.Reconciliation is not null),
            candidatePage:
                candidatePage,
            candidateLayoutVisualEvidence:
                layoutVisualEvidence,
            candidatePreservedLayoutVisuals:
                preservedLayoutVisuals);
    }

    private static DocumentControlledCandidateTextPageStatus ExecutedStatus(
        TextExecutionMode textMode) =>
        textMode switch
        {
            TextExecutionMode.TargetedOcrRecovery =>
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrRecovery,

            TextExecutionMode.TargetedOcrVerification =>
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrVerification,

            TextExecutionMode.TargetedOcrReconciliation =>
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrReconciliation,

            _ =>
                throw new InvalidOperationException(
                    $"Text mode '{textMode}' is not an OCR-backed controlled mode.")
        };

    private static void ValidateCoverage(
        DocumentExtractionResult extraction,
        IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
        DocumentShadowPlanningReport shadowPlanning,
        string sourceDocumentSha256)
    {
        if (!string.Equals(
                shadowPlanning.SourceDocumentSha256,
                sourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Controlled candidate source SHA-256 does not match shadow-planning evidence.");
        }

        if (authoritativeLegacyPages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Authoritative legacy page count {authoritativeLegacyPages.Count} " +
                $"does not match extraction page count {extraction.Pages.Count}.");
        }

        if (shadowPlanning.Pages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Shadow-planning page count {shadowPlanning.Pages.Count} " +
                $"does not match extraction page count {extraction.Pages.Count}.");
        }

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var expected =
                extraction.Pages[index]
                    .PhysicalPageNumber;

            if (authoritativeLegacyPages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Authoritative legacy page at index {index} is " +
                    $"{authoritativeLegacyPages[index].PhysicalPageNumber}; " +
                    $"expected {expected}.");
            }

            if (shadowPlanning.Pages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Shadow-planning page at index {index} is " +
                    $"{shadowPlanning.Pages[index].PhysicalPageNumber}; " +
                    $"expected {expected}.");
            }
        }
    }

    private static bool SelectedTextSequenceExact(
        IReadOnlyList<HybridDocumentElement> authoritative,
        IReadOnlyList<HybridDocumentElement> candidate)
    {
        if (authoritative.Count !=
            candidate.Count)
        {
            return false;
        }

        for (var index = 0;
             index <
             authoritative.Count;
             index++)
        {
            if (!string.Equals(
                    authoritative[index].Text,
                    candidate[index].Text,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TextProjectionExact(
        IReadOnlyList<HybridDocumentElement> authoritative,
        IReadOnlyList<HybridDocumentElement> candidate)
    {
        if (authoritative.Count !=
            candidate.Count)
        {
            return false;
        }

        for (var index = 0;
             index <
             authoritative.Count;
             index++)
        {
            var left =
                authoritative[index];

            var right =
                candidate[index];

            if (left.ReadingOrder !=
                    right.ReadingOrder ||
                left.Kind !=
                    right.Kind ||
                left.Bounds !=
                    right.Bounds ||
                !string.Equals(
                    left.Text,
                    right.Text,
                    StringComparison.Ordinal) ||
                left.TextOrigin !=
                    right.TextOrigin ||
                left.NativeBlock?.SourceSequence !=
                    right.NativeBlock?.SourceSequence ||
                (left.Reconciliation is null) !=
                    (right.Reconciliation is null))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask DeliverBestEffortAsync(
        DocumentControlledCandidateTextExecutionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dependencies
                .Observer
                .ObserveAsync(
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            // Controlled candidate telemetry is non-authoritative.
        }
    }
}
