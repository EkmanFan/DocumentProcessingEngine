namespace DocumentProcessing.Core.Extraction;

/// <summary>
/// A text block produced by layout analysis.
/// SourceSequence preserves the page segmenter's original sequence.
/// ReadingOrder is a distinct derived order and may differ from SourceSequence.
/// Typography is optional extractor evidence and does not determine reading
/// order by itself.
/// </summary>
public sealed record DocumentTextBlock
{
    public DocumentTextBlock(
        int sourceSequence,
        int? readingOrder,
        string text,
        NormalizedRectangle bounds,
        IReadOnlyList<DocumentWord>? words = null,
        string? dominantFontName = null,
        double? medianPointSize = null,
        int lineCount = 0)
    {
        if (sourceSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSequence));
        }

        if (readingOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingOrder));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Block text cannot be empty.",
                nameof(text));
        }

        if (medianPointSize is not null &&
            (!double.IsFinite(medianPointSize.Value) ||
             medianPointSize.Value <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(medianPointSize),
                "Point size must be finite and greater than zero when supplied.");
        }

        if (lineCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineCount));
        }

        SourceSequence = sourceSequence;
        ReadingOrder = readingOrder;
        Text = text;
        Bounds =
            bounds ??
            throw new ArgumentNullException(
                nameof(bounds));

        Words =
            words ??
            Array.Empty<DocumentWord>();

        DominantFontName =
            string.IsNullOrWhiteSpace(
                dominantFontName)
                ? null
                : dominantFontName;

        MedianPointSize =
            medianPointSize;

        LineCount =
            lineCount;
    }

    public int SourceSequence { get; }

    /// <summary>
    /// Zero-based reading-order position, or null when no reading order is available.
    /// </summary>
    public int? ReadingOrder { get; }

    public string Text { get; }

    public NormalizedRectangle Bounds { get; }

    public IReadOnlyList<DocumentWord> Words { get; }

    /// <summary>
    /// Dominant extractor-observed font name across source letters.
    /// </summary>
    public string? DominantFontName { get; }

    /// <summary>
    /// Median source-letter point size for the block.
    /// </summary>
    public double? MedianPointSize { get; }

    /// <summary>
    /// Number of source layout lines contributing to the block.
    /// Zero means that the producer did not supply line-count evidence.
    /// </summary>
    public int LineCount { get; }

    /// <summary>
    /// Derived source-word count; no duplicate count is stored.
    /// </summary>
    public int WordCount =>
        Words.Count;
}
