using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.DualRun.InProcess;

/// <summary>
/// Executes the complete candidate planning chain as non-authoritative Dual Run
/// work.
///
/// Non-cancellation operational failures are converted into a Dual Run report and
/// do not authorize or alter runtime execution. Fatal allocation failures are
/// not swallowed.
/// </summary>
public sealed class DocumentDualRunPlanningRunner
{
    #region Variables and Constants

    private readonly DocumentDualRunPlanningDependencies _dependencies;

    #endregion

    #region ctor

    public DocumentDualRunPlanningRunner(
        DocumentDualRunPlanningDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    #endregion

    #region Methods Execution

    public ValueTask<DocumentDualRunPlanningReport> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions,
        string sourceDocumentSha256,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            source,
            format,
            extraction,
            authoritativeDecisions,
            sourceDocumentSha256,
            precomputedRasterObservations:
                null,
            rasterObservationFailure:
                null,
            cancellationToken:
                cancellationToken);

    internal ValueTask<DocumentDualRunPlanningReport> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions,
        string sourceDocumentSha256,
        DocumentExtractionWithRasterObservationsResult? coordinatedExtraction,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            source,
            format,
            extraction,
            authoritativeDecisions,
            sourceDocumentSha256,
            precomputedRasterObservations:
                coordinatedExtraction?
                    .RasterObservations,
            rasterObservationFailure:
                coordinatedExtraction?
                    .RasterObservationFailure,
            cancellationToken);

    internal async ValueTask<DocumentDualRunPlanningReport> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions,
        string sourceDocumentSha256,
        IReadOnlyList<PageVisualRasterObservations>?
            precomputedRasterObservations,
        RasterObservationAcquisitionFailure?
            rasterObservationFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            authoritativeDecisions);

        cancellationToken.ThrowIfCancellationRequested();

        var stage =
            DocumentDualRunPlanningFailureStage.Capability;

        DocumentDualRunPlanningReport report;

        try
        {
            if (!_dependencies
                    .VisualRasterObservationSource
                    .CanObserve(
                        format))
            {
                report =
                    new DocumentDualRunPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentDualRunPlanningStatus.UnsupportedFormat,
                        pages:
                            []);

                await DeliverBestEffortAsync(
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);

                return report;
            }

            ValidateAuthoritativeCoverage(
                extraction,
                authoritativeDecisions);

            stage =
                DocumentDualRunPlanningFailureStage.NativeNormalization;

            var normalization =
                _dependencies
                    .NativeTextNormalizer
                    .Normalize(
                        extraction,
                        cancellationToken);

            stage =
                DocumentDualRunPlanningFailureStage.RasterObservation;

            if (rasterObservationFailure is { } rasterFailure)
            {
                report =
                    new DocumentDualRunPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentDualRunPlanningStatus.Failed,
                        pages:
                            [],
                        new DocumentDualRunPlanningFailure(
                            DocumentDualRunPlanningFailureStage
                                .RasterObservation,
                            rasterFailure.ExceptionType,
                            rasterFailure.Message));
            }
            else
            {
                IReadOnlyList<PageVisualRasterObservations>
                    rasterObservations;

                if (precomputedRasterObservations is { } precomputed)
                {
                    rasterObservations =
                        precomputed;
                }
                else
                {
                    rasterObservations =
                        await _dependencies
                            .VisualRasterObservationSource
                            .ObserveAsync(
                                source,
                                format,
                                extraction,
                                cancellationToken)
                            .ConfigureAwait(false);
                }

                stage =
                    DocumentDualRunPlanningFailureStage.StructuralEnrichment;

                var visualObservations =
                    _dependencies
                        .StructuralEvidenceEnricher
                        .Enrich(
                            extraction,
                            normalization,
                            rasterObservations,
                            cancellationToken);

                stage =
                    DocumentDualRunPlanningFailureStage.CandidatePlanning;

                var dualRunDecisions =
                    _dependencies
                        .GuardedPlanner
                        .Plan(
                            extraction,
                            visualObservations);

                var comparisons =
                    BuildComparisons(
                        extraction,
                        authoritativeDecisions,
                        dualRunDecisions);

                report =
                    new DocumentDualRunPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentDualRunPlanningStatus.Completed,
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
                new DocumentDualRunPlanningReport(
                    sourceDocumentSha256,
                    format,
                    DocumentDualRunPlanningStatus.Failed,
                    pages:
                        [],
                    new DocumentDualRunPlanningFailure(
                        stage,
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

    #endregion

    #region Methods Telemetry

    private async ValueTask DeliverBestEffortAsync(
        DocumentDualRunPlanningReport report,
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
            // The observer is diagnostics-only. Failure to export a Dual Run
            // report cannot change the authoritative execution path.
        }
    }

    #endregion

    #region Methods Validation and Comparison

    private static void ValidateAuthoritativeCoverage(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions)
    {
        if (authoritativeDecisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Authoritative planning contains " +
                $"{authoritativeDecisions.Count} page decision(s) for " +
                $"{extraction.Pages.Count} extracted page(s).");
        }

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var expected =
                extraction.Pages[index]
                    .PhysicalPageNumber;

            var actual =
                authoritativeDecisions[index]
                    .PhysicalPageNumber;

            if (actual !=
                expected)
            {
                throw new InvalidDataException(
                    $"Authoritative decision at index {index} refers to " +
                    $"physical page {actual}; expected {expected}.");
            }
        }
    }

    private static IReadOnlyList<DocumentDualRunPageComparison> BuildComparisons(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions,
        IReadOnlyList<GuardedPagePlanningDecision> dualRunDecisions)
    {
        if (dualRunDecisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Guarded Dual Run planner returned {dualRunDecisions.Count} decision(s) " +
                $"for {extraction.Pages.Count} extracted page(s).");
        }

        var comparisons =
            new DocumentDualRunPageComparison[
                extraction.Pages.Count];

        for (var index = 0;
             index <
             comparisons.Length;
             index++)
        {
            comparisons[index] =
                new DocumentDualRunPageComparison(
                    authoritativeDecisions[index],
                    dualRunDecisions[index]);
        }

        return comparisons;
    }

    #endregion
}
