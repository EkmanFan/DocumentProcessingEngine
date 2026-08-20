using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Runs the deterministic source-visual evidence chain for authoritative
/// planning. It does not execute layout, OCR, visual preservation, or page
/// assembly.
///
/// The caller owns source-position custody and failure semantics.
/// </summary>
internal sealed class DocumentAuthoritativeVisualPlanningRunner
{
    #region Variables and Constants

    private readonly DocumentAuthoritativeVisualPlanningDependencies _dependencies;

    #endregion


    #region ctor

    public DocumentAuthoritativeVisualPlanningRunner(
        DocumentAuthoritativeVisualPlanningDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    #endregion


    #region Methods

    public async ValueTask<IReadOnlyList<GuardedPagePlanningDecision>> RunAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_dependencies
                .VisualRasterObservationSource
                .CanObserve(
                    format))
        {
            throw new NotSupportedException(
                $"The configured authoritative visual observation source " +
                $"cannot process format '{format}'.");
        }

        var normalization =
            _dependencies
                .NativeTextNormalizer
                .Normalize(
                    extraction,
                    cancellationToken);

        var rasterObservations =
            await _dependencies
                .VisualRasterObservationSource
                .ObserveAsync(
                    source,
                    format,
                    extraction,
                    cancellationToken)
                .ConfigureAwait(false);

        var visualObservations =
            _dependencies
                .StructuralEvidenceEnricher
                .Enrich(
                    extraction,
                    normalization,
                    rasterObservations,
                    cancellationToken);

        return _dependencies
            .GuardedPlanner
            .Plan(
                extraction,
                visualObservations);
    }

    #endregion
}
