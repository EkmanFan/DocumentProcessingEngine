namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Policy decision for one actual source visual occurrence.
///
/// <see cref="VisualDisposition.NoVisual"/> is intentionally invalid here:
/// this type represents an element that does exist. A page with no visual
/// elements uses an empty collection in <see cref="PageProcessingRequirements"/>.
/// </summary>
public sealed record VisualElementDisposition
{
    public VisualElementDisposition(
        int sourceVisualIndex,
        VisualDisposition disposition)
    {
        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        if (!Enum.IsDefined(
                disposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition,
                "Visual disposition must be a defined value.");
        }

        if (disposition ==
            VisualDisposition.NoVisual)
        {
            throw new ArgumentException(
                "An existing visual element cannot have the NoVisual disposition.",
                nameof(disposition));
        }

        SourceVisualIndex =
            sourceVisualIndex;

        Disposition =
            disposition;
    }

    public int SourceVisualIndex { get; }

    public VisualDisposition Disposition { get; }
}
