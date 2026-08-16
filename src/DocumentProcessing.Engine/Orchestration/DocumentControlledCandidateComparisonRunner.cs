using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Deterministic H.4D.4A cross-axis candidate comparison.
///
/// The runner performs no document processing. It validates that H.4C planning,
/// H.4D.2B text execution and H.4D.3B visual execution refer to the same source
/// and exact page/action coverage, then surfaces explicit cutover blockers.
///
/// H.4D.4A deliberately cannot clear PortableOutputNotCompared or
/// ProvenanceNotCompared because no candidate DocumentIngestionResult exists
/// yet. That work belongs to H.4D.4B.
/// </summary>
public sealed class DocumentControlledCandidateComparisonRunner
{
    private readonly DocumentControlledCandidateComparisonDependencies
        _dependencies;

    public DocumentControlledCandidateComparisonRunner(
        DocumentControlledCandidateComparisonDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    public async ValueTask<DocumentControlledCandidateComparisonReport>
        RunAsync(
            DocumentIngestionResult authoritativeResult,
            DocumentShadowPlanningReport shadowPlanning,
            DocumentControlledCandidateTextExecutionReport textExecution,
            DocumentControlledCandidateVisualExecutionReport visualExecution,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeResult);

        ArgumentNullException.ThrowIfNull(
            shadowPlanning);

        ArgumentNullException.ThrowIfNull(
            textExecution);

        ArgumentNullException.ThrowIfNull(
            visualExecution);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentControlledCandidateComparisonReport report;

        try
        {
            ValidateCustody(
                authoritativeResult,
                shadowPlanning,
                textExecution,
                visualExecution);

            if (!shadowPlanning.IsCompleted)
            {
                report =
                    new DocumentControlledCandidateComparisonReport(
                        authoritativeResult.Source.Sha256,
                        DocumentControlledCandidateComparisonStatus
                            .PlanningUnavailable,
                        pages:
                            [],
                        cutoverBlockers:
                            [
                                DocumentControlledCandidateCutoverBlocker
                                    .PortableOutputNotCompared,
                                DocumentControlledCandidateCutoverBlocker
                                    .ProvenanceNotCompared
                            ]);
            }
            else if (textExecution.Status !=
                         DocumentControlledCandidateTextExecutionStatus.Completed ||
                     visualExecution.Status !=
                         DocumentControlledCandidateVisualExecutionStatus.Completed)
            {
                var blockers =
                    new List<DocumentControlledCandidateCutoverBlocker>();

                if (textExecution.Status !=
                    DocumentControlledCandidateTextExecutionStatus.Completed)
                {
                    blockers.Add(
                        DocumentControlledCandidateCutoverBlocker
                            .TextExecutionUnavailable);
                }

                if (visualExecution.Status !=
                    DocumentControlledCandidateVisualExecutionStatus.Completed)
                {
                    blockers.Add(
                        DocumentControlledCandidateCutoverBlocker
                            .VisualExecutionUnavailable);
                }

                AddProjectionBlockers(
                    blockers);

                report =
                    new DocumentControlledCandidateComparisonReport(
                        authoritativeResult.Source.Sha256,
                        DocumentControlledCandidateComparisonStatus
                            .CandidateExecutionUnavailable,
                        pages:
                            [],
                        blockers);
            }
            else
            {
                report =
                    CompareCompleted(
                        authoritativeResult,
                        shadowPlanning,
                        textExecution,
                        visualExecution,
                        cancellationToken);
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
                new DocumentControlledCandidateComparisonReport(
                    authoritativeResult.Source.Sha256,
                    DocumentControlledCandidateComparisonStatus.Failed,
                    pages:
                        [],
                    cutoverBlockers:
                        [],
                    new DocumentControlledCandidateComparisonFailure(
                        exception.GetType().FullName ??
                        exception.GetType().Name,
                        exception.Message));
        }

        await DeliverBestEffortAsync(
                report,
                cancellationToken)
            .ConfigureAwait(false);

        return report;
    }

    private static DocumentControlledCandidateComparisonReport CompareCompleted(
        DocumentIngestionResult authoritativeResult,
        DocumentShadowPlanningReport shadowPlanning,
        DocumentControlledCandidateTextExecutionReport textExecution,
        DocumentControlledCandidateVisualExecutionReport visualExecution,
        CancellationToken cancellationToken)
    {
        var pageCount =
            authoritativeResult.Pages.Count;

        if (shadowPlanning.Pages.Count !=
                pageCount ||
            textExecution.Pages.Count !=
                pageCount ||
            visualExecution.Pages.Count !=
                pageCount)
        {
            throw new InvalidDataException(
                "H.4D.4A requires exact page coverage across authority, planning, " +
                "controlled text execution and controlled visual execution.");
        }

        var pages =
            new List<DocumentControlledCandidatePageComparison>(
                pageCount);

        var blockers =
            new List<DocumentControlledCandidateCutoverBlocker>();

        for (var index = 0;
             index <
             pageCount;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var authoritativePage =
                authoritativeResult.Pages[
                    index];

            var shadowPage =
                shadowPlanning.Pages[
                    index];

            var textPage =
                textExecution.Pages[
                    index];

            var visualPage =
                visualExecution.Pages[
                    index];

            var physicalPageNumber =
                index +
                1;

            if (authoritativePage.PhysicalPageNumber !=
                    physicalPageNumber ||
                shadowPage.PhysicalPageNumber !=
                    physicalPageNumber ||
                textPage.PhysicalPageNumber !=
                    physicalPageNumber ||
                visualPage.PhysicalPageNumber !=
                    physicalPageNumber)
            {
                throw new InvalidDataException(
                    $"H.4D.4A page identity mismatch at index {index}.");
            }

            var authoritativeRoute =
                shadowPage
                    .AuthoritativeLegacy
                    .Plan
                    .Route;

            if (textPage.AuthoritativeLegacyRoute !=
                    authoritativeRoute ||
                visualPage.AuthoritativeLegacyRoute !=
                    authoritativeRoute)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} controlled evidence does not " +
                    "agree on the authoritative legacy route.");
            }

