namespace DocumentProcessing.Core.Layout;

public sealed class LayoutAnalysisResult
{
    public LayoutAnalysisResult(
        string backendId,
        int physicalPageNumber,
        IReadOnlyList<LayoutObservation>? observations = null)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            throw new ArgumentException(
                "Layout backend ID cannot be empty.",
                nameof(backendId));
        }

        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        var resolvedObservations =
            observations ?? Array.Empty<LayoutObservation>();

        if (resolvedObservations.Any(
                observation =>
                    observation.PhysicalPageNumber != physicalPageNumber))
        {
            throw new ArgumentException(
                "All layout observations must belong to the result page.",
                nameof(observations));
        }

        BackendId = backendId.Trim();
        PhysicalPageNumber = physicalPageNumber;
        Observations = resolvedObservations.ToArray();
    }

    public string BackendId { get; }
    public int PhysicalPageNumber { get; }
    public IReadOnlyList<LayoutObservation> Observations { get; }
}
