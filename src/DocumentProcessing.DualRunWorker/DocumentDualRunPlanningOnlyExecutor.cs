using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.DualRunWorker;

/// <summary>
/// Executes deterministic PlanningOnly projection from the worker's independently
/// owned source snapshot.
///
/// No layout ML, OCR, raster rendering, or candidate page execution is invoked.
/// </summary>
internal sealed class DocumentDualRunPlanningOnlyExecutor
{
    #region Methods Planning

    public async ValueTask<IReadOnlyList<DocumentDualRunWorkerPageResult>> ExecuteAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.ExecutionMode !=
            DocumentDualRunExecutionMode.PlanningOnly)
        {
            throw new ArgumentException(
                "PlanningOnly executor requires PlanningOnly execution mode.",
                nameof(request));
        }

        var planning =
            await new DocumentDualRunWorkerPlanningPipeline()
                .ExecuteAsync(
                    jobDirectoryPath,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        var pages =
            new DocumentDualRunWorkerPageResult[
                planning.Decisions.Count];

        for (var index = 0;
             index <
             planning.Decisions.Count;
             index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            pages[index] =
                BuildPageResult(
                    request.AuthoritativePages[index],
                    planning.Decisions[index]);
        }

        return Array.AsReadOnly(
            pages);
    }

    #endregion

    #region Methods Comparison

    private static DocumentDualRunWorkerPageResult BuildPageResult(
        DocumentDualRunAuthoritativePageBaseline authoritativeBaseline,
        GuardedPagePlanningDecision decision)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeBaseline);

        ArgumentNullException.ThrowIfNull(
            decision);

        if (authoritativeBaseline.PhysicalPageNumber !=
            decision.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Dual Run worker candidate decision refers to physical page " +
                $"{decision.PhysicalPageNumber}; expected " +
                $"{authoritativeBaseline.PhysicalPageNumber}.");
        }

        var candidatePlan =
            decision
                .Candidate
                .Plan;

        var authoritativePlanningAgreement =
            authoritativeBaseline.NativeTextStatus ==
                decision.Authoritative.Assessment.NativeTextStatus &&
            authoritativeBaseline.AuthoritativeRoute ==
                decision.Authoritative.Plan.Route;

        var candidateRemovesAuthoritativeTextMl =
            authoritativeBaseline.AuthoritativeRoute !=
                PageProcessingRoute.NativeOnly &&
            candidatePlan.TextMode ==
                TextExecutionMode.NativeText;

        return new DocumentDualRunWorkerPageResult(
            authoritativeBaseline.PhysicalPageNumber,
            authoritativePlanningAgreement,
            candidatePlan.TextMode,
            candidateRemovesAuthoritativeTextMl,
            candidatePlan.RequiresVisualAnalysis,
            candidatePlan.RequiresMeaningfulVisualPreservation);
    }

    #endregion
}
