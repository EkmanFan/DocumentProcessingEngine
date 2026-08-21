using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Epub.Locations;

/// <summary>
/// Identifies one selected image usage within EPUB reading content without
/// inventing a physical page.
/// </summary>
public sealed record EpubVisualSourceLocation
    : DocumentSourceLocation
{
    public int SpineIndex { get; }

    public string ContentResourcePath { get; }

    public string ImageResourcePath { get; }

    public int OccurrenceIndex { get; }

    public string? FragmentId { get; }

    public bool IsAuxiliary { get; }

    public EpubVisualSourceLocation(
        int spineIndex,
        string contentResourcePath,
        string imageResourcePath,
        int occurrenceIndex,
        string? fragmentId,
        bool isAuxiliary)
    {
        if (spineIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spineIndex));
        }

        if (occurrenceIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurrenceIndex));
        }

        ContentResourcePath =
            NormalizeRequired(
                contentResourcePath,
                nameof(contentResourcePath));

        ImageResourcePath =
            NormalizeRequired(
                imageResourcePath,
                nameof(imageResourcePath));

        SpineIndex =
            spineIndex;

        OccurrenceIndex =
            occurrenceIndex;

        FragmentId =
            string.IsNullOrWhiteSpace(
                fragmentId)
                ? null
                : fragmentId.Trim();

        IsAuxiliary =
            isAuxiliary;
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "EPUB visual location value cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }
}
