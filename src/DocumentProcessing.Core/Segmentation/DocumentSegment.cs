using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Core.Segmentation;

/// <summary>
/// Stable document-local structural unit derived from normalized source blocks.
/// Retrieval chunking is deliberately outside this model.
/// </summary>
public sealed class DocumentSegment
{
    public DocumentSegment(
        string id,
        int ordinal,
        int firstPhysicalPageNumber,
        int lastPhysicalPageNumber,
        string text,
        IReadOnlyList<NormalizedDocumentTextBlock> sourceBlocks,
        string? headingText = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Segment identifier cannot be empty.",
                nameof(id));
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal));
        }

        if (firstPhysicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstPhysicalPageNumber));
        }

        if (lastPhysicalPageNumber <
            firstPhysicalPageNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastPhysicalPageNumber));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Segment text cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(
            sourceBlocks);

        if (sourceBlocks.Count == 0)
        {
            throw new ArgumentException(
                "A segment must retain at least one source block.",
                nameof(sourceBlocks));
        }

        Id = id.Trim();
        Ordinal = ordinal;
        FirstPhysicalPageNumber =
            firstPhysicalPageNumber;
        LastPhysicalPageNumber =
            lastPhysicalPageNumber;
        Text = text;
        SourceBlocks = sourceBlocks;
        HeadingText =
            string.IsNullOrWhiteSpace(headingText)
                ? null
                : headingText.Trim();
    }

    /// <summary>
    /// Deterministic identifier scoped to this document segmentation result.
    /// It is not a globally unique document identity.
    /// </summary>
    public string Id { get; }

    public int Ordinal { get; }

    public int FirstPhysicalPageNumber { get; }

    public int LastPhysicalPageNumber { get; }

    public string? HeadingText { get; }

    public string Text { get; }

    public IReadOnlyList<NormalizedDocumentTextBlock> SourceBlocks { get; }
}
