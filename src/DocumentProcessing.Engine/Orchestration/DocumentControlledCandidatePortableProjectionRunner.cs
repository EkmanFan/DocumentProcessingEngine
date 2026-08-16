using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// H.4D.4B.1 non-authoritative candidate portable-output/provenance projection.
///
/// This runner performs no extraction, rasterization, layout, OCR,
/// reconciliation or source-visual materialization. It consumes the already
/// executed candidate text pages plus H.4D.3B visual evidence.
///
/// Source-preserved visuals and AnalyzeVisual evidence stay neutral sidecars.
/// No fake LayoutObservation/Figure evidence is manufactured.
/// </summary>
public sealed class DocumentControlledCandidatePortableProjectionRunner
{
    private readonly DocumentControlledCandidatePortableProjectionDependencies
        _dependencies;

    public DocumentControlledCandidatePortableProjectionRunner(
        DocumentControlledCandidatePortableProjectionDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    public async ValueTask<DocumentControlledCandidatePortableProjectionReport>
        RunAsync(
            DocumentIngestionResult authoritativeResult,
            DocumentControlledCandidateTextExecutionReport textExecution,
            DocumentControlledCandidateVisualExecutionReport visualExecution,
            string engineVersion,
            ProcessingComponentIdentity nativeExtractionIdentity,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeResult);

        ArgumentNullException.ThrowIfNull(
            textExecution);

        ArgumentNullException.ThrowIfNull(
            visualExecution);

        ArgumentNullException.ThrowIfNull(
            nativeExtractionIdentity);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentControlledCandidatePortableProjectionReport report;

        try
        {
            ValidateCustody(
                authoritativeResult,
                textExecution,
                visualExecution);

            if (textExecution.Status !=
                    DocumentControlledCandidateTextExecutionStatus.Completed ||
                visualExecution.Status !=
                    DocumentControlledCandidateVisualExecutionStatus.Completed ||
                textExecution.Pages.Any(
                    page =>
                        page.CandidatePage is null))
            {
                report =
                    new DocumentControlledCandidatePortableProjectionReport(
                        authoritativeResult.Source.Sha256,
                        DocumentControlledCandidatePortableProjectionStatus
                            .InputUnavailable);
            }
            else
            {
                report =
                    BuildCompleted(
                        authoritativeResult,
                        textExecution,
                        visualExecution,
                        engineVersion,
                        nativeExtractionIdentity,
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
                new DocumentControlledCandidatePortableProjectionReport(
                    authoritativeResult.Source.Sha256,
                    DocumentControlledCandidatePortableProjectionStatus.Failed,
                    failure:
                        new DocumentControlledCandidatePortableProjectionFailure(
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

    private DocumentControlledCandidatePortableProjectionReport BuildCompleted(
        DocumentIngestionResult authoritativeResult,
        DocumentControlledCandidateTextExecutionReport textExecution,
        DocumentControlledCandidateVisualExecutionReport visualExecution,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        CancellationToken cancellationToken)
    {
        var physicalPageCount =
            authoritativeResult.Source.PhysicalPageCount;

        if (textExecution.Pages.Count !=
                physicalPageCount ||
            visualExecution.Pages.Count !=
                physicalPageCount)
        {
            throw new InvalidDataException(
                "H.4D.4B.1 requires exact candidate text/visual page coverage.");
        }

        var candidatePages =
            new HybridDocumentPage[
                physicalPageCount];

        for (var index = 0;
             index <
             physicalPageCount;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedPhysicalPageNumber =
                index +
                1;

            var textPage =
                textExecution.Pages[
                    index];

            var visualPage =
                visualExecution.Pages[
                    index];

            if (textPage.PhysicalPageNumber !=
                    expectedPhysicalPageNumber ||
                visualPage.PhysicalPageNumber !=
                    expectedPhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"H.4D.4B.1 page identity mismatch at index {index}.");
            }

            candidatePages[index] =
                textPage.CandidatePage ??
                throw new InvalidDataException(
                    $"Physical page {expectedPhysicalPageNumber} has no retained " +
                    "candidate HybridDocumentPage.");

            if (candidatePages[index].PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Retained candidate page identity mismatch at physical page " +
                    $"{expectedPhysicalPageNumber}.");
            }
        }

        var candidateElements =
            candidatePages
                .SelectMany(
                    page =>
                        page.Elements)
                .ToArray();

        var hasLayoutEvidence =
            candidateElements.Any(
                element =>
                    element.LayoutObservation is not null);

        var hasReconciliationEvidence =
            candidateElements.Any(
                element =>
                    element.Reconciliation is not null);

        if (hasLayoutEvidence &&
            _dependencies.RasterizationIdentity is null)
        {
            throw new InvalidOperationException(
                "Candidate layout-backed portable projection requires an explicit " +
                "candidate rasterization identity.");
        }

        if (hasLayoutEvidence &&
            _dependencies.LayoutAnalysisIdentity is null)
        {
            throw new InvalidOperationException(
                "Candidate layout-backed portable projection requires an explicit " +
                "candidate layout-analysis identity.");
        }

        if (hasReconciliationEvidence &&
            _dependencies.ReconciliationIdentity is null)
        {
            throw new InvalidOperationException(
                "Candidate reconciliation-backed portable projection requires an " +
                "explicit candidate reconciliation identity.");
        }

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    candidatePages);

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly,
                    cancellationToken);

        var segmentation =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization,
                    cancellationToken);

        var candidateDocument =
            DocumentIngestionResultBuilder
                .Build(
                    segmentation,
                    new DocumentProcessingProvenanceContext(
                        authoritativeResult.Source,
                        engineVersion,
                        nativeExtractionIdentity,
                        rasterization:
                            hasLayoutEvidence
                                ? _dependencies.RasterizationIdentity
                                : null,
                        layoutAnalysis:
                            hasLayoutEvidence
                                ? _dependencies.LayoutAnalysisIdentity
                                : null,
                        reconciliation:
                            hasReconciliationEvidence
                                ? _dependencies.ReconciliationIdentity
                                : null));

        var sourceVisuals =
            BuildSourceVisualProvenance(
                authoritativeResult.Source.Sha256,
                visualExecution);

        var analyses =
            BuildVisualAnalysisProvenance(
                authoritativeResult.Source.Sha256,
                visualExecution);

        var output =
            new DocumentControlledCandidatePortableOutput(
                candidateDocument,
                sourceVisuals,
                analyses);

        return new DocumentControlledCandidatePortableProjectionReport(
            authoritativeResult.Source.Sha256,
            DocumentControlledCandidatePortableProjectionStatus.Completed,
            output);
    }

    private static IReadOnlyList<DocumentControlledCandidateSourceVisualProvenance>
        BuildSourceVisualProvenance(
            string sourceDocumentSha256,
            DocumentControlledCandidateVisualExecutionReport visualExecution)
    {
        var result =
            new List<DocumentControlledCandidateSourceVisualProvenance>();

        foreach (var page in
                 visualExecution.Pages)
        {
            foreach (var element in
                     page.Elements)
            {
                if (element.Action !=
                    VisualExecutionAction.PreserveMeaningfulVisual)
                {
                    continue;
                }

                var materialization =
                    element.Materialization ??
                    throw new InvalidDataException(
                        $"Physical page {page.PhysicalPageNumber} source visual " +
                        $"{element.SourceVisualIndex} lacks preservation materialization.");

                result.Add(
                    new DocumentControlledCandidateSourceVisualProvenance(
                        sourceDocumentSha256,
                        materialization));
            }
        }

        return result;
    }

    private static IReadOnlyList<DocumentControlledCandidateVisualAnalysisProvenance>
        BuildVisualAnalysisProvenance(
            string sourceDocumentSha256,
            DocumentControlledCandidateVisualExecutionReport visualExecution)
    {
        var result =
            new List<DocumentControlledCandidateVisualAnalysisProvenance>();

        foreach (var page in
                 visualExecution.Pages)
        {
            foreach (var element in
                     page.Elements)
            {
                if (element.Action !=
                    VisualExecutionAction.AnalyzeVisual)
                {
                    continue;
                }

                var raster =
                    page.AnalysisRaster ??
                    throw new InvalidDataException(
                        $"Physical page {page.PhysicalPageNumber} AnalyzeVisual " +
                        "execution lacks page raster evidence.");

                var layout =
                    page.AnalysisLayout ??
                    throw new InvalidDataException(
                        $"Physical page {page.PhysicalPageNumber} AnalyzeVisual " +
                        "execution lacks layout evidence.");

                result.Add(
                    new DocumentControlledCandidateVisualAnalysisProvenance(
                        sourceDocumentSha256,
                        element.SourceVisualIndex,
                        raster,
                        layout));
            }
        }

        return result;
    }

    private static void ValidateCustody(
        DocumentIngestionResult authoritativeResult,
        DocumentControlledCandidateTextExecutionReport textExecution,
        DocumentControlledCandidateVisualExecutionReport visualExecution)
    {
        var sourceSha =
            authoritativeResult.Source.Sha256;

        if (!string.Equals(
                textExecution.SourceDocumentSha256,
                sourceSha,
                StringComparison.Ordinal) ||
            !string.Equals(
                visualExecution.SourceDocumentSha256,
                sourceSha,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "H.4D.4B.1 projection inputs do not share authoritative source custody.");
        }
    }

    private async ValueTask DeliverBestEffortAsync(
        DocumentControlledCandidatePortableProjectionReport report,
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
            // Candidate portable-projection telemetry is non-authoritative.
        }
    }
}
