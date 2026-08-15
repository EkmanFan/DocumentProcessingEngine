namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Immutable page-level evidence snapshot for future two-axis processing policy.
///
/// The text-authority axis and visual-evidence axis are independent. This type
/// deliberately contains no route and no visual disposition.
/// </summary>
public sealed record PageProcessingEvidence
{
    public PageProcessingEvidence(
        int physicalPageNumber,
        TextAuthority textAuthority,
        IEnumerable<VisualElementEvidence> visualElements)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (!Enum.IsDefined(
                typeof(TextAuthority),
                textAuthority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textAuthority),
                textAuthority,
                "Text authority must be a defined value.");
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
                    "Visual evidence cannot contain null elements.",
                    nameof(visualElements));
            }

            if (!indexes.Add(
                    visualElement.SourceVisualIndex))
            {
                throw new ArgumentException(
                    "Visual evidence cannot contain duplicate source visual indexes.",
                    nameof(visualElements));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        TextAuthority =
            textAuthority;

        VisualElements =
            Array.AsReadOnly(
                materialized);
    }

    public int PhysicalPageNumber { get; }

    public TextAuthority TextAuthority { get; }

    /// <summary>
    /// Zero elements means that no embedded visual occurrence requires
    /// classification. A synthetic "no visual" evidence element is not needed.
    /// </summary>
    public IReadOnlyList<VisualElementEvidence> VisualElements { get; }
}
