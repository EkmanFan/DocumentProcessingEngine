using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Authoritative source-visual planning output together with the exact
/// page-normalized raster observations used to produce it.
/// </summary>
internal sealed record DocumentAuthoritativeVisualPlanningResult
{
    public DocumentAuthoritativeVisualPlanningResult(
        IEnumerable<GuardedPagePlanningDecision> decisions,
        IEnumerable<PageVisualRasterObservations> rasterObservations)
    {
        ArgumentNullException.ThrowIfNull(
            decisions);

        ArgumentNullException.ThrowIfNull(
            rasterObservations);

        var materializedDecisions =
            decisions.ToArray();

        var materializedRasterObservations =
            rasterObservations.ToArray();

        if (materializedDecisions.Length !=
            materializedRasterObservations.Length)
        {
            throw new InvalidDataException(
                "Authoritative visual planning decisions and raster " +
                "observations must contain the same number of pages.");
        }

        for (var index = 0;
             index <
             materializedDecisions.Length;
             index++)
        {
            if (materializedDecisions[index].PhysicalPageNumber !=
                materializedRasterObservations[index].PhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Authoritative visual planning page {index} does not " +
                    "align with its raster observations.");
            }
        }

        Decisions =
            Array.AsReadOnly(
                materializedDecisions);

        RasterObservations =
            Array.AsReadOnly(
                materializedRasterObservations);
    }

    public IReadOnlyList<GuardedPagePlanningDecision> Decisions { get; }

    public IReadOnlyList<PageVisualRasterObservations> RasterObservations { get; }
}
