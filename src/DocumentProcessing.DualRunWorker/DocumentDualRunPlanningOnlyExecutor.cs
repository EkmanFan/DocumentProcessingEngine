using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.DualRunWorker;

/// <summary>
/// Executes the deterministic PlanningOnly candidate pipeline from the worker's
/// independently owned source snapshot.
///
/// No layout ML, OCR, or candidate page execution is invoked here.
/// </summary>
internal sealed class DocumentDualRunPlanningOnlyExecutor
{
    #region Methods Planning

    public async ValueTask<IReadOnlyList<DocumentDualRunWorkerPageResult>> ExecuteAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                jobDirectoryPath))
        {
            throw new ArgumentException(
                "Dual Run worker job directory cannot be empty.",
                nameof(jobDirectoryPath));
        }

        ArgumentNullException.ThrowIfNull(
            request);

        if (request.ExecutionMode !=
            DocumentDualRunExecutionMode.PlanningOnly)
        {
            throw new ArgumentException(
                "PlanningOnly executor requires PlanningOnly execution mode.",
                nameof(request));
        }

        var sourcePath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .SourceSnapshotFileName);

        await using var sourceStream =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize:
                    81920,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        var source =
            new DocumentSource(
                sourceStream,
                request.FileName,
                request.DeclaredMediaType);

        var rasterObservationSource =
            new PdfPigVisualRasterObservationSource();

        var extractionWithRaster =
            await new PdfPigDocumentExtractor()
                .ExtractWithRasterObservationsAsync(
                    source,
                    request.Format,
                    rasterObservationSource,
                    cancellationToken)
                .ConfigureAwait(false);

        if (extractionWithRaster
                .RasterObservationFailure is { } rasterFailure)
        {
            throw new InvalidDataException(
                $"Dual Run worker raster observation failed with " +
                $"'{rasterFailure.ExceptionType}': {rasterFailure.Message}");
        }

        var rasterObservations =
            extractionWithRaster
                .RasterObservations ??
            throw new InvalidDataException(
                "Dual Run worker coordinated extraction returned neither raster observations nor failure evidence.");

        var extraction =
            extractionWithRaster
                .Extraction;

        ValidateAuthoritativeCoverage(
            extraction.Pages.Count,
            request.AuthoritativePages);

        var normalization =
            new DocumentTextNormalizer()
                .Normalize(
                    extraction,
                    cancellationToken);

        var visualEvidence =
            new DefaultVisualStructuralEvidenceEnricher()
                .Enrich(
                    extraction,
                    normalization,
                    rasterObservations,
                    cancellationToken);

        var decisions =
            GuardedDocumentPageExecutionPlanner
                .CreateDefault()
                .Plan(
                    extraction,
                    visualEvidence);

        if (decisions.Count !=
            request.AuthoritativePages.Count)
        {
            throw new InvalidDataException(
                $"Dual Run worker planning returned {decisions.Count} page decision(s) for " +
                $"{request.AuthoritativePages.Count} authoritative page baseline(s).");
        }

        var pages =
            new DocumentDualRunWorkerPageResult[
                decisions.Count];

        for (var index = 0;
             index <
             decisions.Count;
             index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            pages[index] =
                BuildPageResult(
                    request.AuthoritativePages[index],
                    decisions[index]);
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

    #region Methods Validation

    private static void ValidateAuthoritativeCoverage(
        int extractionPageCount,
        IReadOnlyList<DocumentDualRunAuthoritativePageBaseline> authoritativePages)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativePages);

        if (extractionPageCount !=
            authoritativePages.Count)
        {
            throw new InvalidDataException(
                $"Dual Run worker extracted {extractionPageCount} page(s), but request contains " +
                $"{authoritativePages.Count} authoritative page baseline(s).");
        }

        for (var index = 0;
             index <
             authoritativePages.Count;
             index++)
        {
            var expectedPageNumber =
                index +
                1;

            if (authoritativePages[index].PhysicalPageNumber !=
                expectedPageNumber)
            {
                throw new InvalidDataException(
                    $"Dual Run worker authoritative baseline at index {index} refers to " +
                    $"physical page {authoritativePages[index].PhysicalPageNumber}; expected " +
                    $"{expectedPageNumber}.");
            }
        }
    }

    #endregion
}
