namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Complete deterministic visual-observation set for one physical source page.
///
/// This contract is an explicit integration boundary. It does not manufacture
/// missing observations: the guarded planner requires one observation for every
/// source visual occurrence reported by native extraction.
/// </summary>
public sealed record PageVisualEvidenceObservations
{
    public PageVisualEvidenceObservations(
        int physicalPageNumber,
        IEnumerable<VisualEvidenceObservation> visualElements)
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
                    "Visual observations cannot contain null elements.",
                    nameof(visualElements));
            }

            if (!indexes.Add(
                    visualElement.SourceVisualIndex))
            {
                throw new ArgumentException(
                    "Visual observations cannot contain duplicate source visual indexes.",
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

    public IReadOnlyList<VisualEvidenceObservation> VisualElements { get; }
}
