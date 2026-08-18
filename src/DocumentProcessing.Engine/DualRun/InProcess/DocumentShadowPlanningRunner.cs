using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Executes the complete candidate planning chain as non-authoritative shadow
/// work.
///
/// Non-cancellation operational failures are converted into a shadow report and
/// do not authorize or alter runtime execution. Fatal allocation failures are
/// not swallowed.
/// </summary>
public sealed class DocumentShadowPlanningRunner
{
    #region Variables and Constants

    private readonly DocumentShadowPlanningDependencies _dependencies;

    #endregion

    #region ctor

    public DocumentShadowPlanningRunner(
        DocumentShadowPlanningDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    #endregion

    #region Methods Execution

    public ValueTask<DocumentShadowPlanningReport> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeLegacyDecisions,
        string sourceDocumentSha256,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            source,
            format,
            extraction,
            authoritativeLegacyDecisions,
            sourceDocumentSha256,
            coordinatedExtraction:
                null,
            cancellationToken:
                cancellationToken);

    internal async ValueTask<DocumentShadowPlanningReport> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeLegacyDecisions,
        string sourceDocumentSha256,
        DocumentExtractionWithRasterObservationsResult? coordinatedExtraction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            authoritativeLegacyDecisions);

        cancellationToken.ThrowIfCancellationRequested();

        var stage =
            DocumentShadowPlanningFailureStage.Capability;

        DocumentShadowPlanningReport report;

        try
        {
            if (!_dependencies
                    .VisualRasterObservationSource
                    .CanObserve(
                        format))
            {
                report =
                    new DocumentShadowPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentShadowPlanningStatus.UnsupportedFormat,
                        pages:
                            []);

                await DeliverBestEffortAsync(
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);

                return report;
            }

            ValidateAuthoritativeLegacyCoverage(
                extraction,
                authoritativeLegacyDecisions);

            stage =
                DocumentShadowPlanningFailureStage.NativeNormalization;

            var normalization =
                _dependencies
                    .NativeTextNormalizer
                    .Normalize(
                        extraction,
                        cancellationToken);

            stage =
                DocumentShadowPlanningFailureStage.RasterObservation;

            if (coordinatedExtraction?
                    .RasterObservationFailure is { } rasterFailure)
            {
                report =
                    new DocumentShadowPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentShadowPlanningStatus.Failed,
                        pages:
                            [],
                        new DocumentShadowPlanningFailure(
                            DocumentShadowPlanningFailureStage
                                .RasterObservation,
                            rasterFailure.ExceptionType,
                            rasterFailure.Message));
            }
            else
            {
                IReadOnlyList<PageVisualRasterObservations>
                    rasterObservations;

                if (coordinatedExtraction?
                        .RasterObservations is { } precomputed)
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
                    DocumentShadowPlanningFailureStage.StructuralEnrichment;

                var visualObservations =
                    _dependencies
                        .StructuralEvidenceEnricher
                        .Enrich(
                            extraction,
                            normalization,
                            rasterObservations,
                            cancellationToken);

                stage =
                    DocumentShadowPlanningFailureStage.CandidatePlanning;

                var shadowDecisions =
                    _dependencies
                        .GuardedPlanner
                        .Plan(
                            extraction,
                            visualObservations);

                var comparisons =
                    BuildComparisons(
                        extraction,
                        authoritativeLegacyDecisions,
                        shadowDecisions);

                report =
                    new DocumentShadowPlanningReport(
                        sourceDocumentSha256,
                        format,
                        DocumentShadowPlanningStatus.Completed,
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
                new DocumentShadowPlanningReport(
                    sourceDocumentSha256,
                    format,
                    DocumentShadowPlanningStatus.Failed,
                    pages:
                        [],
                    new DocumentShadowPlanningFailure(
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
        DocumentShadowPlanningReport report,
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
            // The observer is diagnostics-only. Failure to export a shadow
            // report cannot change the authoritative legacy execution path.
        }
    }

    #endregion

    #region Methods Validation and Comparison

    private static void ValidateAuthoritativeLegacyCoverage(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeLegacyDecisions)
    {
        if (authoritativeLegacyDecisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Authoritative legacy planning contains " +
                $"{authoritativeLegacyDecisions.Count} page decision(s) for " +
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
                authoritativeLegacyDecisions[index]
                    .PhysicalPageNumber;

            if (actual !=
                expected)
            {
                throw new InvalidDataException(
                    $"Authoritative legacy decision at index {index} refers to " +
                    $"physical page {actual}; expected {expected}.");
            }
        }
    }

    private static IReadOnlyList<DocumentShadowPageComparison> BuildComparisons(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> authoritativeLegacyDecisions,
        IReadOnlyList<GuardedPagePlanningDecision> shadowDecisions)
    {
        if (shadowDecisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Guarded shadow planner returned {shadowDecisions.Count} decision(s) " +
                $"for {extraction.Pages.Count} extracted page(s).");
        }

        var comparisons =
            new DocumentShadowPageComparison[
                extraction.Pages.Count];

        for (var index = 0;
             index <
             comparisons.Length;
             index++)
        {
            comparisons[index] =
                new DocumentShadowPageComparison(
                    authoritativeLegacyDecisions[index],
                    shadowDecisions[index]);
        }

        return comparisons;
    }

    #endregion
}
