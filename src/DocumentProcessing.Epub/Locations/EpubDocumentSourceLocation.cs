using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Epub.Locations;

/// <summary>
/// Identifies one native text block within an EPUB spine resource.
/// </summary>
public sealed record EpubDocumentSourceLocation
    : DocumentSourceLocation
{
    public int SpineIndex { get; }

    public string ResourcePath { get; }

    public int BlockIndex { get; }

    public string? FragmentId { get; }

    public EpubDocumentSourceLocation(
        int spineIndex,
        string resourcePath,
        int blockIndex,
        string? fragmentId = null)
    {
        if (spineIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spineIndex));
        }

        if (string.IsNullOrWhiteSpace(
                resourcePath))
        {
            throw new ArgumentException(
                "EPUB resource path cannot be empty.",
                nameof(resourcePath));
        }

        if (blockIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockIndex));
        }

        SpineIndex =
            spineIndex;

        ResourcePath =
            resourcePath.Trim();

        BlockIndex =
            blockIndex;

        FragmentId =
            string.IsNullOrWhiteSpace(
                fragmentId)
                ? null
                : fragmentId.Trim();
    }
}
