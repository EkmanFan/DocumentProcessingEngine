using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.DualRun.InProcess;

/// <summary>
/// Controlled non-authoritative candidate text execution.
///
/// H.4D.1 executes NativeText and defers OCR-backed modes unless OCR capability
/// is explicitly composed.
///
/// OCR-backed modes execute through the Dual Run candidate
/// raster/layout/OCR runtime. The same page execution may materialize
/// semantically authorized layout visual regions, but those preserved values
/// remain non-authoritative comparison evidence.
///
/// The authoritative page list is already complete before this runner is invoked. No
/// candidate result is returned to authoritative orchestration.
/// </summary>
public sealed class DocumentDualRunCandidateTextExecutionRunner
{
    #region Variables and Constants

    private readonly DocumentDualRunCandidateTextExecutionDependencies
        _dependencies;

    private readonly DocumentDualRunCandidateOcrTextPageExecutor?
        _ocrTextPageExecutor;

    #endregion

    #region ctor

    public DocumentDualRunCandidateTextExecutionRunner(
        DocumentDualRunCandidateTextExecutionDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));

        _ocrTextPageExecutor =
            dependencies.CanExecuteOcrBackedText
                ? new DocumentDualRunCandidateOcrTextPageExecutor(
                    dependencies.LayoutAnalyzer!,
                    dependencies.TextRecognizer!)
                : null;
    }

    #endregion

    #region Methods Execution

    internal bool CanExecuteOcrBackedText =>
        _ocrTextPageExecutor is not null;

    /// <summary>
    /// Backward-compatible H.4D.1 entry point. OCR-backed modes remain deferred
    /// when only NativeText execution was composed.
    /// </summary>
    public ValueTask<DocumentDualRunCandidateTextExecutionReport>
        RunAsync(
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativePages,
            DocumentDualRunPlanningReport dualRunPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default) =>
        RunCoreAsync(
            source:
                null,
            format:
                null,
            extraction,
            authoritativePages,
            dualRunPlanning,
            sourceDocumentSha256,
            cancellationToken);

    /// <summary>
    /// H.4D.2B entry point. The document source is used only by the explicitly
    /// configured Dual Run candidate rasterizer.
    /// </summary>
    public ValueTask<DocumentDualRunCandidateTextExecutionReport>
        RunAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativePages,
            DocumentDualRunPlanningReport dualRunPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return RunCoreAsync(
            source,
            format,
            extraction,
            authoritativePages,
            dualRunPlanning,
            sourceDocumentSha256,
            cancellationToken);
    }

    private async ValueTask<DocumentDualRunCandidateTextExecutionReport>
        RunCoreAsync(
            DocumentSource? source,
            DocumentFormatId? format,
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativePages,
            DocumentDualRunPlanningReport dualRunPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            authoritativePages);

        ArgumentNullException.ThrowIfNull(
            dualRunPlanning);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        cancellationToken.ThrowIfCancellationRequested();

        DocumentDualRunCandidateTextExecutionReport report;

        int? currentPhysicalPageNumber =
            null;

        try
        {
            if (!dualRunPlanning.IsCompleted)
            {
                report =
                    new DocumentDualRunCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentDualRunCandidateTextExecutionStatus
                            .PlanningUnavailable,
                        pages:
                            []);
            }
            else
            {
                ValidateCoverage(
                    extraction,
                    authoritativePages,
                    dualRunPlanning,
                    sourceDocumentSha256);

                var comparisons =
                    new List<DocumentDualRunCandidateTextPageComparison>(
                        extraction.Pages.Count);

                IDocumentRasterizationSession? rasterSession =
                    null;

                try
                {
                    var firstExecutableOcrPage =
                        _ocrTextPageExecutor is null
                            ? null
                            : dualRunPlanning.Pages
                                .FirstOrDefault(
                                    page =>
                                        page
                                            .DualRun
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

                        if (dualRunPlanning.Format !=
                            format.Value)
                        {
                            throw new InvalidDataException(
                                $"Dual Run candidate format '{format.Value}' does not " +
                                $"match Dual Run planning format '{dualRunPlanning.Format}'.");
                        }

                        var rasterizer =
                            _dependencies.DocumentRasterizer ??
                            throw new InvalidOperationException(
                                "Controlled OCR-backed candidate execution has no rasterizer.");

                        if (!rasterizer.CanRasterize(
                                format.Value))
                        {
                            throw new NotSupportedException(
                                $"The Dual Run candidate rasterizer cannot process " +
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
                                "Dual Run candidate rasterizer returned no session.");
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
                            authoritativePages[index];

                        var dualRunPage =
                            dualRunPlanning.Pages[index];

                        currentPhysicalPageNumber =
                            extractionPage.PhysicalPageNumber;

                        var candidatePlan =
                            dualRunPage
                                .DualRun
                                .Candidate
                                .Plan;

                        HybridDocumentPage candidatePage;

                        IReadOnlyList<LayoutVisualEvidence>
                            layoutVisualEvidence =
                                [];

                        IReadOnlyList<PreservedVisualEvidence>
                            preservedLayoutVisuals =
                                [];

                        DocumentDualRunCandidateTextPageStatus pageStatus;

                        if (candidatePlan.TextMode ==
                            TextExecutionMode.NativeText)
                        {
                            candidatePage =
                                NativeHybridPageAssembler
                                    .Assemble(
                                        extractionPage);

                            pageStatus =
                                DocumentDualRunCandidateTextPageStatus
                                    .ExecutedNativeText;
                        }
                        else if (_ocrTextPageExecutor is null)
                        {
                            comparisons.Add(
                                new DocumentDualRunCandidateTextPageComparison(
                                    extractionPage.PhysicalPageNumber,
                                    dualRunPage
                                        .Authoritative
                                        .Plan
                                        .Route,
                                    candidatePlan.TextMode,
                                    DocumentDualRunCandidateTextPageStatus
                                        .DeferredNonNativeTextMode,
                                    candidateRemovesAuthoritativeTextMl:
                                        false,
                                    candidateHasIndependentVisualWork:
                                        dualRunPage
                                            .DualRun
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
                                        dualRunPage
                                            .DualRun
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
                                dualRunPage,
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
                    new DocumentDualRunCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentDualRunCandidateTextExecutionStatus.Completed,
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
                new DocumentDualRunCandidateTextExecutionReport(
                    sourceDocumentSha256,
                    DocumentDualRunCandidateTextExecutionStatus.Failed,
                    pages:
                        [],
                    new DocumentDualRunCandidateTextExecutionFailure(
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

    #endregion

    #region Methods Comparison

    private static DocumentDualRunCandidateTextPageComparison Compare(
        HybridDocumentPage authoritativePage,
        HybridDocumentPage candidatePage,
        DocumentDualRunPageComparison dualRunPage,
        DocumentDualRunCandidateTextPageStatus status,
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

        return new DocumentDualRunCandidateTextPageComparison(
            dualRunPage.PhysicalPageNumber,
            dualRunPage
                .Authoritative
                .Plan
                .Route,
            dualRunPage
                .DualRun
                .Candidate
                .Plan
                .TextMode,
            status,
            candidateRemovesAuthoritativeTextMl:
                dualRunPage
                    .CandidateRemovesAuthoritativeTextMl,
            candidateHasIndependentVisualWork:
                dualRunPage
                    .DualRun
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

    private static DocumentDualRunCandidateTextPageStatus ExecutedStatus(
        TextExecutionMode textMode) =>
        textMode switch
        {
            TextExecutionMode.TargetedOcrRecovery =>
                DocumentDualRunCandidateTextPageStatus
                    .ExecutedTargetedOcrRecovery,

            TextExecutionMode.TargetedOcrVerification =>
                DocumentDualRunCandidateTextPageStatus
                    .ExecutedTargetedOcrVerification,

            TextExecutionMode.TargetedOcrReconciliation =>
                DocumentDualRunCandidateTextPageStatus
                    .ExecutedTargetedOcrReconciliation,

            _ =>
                throw new InvalidOperationException(
                    $"Text mode '{textMode}' is not an OCR-backed controlled mode.")
        };

    #endregion

    #region Methods Validation

    private static void ValidateCoverage(
        DocumentExtractionResult extraction,
        IReadOnlyList<HybridDocumentPage> authoritativePages,
        DocumentDualRunPlanningReport dualRunPlanning,
        string sourceDocumentSha256)
    {
        if (!string.Equals(
                dualRunPlanning.SourceDocumentSha256,
                sourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Dual Run candidate source SHA-256 does not match Dual Run planning evidence.");
        }

        if (authoritativePages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Authoritative page count {authoritativePages.Count} " +
                $"does not match extraction page count {extraction.Pages.Count}.");
        }

        if (dualRunPlanning.Pages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Dual Run planning page count {dualRunPlanning.Pages.Count} " +
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

            if (authoritativePages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Authoritative page at index {index} is " +
                    $"{authoritativePages[index].PhysicalPageNumber}; " +
                    $"expected {expected}.");
            }

            if (dualRunPlanning.Pages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Dual Run planning page at index {index} is " +
                    $"{dualRunPlanning.Pages[index].PhysicalPageNumber}; " +
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

    #endregion

    #region Methods Telemetry

    private async ValueTask DeliverBestEffortAsync(
        DocumentDualRunCandidateTextExecutionReport report,
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
            // Dual Run candidate telemetry is non-authoritative.
        }
    }

    #endregion
}
