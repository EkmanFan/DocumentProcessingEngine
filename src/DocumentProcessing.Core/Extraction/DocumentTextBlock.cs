namespace DocumentProcessing.Core.Extraction;

/// <summary>
/// A text block produced by layout analysis.
/// SourceSequence preserves the page segmenter's original sequence.
/// ReadingOrder is a distinct derived order and may differ from SourceSequence.
/// </summary>
public sealed record DocumentTextBlock
{
    public DocumentTextBlock(
        int sourceSequence,
        int? readingOrder,
        string text,
        NormalizedRectangle bounds,
        IReadOnlyList<DocumentWord>? words = null)
    {
        if (sourceSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSequence));
        }

        if (readingOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(readingOrder));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Block text cannot be empty.", nameof(text));
        }

        SourceSequence = sourceSequence;
        ReadingOrder = readingOrder;
        Text = text;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        Words = words ?? [];
    }

    public int SourceSequence { get; }

    /// <summary>
    /// Zero-based reading-order position, or null when no reading order is available.
    /// </summary>
    public int? ReadingOrder { get; }

    public string Text { get; }
    public NormalizedRectangle Bounds { get; }
    public IReadOnlyList<DocumentWord> Words { get; }
}
