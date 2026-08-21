namespace DocumentProcessing.Epub.Locations;

/// <summary>
/// One package spine item in authoritative EPUB reading order.
/// </summary>
public sealed record EpubSpineItemDescriptor
{
    public int SpineIndex { get; }

    public string IdRef { get; }

    public string ResourcePath { get; }

    public string MediaType { get; }

    public bool IsLinear { get; }

    public EpubSpineItemDescriptor(
        int spineIndex,
        string idRef,
        string resourcePath,
        string mediaType,
        bool isLinear)
    {
        if (spineIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spineIndex));
        }

        IdRef =
            NormalizeRequired(
                idRef,
                nameof(idRef));

        ResourcePath =
            NormalizeRequired(
                resourcePath,
                nameof(resourcePath));

        MediaType =
            NormalizeRequired(
                mediaType,
                nameof(mediaType));

        SpineIndex =
            spineIndex;

        IsLinear =
            isLinear;
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "EPUB spine value cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }
}
