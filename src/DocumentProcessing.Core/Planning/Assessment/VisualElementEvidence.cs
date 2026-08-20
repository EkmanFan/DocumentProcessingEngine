namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Evidence classification for one source visual occurrence.
///
/// The source index is zero-based within the source page's visual occurrences.
/// This contract deliberately contains no <see cref="VisualDisposition"/> and
/// no <see cref="PageProcessingRoute"/>. Evidence and policy remain separate.
/// </summary>
public sealed record VisualElementEvidence
{
    public VisualElementEvidence(
        int sourceVisualIndex,
        VisualEvidenceKind kind)
    {
        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        if (!Enum.IsDefined(
                typeof(VisualEvidenceKind),
                kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Visual evidence kind must be a defined value.");
        }

        SourceVisualIndex =
            sourceVisualIndex;

        Kind =
            kind;
    }

    public int SourceVisualIndex { get; }

    public VisualEvidenceKind Kind { get; }
}
