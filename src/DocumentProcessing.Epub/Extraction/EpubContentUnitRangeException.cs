namespace DocumentProcessing.Epub.Extraction;

/// <summary>
/// Reports a consumer-safe mismatch between an approved content-unit range
/// and the immutable EPUB spine inspected at execution time.
/// </summary>
internal sealed class EpubContentUnitRangeException
    : Exception
{
    /// <summary>Creates one EPUB content-unit range validation failure.</summary>
    public EpubContentUnitRangeException(
        string message)
        : base(
            message)
    {
    }
}
