namespace DocumentProcessing.Core.Normalization;

/// <summary>
/// Deterministic text dehyphenation output.
///
/// The result records only transformations justified by explicit source
/// evidence. It is not a fuzzy correction result and does not claim that the
/// resulting text is authoritative.
/// </summary>
public sealed record TextDehyphenationResult
{
    public TextDehyphenationResult(
        string text,
        int softHyphenRemovalCount,
        int boundaryJoinCount)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (softHyphenRemovalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(softHyphenRemovalCount));
        }

        if (boundaryJoinCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundaryJoinCount));
        }

        Text =
            text;

        SoftHyphenRemovalCount =
            softHyphenRemovalCount;

        BoundaryJoinCount =
            boundaryJoinCount;
    }

    public string Text { get; }

    /// <summary>
    /// Number of discretionary U+00AD characters removed.
    /// </summary>
    public int SoftHyphenRemovalCount { get; }

    /// <summary>
    /// Number of source-fragment boundaries deterministically joined after
    /// dehyphenation evidence was observed.
    /// </summary>
    public int BoundaryJoinCount { get; }

    public bool Changed =>
        SoftHyphenRemovalCount > 0 ||
        BoundaryJoinCount > 0;
}
