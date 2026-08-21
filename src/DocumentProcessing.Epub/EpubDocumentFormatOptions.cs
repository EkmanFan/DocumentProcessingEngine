namespace DocumentProcessing.Epub;

/// <summary>
/// Bounded V1 configuration for EPUB recognition, validation and native
/// acquisition.
/// </summary>
public sealed class EpubDocumentFormatOptions
{
    #region Properties

    public EpubCheckOptions EpubCheck { get; }

    public long MaximumSourceBytes { get; }

    public int MaximumArchiveEntries { get; }

    public long MaximumTotalUncompressedBytes { get; }

    public long MaximumTextResourceBytes { get; }

    public long MaximumVisualResourceBytes { get; }

    #endregion

    #region ctor

    public EpubDocumentFormatOptions(
        EpubCheckOptions? epubCheck = null,
        long maximumSourceBytes = long.MaxValue,
        int maximumArchiveEntries = int.MaxValue,
        long maximumTotalUncompressedBytes = long.MaxValue,
        long maximumTextResourceBytes = long.MaxValue,
        long maximumVisualResourceBytes = long.MaxValue)
    {
        if (maximumSourceBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSourceBytes));
        }

        if (maximumArchiveEntries <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumArchiveEntries));
        }

        if (maximumTotalUncompressedBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTotalUncompressedBytes));
        }

        if (maximumTextResourceBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTextResourceBytes));
        }

        if (maximumVisualResourceBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumVisualResourceBytes));
        }

        EpubCheck =
            epubCheck ??
            EpubCheckOptions.CreateDefault();

        MaximumSourceBytes =
            maximumSourceBytes;

        MaximumArchiveEntries =
            maximumArchiveEntries;

        MaximumTotalUncompressedBytes =
            maximumTotalUncompressedBytes;

        MaximumTextResourceBytes =
            maximumTextResourceBytes;

        MaximumVisualResourceBytes =
            maximumVisualResourceBytes;
    }

    #endregion
}
