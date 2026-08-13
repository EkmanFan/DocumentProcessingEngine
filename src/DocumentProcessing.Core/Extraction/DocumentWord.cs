namespace DocumentProcessing.Core.Extraction;

public sealed record DocumentWord
{
    public DocumentWord(
        int sourceSequence,
        string text,
        NormalizedRectangle bounds)
    {
        if (sourceSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSequence));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Word text cannot be empty.", nameof(text));
        }

        SourceSequence = sourceSequence;
        Text = text;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
    }

    public int SourceSequence { get; }
    public string Text { get; }
    public NormalizedRectangle Bounds { get; }
}
