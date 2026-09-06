using System.Xml.Linq;
using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Epub.Extraction;

/// <summary>
/// Reads Dublin Core descriptive metadata from an EPUB OPF package document.
/// </summary>
/// <remarks>
/// Acquisition only, over the package document the extractor has already parsed:
/// no second OPF parser is introduced.
///
/// Title, identifier and language also reach
/// <c>EpubDocumentSourceStructure</c>. They are acquired here as well so a
/// consumer reads descriptive metadata the same way for every format instead of
/// downcasting to an EPUB-specific structure.
///
/// EPUB 3 expresses a subtitle through a <c>meta refines</c> refinement on a
/// second <c>dc:title</c>. Resolving that refinement is deliberately not
/// attempted: it is an interpretation step, and this slice acquires facts only.
/// </remarks>
public static class EpubNativeMetadataReader
{
    #region Variables and Constants

    /// <summary>
    /// Source hint for the OPF Dublin Core title.
    /// </summary>
    public const string TitleSourceHint =
        "epub.opf.dc:title";

    /// <summary>
    /// Source hint for an OPF Dublin Core creator.
    /// </summary>
    public const string CreatorSourceHint =
        "epub.opf.dc:creator";

    /// <summary>
    /// Source hint for an OPF Dublin Core contributor.
    /// </summary>
    public const string ContributorSourceHint =
        "epub.opf.dc:contributor";

    /// <summary>
    /// Source hint for the OPF Dublin Core description.
    /// </summary>
    public const string DescriptionSourceHint =
        "epub.opf.dc:description";

    /// <summary>
    /// Source hint for the OPF Dublin Core publisher.
    /// </summary>
    public const string PublisherSourceHint =
        "epub.opf.dc:publisher";

    /// <summary>
    /// Source hint for the OPF Dublin Core language.
    /// </summary>
    public const string LanguageSourceHint =
        "epub.opf.dc:language";

    /// <summary>
    /// Source hint for an OPF Dublin Core date.
    /// </summary>
    public const string DateSourceHint =
        "epub.opf.dc:date";

    #endregion

    #region Methods

    /// <summary>
    /// Reads native metadata from an OPF package document.
    /// </summary>
    /// <param name="package">Parsed OPF package document.</param>
    /// <returns>Acquired native metadata.</returns>
    public static NativeDocumentMetadata Read(
        XDocument package)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        var contributors =
            ReadAll(
                    package,
                    "creator",
                    CreatorSourceHint)
                .Concat(
                    ReadAll(
                        package,
                        "contributor",
                        ContributorSourceHint))
                .ToArray();

        return new NativeDocumentMetadata(
            ReadFirst(
                package,
                "title",
                TitleSourceHint),
            subtitle:
                null,
            ReadFirst(
                package,
                "description",
                DescriptionSourceHint),
            contributors,
            ReadFirst(
                package,
                "publisher",
                PublisherSourceHint),
            ReadFirst(
                package,
                "language",
                LanguageSourceHint),
            ReadAll(
                    package,
                    "date",
                    DateSourceHint)
                .ToArray());
    }

    #endregion

    #region Methods Reading

    private static NativeDocumentMetadataValue? ReadFirst(
        XDocument package,
        string localName,
        string sourceHint) =>
        NativeDocumentMetadataValue.TryCreate(
            Elements(
                    package,
                    localName)
                .Select(
                    element =>
                        element.Value)
                .FirstOrDefault(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value)),
            sourceHint);

    private static IEnumerable<NativeDocumentMetadataValue> ReadAll(
        XDocument package,
        string localName,
        string sourceHint) =>
        Elements(
                package,
                localName)
            .Select(
                element =>
                    NativeDocumentMetadataValue.TryCreate(
                        element.Value,
                        sourceHint))
            .Where(
                value =>
                    value is not null)
            .Select(
                value =>
                    value!);

    /// <summary>
    /// Matches on local name so the reader is independent of how the package
    /// declares its Dublin Core namespace prefix.
    /// </summary>
    private static IEnumerable<XElement> Elements(
        XDocument package,
        string localName) =>
        package
            .Descendants()
            .Where(
                element =>
                    string.Equals(
                        element.Name.LocalName,
                        localName,
                        StringComparison.OrdinalIgnoreCase));

    #endregion
}
