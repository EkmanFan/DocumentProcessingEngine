using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Epub.Locations;

/// <summary>
/// EPUB package and spine facts retained without inventing physical pages.
/// </summary>
public sealed record EpubDocumentSourceStructure
    : DocumentSourceStructure
{
    public string PackagePath { get; }

    public IReadOnlyList<EpubSpineItemDescriptor> SpineItems { get; }

    public string? Title { get; }

    public string? Identifier { get; }

    public string? Language { get; }

    public EpubDocumentSourceStructure(
        string packagePath,
        IReadOnlyList<EpubSpineItemDescriptor> spineItems,
        string? title = null,
        string? identifier = null,
        string? language = null)
    {
        if (string.IsNullOrWhiteSpace(
                packagePath))
        {
            throw new ArgumentException(
                "EPUB package path cannot be empty.",
                nameof(packagePath));
        }

        ArgumentNullException.ThrowIfNull(
            spineItems);

        var items =
            spineItems.ToArray();

        if (items.Length ==
            0)
        {
            throw new ArgumentException(
                "EPUB source structure must contain at least one spine item.",
                nameof(spineItems));
        }

        if (items.Any(
                item =>
                    item is null))
        {
            throw new ArgumentException(
                "EPUB source structure cannot contain null spine items.",
                nameof(spineItems));
        }

        for (var index = 0;
             index < items.Length;
             index++)
        {
            if (items[index].SpineIndex !=
                index)
            {
                throw new ArgumentException(
                    "EPUB spine indexes must be contiguous and match reading order.",
                    nameof(spineItems));
            }
        }

        PackagePath =
            packagePath.Trim();

        SpineItems =
            items;

        Title =
            NormalizeOptional(
                title);

        Identifier =
            NormalizeOptional(
                identifier);

        Language =
            NormalizeOptional(
                language);
    }

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();
}
