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

    #endregion

    #region ctor

    public EpubDocumentFormatOptions(
        EpubCheckOptions? epubCheck = null,
        long maximumSourceBytes = 256L * 1024 * 1024,
        int maximumArchiveEntries = 10000,
        long maximumTotalUncompressedBytes = 1024L * 1024 * 1024,
        long maximumTextResourceBytes = 16L * 1024 * 1024)
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
    }

    #endregion
}
