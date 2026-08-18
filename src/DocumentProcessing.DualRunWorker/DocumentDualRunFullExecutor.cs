using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.DualRunWorker;

/// <summary>
/// Full worker execution boundary.
///
/// This checkpoint executes NativeText candidate pages and produces the complete
/// Full transport comparison for them. OCR-backed candidate pages fail closed
/// before any ML/raster runtime is composed. The next increment replaces that
/// fail-closed gate with lazy PP/Paddle composition.
/// </summary>
internal sealed class DocumentDualRunFullExecutor
{
    #region Methods Execution

    public async ValueTask<IReadOnlyList<DocumentDualRunWorkerPageResult>> ExecuteAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.ExecutionMode !=
            DocumentDualRunExecutionMode.Full)
        {
            throw new ArgumentException(
                "Full executor requires Full execution mode.",
                nameof(request));
        }

        var planning =
            await new DocumentDualRunWorkerPlanningPipeline()
                .ExecuteAsync(
                    jobDirectoryPath,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        var firstOcrBackedPage =
            planning.Decisions
                .FirstOrDefault(
                    decision =>
                        decision
                            .Candidate
                            .Plan
                            .TextMode !=
                        TextExecutionMode.NativeText);

        if (firstOcrBackedPage is not null)
        {
            throw new InvalidOperationException(
                $"Dual Run Full worker requires lazy OCR-backed runtime for physical page " +
                $"{firstOcrBackedPage.PhysicalPageNumber}; PP/Paddle composition is not wired yet.");
        }

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

            var sourcePage =
                planning
                    .Extraction
                    .Pages[index];

            var decision =
                planning
                    .Decisions[index];

            var authoritativeBaseline =
                request
                    .AuthoritativePages[index];

            var candidatePage =
                AssembleNativePage(
                    sourcePage);

            pages[index] =
                BuildExecutedNativeResult(
                    authoritativeBaseline,
                    decision,
                    candidatePage);
        }

        return Array.AsReadOnly(
            pages);
    }

    #endregion

    #region Methods Native Execution

    private static HybridDocumentPage AssembleNativePage(
        DocumentProcessing.Core.Extraction.DocumentExtractionPage sourcePage)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        if (sourcePage.Blocks.Count ==
            0)
        {
            throw new InvalidDataException(
                $"NativeText candidate page {sourcePage.PhysicalPageNumber} contains no native text blocks.");
        }

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

    #endregion

    #region Methods Comparison

    private static DocumentDualRunWorkerPageResult BuildExecutedNativeResult(
        DocumentDualRunAuthoritativePageBaseline authoritativeBaseline,
        GuardedPagePlanningDecision decision,
        HybridDocumentPage candidatePage)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeBaseline);

        ArgumentNullException.ThrowIfNull(
            decision);

        ArgumentNullException.ThrowIfNull(
            candidatePage);

        if (authoritativeBaseline.PhysicalPageNumber !=
                decision.PhysicalPageNumber ||
            candidatePage.PhysicalPageNumber !=
                decision.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Dual Run Full native comparison page identities do not agree.");
        }

        var candidatePlan =
            decision
                .Candidate
                .Plan;

        if (candidatePlan.TextMode !=
            TextExecutionMode.NativeText)
        {
            throw new InvalidOperationException(
                "Native Full result projection requires NativeText candidate mode.");
        }

        var authoritativePlanningAgreement =
            authoritativeBaseline.NativeTextStatus ==
                decision.Authoritative.Assessment.NativeTextStatus &&
            authoritativeBaseline.AuthoritativeRoute ==
                decision.Authoritative.Plan.Route;

        var candidateRemovesAuthoritativeTextMl =
            authoritativeBaseline.AuthoritativeRoute !=
                PageProcessingRoute.NativeOnly;

        var candidateText =
            candidatePage
                .AuthoritativeTextElements;

        var selectedTextSequenceExact =
            string.Equals(
                authoritativeBaseline.SelectedTextSequenceSha256,
                DocumentDualRunTextFingerprint
                    .SelectedTextSequenceSha256(
                        candidateText),
                StringComparison.Ordinal);

        var textProjectionExact =
            string.Equals(
                authoritativeBaseline.TextProjectionSha256,
                DocumentDualRunTextFingerprint
                    .TextProjectionSha256(
                        candidateText),
                StringComparison.Ordinal);

        return new DocumentDualRunWorkerPageResult(
            authoritativeBaseline.PhysicalPageNumber,
            authoritativePlanningAgreement,
            candidatePlan.TextMode,
            candidateRemovesAuthoritativeTextMl,
            candidatePlan.RequiresVisualAnalysis,
            candidatePlan.RequiresMeaningfulVisualPreservation,
            DocumentDualRunCandidateTextPageStatus.ExecutedNativeText,
            selectedTextSequenceExact,
            textProjectionExact,
            authoritativeBaseline.AuthoritativeTextElementCount,
            candidateText.Count,
            authoritativeBaseline.AuthoritativeReconciliationEvidenceCount,
            candidateText.Count(
                element =>
                    element.Reconciliation is not null));
    }

    #endregion
}
