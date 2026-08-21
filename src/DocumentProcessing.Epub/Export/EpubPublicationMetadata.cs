namespace DocumentProcessing.Epub.Export;

/// <summary>
/// Human-facing metadata used when a processing result is exported as EPUB.
/// </summary>
public sealed record EpubPublicationMetadata
{
    public string Title { get; }

    public string Language { get; }

    public string? Creator { get; }

    public string? Identifier { get; }

    public DateTimeOffset? ModifiedAtUtc { get; }

    public EpubPublicationMetadata(
        string title,
        string language,
        string? creator = null,
        string? identifier = null,
        DateTimeOffset? modifiedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(
                title))
        {
            throw new ArgumentException(
                "EPUB title cannot be empty.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(
                language))
        {
            throw new ArgumentException(
                "EPUB language cannot be empty.",
                nameof(language));
        }

        Title =
            title.Trim();

        Language =
            language.Trim();

        Creator =
            NormalizeOptional(
                creator);

        Identifier =
            NormalizeOptional(
                identifier);

        ModifiedAtUtc =
            modifiedAtUtc?.ToUniversalTime();
    }

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();
}
