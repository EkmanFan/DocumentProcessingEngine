namespace DocumentProcessing.Core.Extraction;

/// <summary>
/// A native word observation produced by an extractor.
/// Typography is optional evidence because not every document format or
/// extraction backend can provide it.
/// </summary>
public sealed record DocumentWord
{
    public DocumentWord(
        int sourceSequence,
        string text,
        NormalizedRectangle bounds,
        string? fontName = null,
        double? medianPointSize = null)
    {
        if (sourceSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSequence));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Word text cannot be empty.",
                nameof(text));
        }

        ValidatePointSize(
            medianPointSize,
            nameof(medianPointSize));

        SourceSequence = sourceSequence;
        Text = text;
        Bounds = bounds;

        FontName =
            string.IsNullOrWhiteSpace(fontName)
                ? null
                : fontName;

        MedianPointSize =
            medianPointSize;
    }

    public int SourceSequence { get; }

    public string Text { get; }

    public NormalizedRectangle Bounds { get; }

    /// <summary>
    /// Extractor-observed font name, or null when unavailable.
    /// </summary>
    public string? FontName { get; }

    /// <summary>
    /// Median source-letter point size for this word, or null when unavailable.
    /// </summary>
    public double? MedianPointSize { get; }

    private static void ValidatePointSize(
        double? pointSize,
        string parameterName)
    {
        if (pointSize is not null &&
            (!double.IsFinite(pointSize.Value) ||
             pointSize.Value <= 0))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Point size must be finite and greater than zero when supplied.");
        }
    }
}
