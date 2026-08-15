namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Complete low-level raster-observation set for one physical source page.
/// </summary>
public sealed record PageVisualRasterObservations
{
    public PageVisualRasterObservations(
        int physicalPageNumber,
        IEnumerable<VisualRasterObservation> visualElements)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        ArgumentNullException.ThrowIfNull(
            visualElements);

        var materialized =
            visualElements.ToArray();

        var indexes =
            new HashSet<int>();

        foreach (var visualElement in
                 materialized)
        {
            if (visualElement is null)
            {
                throw new ArgumentException(
                    "Visual raster observations cannot contain null elements.",
                    nameof(visualElements));
            }

            if (!indexes.Add(
                    visualElement.SourceVisualIndex))
            {
                throw new ArgumentException(
                    "Visual raster observations cannot contain duplicate source visual indexes.",
                    nameof(visualElements));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        VisualElements =
            Array.AsReadOnly(
                materialized);
    }

    public int PhysicalPageNumber { get; }

    public IReadOnlyList<VisualRasterObservation> VisualElements { get; }
}