            var candidatePlan =
                shadowPage
                    .Shadow
                    .Candidate
                    .Plan;

            if (textPage.CandidateTextMode !=
                candidatePlan.TextMode)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} controlled text mode does not " +
                    "match the H.4C candidate plan.");
            }

            ValidateTextExecutionStatus(
                textPage);

            if (textPage.CandidateRemovesLegacyTextMl !=
                shadowPage.CandidateRemovesLegacyTextMl)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} candidate-removes-legacy-ML " +
                    "evidence is inconsistent.");
            }

            if (textPage.CandidateHasIndependentVisualWork !=
                shadowPage
                    .Shadow
                    .CandidateHasIndependentVisualWork)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} text report visual-work flag " +
                    "does not match H.4C.");
            }

            var plannedVisuals =
                candidatePlan
                    .VisualElements
                    .OrderBy(
                        visual =>
                            visual.SourceVisualIndex)
                    .ToArray();

            var executedVisuals =
                visualPage
                    .Elements
                    .OrderBy(
                        visual =>
                            visual.SourceVisualIndex)
                    .ToArray();

            if (plannedVisuals.Length !=
                executedVisuals.Length)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} visual execution count does not " +
                    "match the H.4C candidate plan.");
            }

            for (var visualIndex = 0;
                 visualIndex <
                 plannedVisuals.Length;
                 visualIndex++)
            {
                var planned =
                    plannedVisuals[
                        visualIndex];

                var executed =
                    executedVisuals[
                        visualIndex];

                if (planned.SourceVisualIndex !=
                        executed.SourceVisualIndex ||
                    planned.Action !=
                        executed.Action)
                {
                    throw new InvalidDataException(
                        $"Physical page {physicalPageNumber} source visual " +
                        $"{visualIndex} executed action does not match H.4C.");
                }
            }

            if (textPage.Status ==
                DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode)
            {
                blockers.Add(
                    DocumentControlledCandidateCutoverBlocker
                        .TextExecutionIncomplete);
            }

            if (textPage.SelectedTextSequenceExact ==
                false)
            {
                blockers.Add(
                    DocumentControlledCandidateCutoverBlocker
                        .SelectedTextSequenceDivergence);
            }

            if (textPage.TextProjectionExact ==
                false)
            {
                blockers.Add(
                    DocumentControlledCandidateCutoverBlocker
                        .TextProjectionDivergence);
            }

            var visualActions =
                executedVisuals
                    .Select(
                        visual =>
                            visual.Action)
                    .ToArray();

            if (visualActions.Contains(
                    VisualExecutionAction.PreserveMeaningfulVisual))
            {
                blockers.Add(
                    DocumentControlledCandidateCutoverBlocker
                        .CandidateVisualPersistenceNotCompared);
            }

            pages.Add(
                new DocumentControlledCandidatePageComparison(
                    physicalPageNumber,
                    authoritativeRoute,
                    candidatePlan.TextMode,
                    textPage.Status,
                    textPage.SelectedTextSequenceExact,
                    textPage.TextProjectionExact,
                    visualActions,
                    visualPlanExecutionExact:
                        true,
                    textPage.CandidateRemovesLegacyTextMl,
                    shadowPage
                        .CandidateAddsIndependentVisualWorkToLegacyNativePage));
        }

        AddProjectionBlockers(
            blockers);

        return new DocumentControlledCandidateComparisonReport(
            authoritativeResult.Source.Sha256,
            DocumentControlledCandidateComparisonStatus.Completed,
            pages,
            blockers);
    }

    private static void ValidateCustody(
        DocumentIngestionResult authoritativeResult,
        DocumentShadowPlanningReport shadowPlanning,
        DocumentControlledCandidateTextExecutionReport textExecution,
        DocumentControlledCandidateVisualExecutionReport visualExecution)
    {
        var sourceSha =
            authoritativeResult.Source.Sha256;

        if (!string.Equals(
                shadowPlanning.SourceDocumentSha256,
                sourceSha,
                StringComparison.Ordinal) ||
            !string.Equals(
                textExecution.SourceDocumentSha256,
                sourceSha,
                StringComparison.Ordinal) ||
            !string.Equals(
                visualExecution.SourceDocumentSha256,
                sourceSha,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "H.4D.4A comparison inputs do not share the authoritative source SHA-256.");
        }

        if (shadowPlanning.Format !=
            authoritativeResult.Source.Format)
        {
            throw new InvalidDataException(
                "H.4D.4A shadow-planning format does not match the authoritative source.");
        }

        if (authoritativeResult.Source.PhysicalPageCount !=
            authoritativeResult.Pages.Count)
        {
            throw new InvalidDataException(
                "Authoritative result page count does not match source custody.");
        }
    }

    private static void ValidateTextExecutionStatus(
        DocumentControlledCandidateTextPageComparison page)
    {
        var expectedStatus =
            page.CandidateTextMode switch
            {
                TextExecutionMode.NativeText =>
                    DocumentControlledCandidateTextPageStatus.ExecutedNativeText,

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
                        $"Unsupported candidate text mode '{page.CandidateTextMode}'.")
            };

        if (page.Status ==
                DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode &&
            page.CandidateTextMode !=
                TextExecutionMode.NativeText)
        {
            return;
        }

        if (page.Status !=
            expectedStatus)
        {
            throw new InvalidDataException(
                $"Physical page {page.PhysicalPageNumber} controlled text status " +
                $"'{page.Status}' does not match candidate mode " +
                $"'{page.CandidateTextMode}'.");
        }
    }

    private static void AddProjectionBlockers(
        ICollection<DocumentControlledCandidateCutoverBlocker> blockers)
    {
        blockers.Add(
            DocumentControlledCandidateCutoverBlocker
                .PortableOutputNotCompared);

        blockers.Add(
            DocumentControlledCandidateCutoverBlocker
                .ProvenanceNotCompared);
    }

    private async ValueTask DeliverBestEffortAsync(
        DocumentControlledCandidateComparisonReport report,
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
            // Comparison telemetry is non-authoritative.
        }
    }
}
