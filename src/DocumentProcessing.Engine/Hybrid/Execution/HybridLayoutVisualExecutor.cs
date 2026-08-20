using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Executes the visual axis for layout-detected Figure regions after layout
/// evidence has been assessed semantically.
///
/// A raw Figure label never authorizes preservation. Unknown evidence remains
/// deferred, presentation-only evidence is omitted from the semantic hybrid
/// stream, and only PreserveMeaningfulVisual opens a caller-owned destination.
/// </summary>
internal sealed class HybridLayoutVisualExecutor
{
    private readonly DefaultLayoutVisualEvidenceAssessor _assessor =
        new();
    private readonly LayoutVisualRegionPreserver _regionPreserver;

    public HybridLayoutVisualExecutor(
        VisualAssetPreserver visualAssetPreserver)
    {
        ArgumentNullException.ThrowIfNull(
            visualAssetPreserver);

        _regionPreserver =
            new LayoutVisualRegionPreserver(
                visualAssetPreserver);
    }

    public IReadOnlyDictionary<int, LayoutVisualEvidence> Assess(
        LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            layout);

        return _assessor
            .Assess(
                layout)
            .ToDictionary(
                evidence =>
                    evidence.Observation.ObservationSequence);
    }

    public static bool RequiresPreservationDestination(
        IEnumerable<LayoutVisualEvidence> visualEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            visualEvidence);

        return visualEvidence.Any(
            evidence =>
                VisualEvidenceDispositionPolicy.Decide(
                    evidence.Kind) ==
                VisualDisposition.PreserveMeaningfulVisual);
    }

    public async ValueTask<HybridDocumentElement?> ExecuteAsync(
        LayoutVisualEvidence evidence,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);
        ArgumentNullException.ThrowIfNull(
            rasterSession);
        ArgumentNullException.ThrowIfNull(
            pageRaster);

        cancellationToken.ThrowIfCancellationRequested();

        var disposition =
            VisualEvidenceDispositionPolicy.Decide(
                evidence.Kind);

        switch (disposition)
        {
            case VisualDisposition.PresentationOnly:
                return null;

            case VisualDisposition.RequiresVisualAnalysis:
                return HybridDocumentElementFactory
                    .FromDeferred(
                        evidence.Observation);

            case VisualDisposition.PreserveMeaningfulVisual:
                if (openVisualDestinationAsync is null)
                {
                    throw new InvalidOperationException(
                        "Meaningful layout visual preservation requires a " +
                        "caller-owned destination.");
                }

                var destination =
                    await openVisualDestinationAsync(
                            evidence.Observation,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (destination is null)
                {
                    throw new InvalidOperationException(
                        "Visual destination factory returned null.");
                }

                var preserved =
                    await _regionPreserver
                        .PreserveAsync(
                            evidence,
                            rasterSession,
                            pageRaster,
                            sourceDocumentSha256,
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);

                return HybridDocumentElementFactory
                    .FromPreservedVisual(
                        preserved);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(disposition),
                    disposition,
                    "Unsupported visual disposition.");
        }
    }
}
