using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.DualRunWorker;

/// <summary>
/// Shared deterministic worker-local planning pipeline used by PlanningOnly and
/// Full execution.
///
/// The worker independently re-extracts source.bin and obtains PdfPig raster
/// observations through the coordinated single-pass extraction seam.
/// </summary>
internal sealed class DocumentDualRunWorkerPlanningPipeline
{
    #region Methods Planning

    public async ValueTask<DocumentDualRunWorkerPlanningContext> ExecuteAsync(
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

        return new DocumentDualRunWorkerPlanningContext(
            extraction,
            decisions);
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

internal sealed record DocumentDualRunWorkerPlanningContext(
    DocumentExtractionResult Extraction,
    IReadOnlyList<GuardedPagePlanningDecision> Decisions);
