using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Epub.Locations;

namespace DocumentProcessing.Epub.Extraction;

/// <summary>
/// Acquires package, spine and XHTML facts from an EPUB already accepted by
/// EPUBCheck. Engine policy is intentionally absent from this adapter.
/// </summary>
internal sealed class EpubPackageExtractor
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        NativeExtractionIdentity =
            new(
                "epub-xhtml",
                "epub-xhtml-native-v1+epubcheck-5.3.0");

    private static readonly IReadOnlyDictionary<string,
        StructuredNativeTextBlockKind> BlockKinds =
        new Dictionary<string, StructuredNativeTextBlockKind>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["p"] =
                StructuredNativeTextBlockKind.Text,
            ["li"] =
                StructuredNativeTextBlockKind.Text,
            ["blockquote"] =
                StructuredNativeTextBlockKind.Text,
            ["pre"] =
                StructuredNativeTextBlockKind.Text,
            ["dt"] =
                StructuredNativeTextBlockKind.Text,
            ["dd"] =
                StructuredNativeTextBlockKind.Text,
            ["h1"] =
                StructuredNativeTextBlockKind.Heading,
            ["h2"] =
                StructuredNativeTextBlockKind.Heading,
            ["h3"] =
                StructuredNativeTextBlockKind.Heading,
            ["h4"] =
                StructuredNativeTextBlockKind.Heading,
            ["h5"] =
                StructuredNativeTextBlockKind.Heading,
            ["h6"] =
                StructuredNativeTextBlockKind.Heading,
            ["figcaption"] =
                StructuredNativeTextBlockKind.Caption
        };

    #endregion

    #region Methods Extraction

    public StructuredNativeDocumentEvidence Extract(
        Stream source,
        EpubDocumentFormatOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            options);

        if (!source.CanSeek)
        {
            throw new InvalidOperationException(
                "EPUB extraction requires a prepared seekable source.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        source.Position =
            0;

        using var archive =
            new ZipArchive(
                source,
                ZipArchiveMode.Read,
                leaveOpen:
                    true);

        var entries =
            ValidateAndIndexEntries(
                archive,
                options,
                cancellationToken);

        var container =
            LoadXml(
                GetRequiredEntry(
                    entries,
                    "META-INF/container.xml"),
                options.MaximumTextResourceBytes);

        var packagePath =
            container
                .Descendants()
                .Where(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "rootfile",
                            StringComparison.Ordinal))
                .Select(
                    element =>
                        element.Attribute(
                                "full-path")?
                            .Value)
                .FirstOrDefault(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value)) ??
            throw new InvalidDataException(
                "EPUB container does not identify a package document.");

        packagePath =
            EpubArchivePath.NormalizeEntryPath(
                Uri.UnescapeDataString(
                    packagePath));

        var package =
            LoadXml(
                GetRequiredEntry(
                    entries,
                    packagePath),
                options.MaximumTextResourceBytes);

        var manifest =
            ReadManifest(
                package,
                packagePath);

        var spineItems =
            ReadSpine(
                package,
                manifest);

        var contentUnits =
            new List<StructuredNativeContentUnit>(
                spineItems.Count);

        foreach (var spineItem in
                 spineItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            contentUnits.Add(
                ExtractContentUnit(
                    entries,
                    spineItem,
                    options.MaximumTextResourceBytes));
        }

        var structure =
            new EpubDocumentSourceStructure(
                packagePath,
                spineItems,
                ReadMetadata(
                    package,
                    "title"),
                ReadMetadata(
                    package,
                    "identifier"),
                ReadMetadata(
                    package,
                    "language"));

        source.Position =
            0;

        return new StructuredNativeDocumentEvidence(
            structure,
            contentUnits,
            NativeExtractionIdentity);
    }

    #endregion

    #region Methods Archive Validation

    private static IReadOnlyDictionary<string, ZipArchiveEntry>
        ValidateAndIndexEntries(
            ZipArchive archive,
            EpubDocumentFormatOptions options,
            CancellationToken cancellationToken)
    {
        if (archive.Entries.Count >
            options.MaximumArchiveEntries)
        {
            throw new InvalidDataException(
                "EPUB archive exceeds the configured entry-count boundary.");
        }

        var entries =
            new Dictionary<string, ZipArchiveEntry>(
                StringComparer.Ordinal);

        long totalLength =
            0;

        foreach (var entry in
                 archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            totalLength =
                checked(
                    totalLength +
                    entry.Length);

            if (totalLength >
                options.MaximumTotalUncompressedBytes)
            {
                throw new InvalidDataException(
                    "EPUB archive exceeds the configured uncompressed-size boundary.");
            }

            if (entry.FullName.EndsWith(
                    "/",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var normalized =
                EpubArchivePath.NormalizeEntryPath(
                    entry.FullName);

            if (!string.Equals(
                    normalized,
                    entry.FullName,
                    StringComparison.Ordinal) ||
                !entries.TryAdd(
                    normalized,
                    entry))
            {
                throw new InvalidDataException(
                    "EPUB archive contains an unsafe or duplicate resource path.");
            }
        }

        return entries;
    }

    private static ZipArchiveEntry GetRequiredEntry(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string path) =>
        entries.TryGetValue(
            path,
            out var entry)
            ? entry
            : throw new InvalidDataException(
                "EPUB package references a missing required resource.");

    #endregion

    #region Methods Package

    private static IReadOnlyDictionary<string, ManifestItem>
        ReadManifest(
            XDocument package,
            string packagePath)
    {
        var manifestElement =
            package
                .Descendants()
                .FirstOrDefault(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "manifest",
                            StringComparison.Ordinal)) ??
            throw new InvalidDataException(
                "EPUB package does not contain a manifest.");

        var manifest =
            new Dictionary<string, ManifestItem>(
                StringComparer.Ordinal);

        foreach (var item in
                 manifestElement.Elements()
                     .Where(
                         element =>
                             string.Equals(
                                 element.Name.LocalName,
                                 "item",
                                 StringComparison.Ordinal)))
        {
            var id =
                RequiredAttribute(
                    item,
                    "id");

            var resourcePath =
                EpubArchivePath.Resolve(
                    packagePath,
                    RequiredAttribute(
                        item,
                        "href"));

            var mediaType =
                RequiredAttribute(
                    item,
                    "media-type");

            if (!manifest.TryAdd(
                    id,
                    new ManifestItem(
                        id,
                        resourcePath,
                        mediaType)))
            {
                throw new InvalidDataException(
                    "EPUB manifest contains duplicate item IDs.");
            }
        }

        return manifest;
    }

    private static IReadOnlyList<EpubSpineItemDescriptor> ReadSpine(
        XDocument package,
        IReadOnlyDictionary<string, ManifestItem> manifest)
    {
        var spineElement =
            package
                .Descendants()
                .FirstOrDefault(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "spine",
                            StringComparison.Ordinal)) ??
            throw new InvalidDataException(
                "EPUB package does not contain a spine.");

        var items =
            new List<EpubSpineItemDescriptor>();

        foreach (var itemRef in
                 spineElement.Elements()
                     .Where(
                         element =>
                             string.Equals(
                                 element.Name.LocalName,
                                 "itemref",
                                 StringComparison.Ordinal)))
        {
            var idRef =
                RequiredAttribute(
                    itemRef,
                    "idref");

            if (!manifest.TryGetValue(
                    idRef,
                    out var manifestItem))
            {
                throw new InvalidDataException(
                    "EPUB spine references an unknown manifest item.");
            }

            items.Add(
                new EpubSpineItemDescriptor(
                    items.Count,
                    idRef,
                    manifestItem.ResourcePath,
                    manifestItem.MediaType,
                    !string.Equals(
                        itemRef.Attribute(
                                "linear")?
                            .Value,
                        "no",
                        StringComparison.OrdinalIgnoreCase)));
        }

        if (items.Count ==
            0)
        {
            throw new InvalidDataException(
                "EPUB package spine cannot be empty.");
        }

        return items;
    }

    private static string? ReadMetadata(
        XDocument package,
        string localName) =>
        package
            .Descendants()
            .FirstOrDefault(
                element =>
                    string.Equals(
                        element.Name.LocalName,
                        localName,
                        StringComparison.OrdinalIgnoreCase))?
            .Value;

    private static string RequiredAttribute(
        XElement element,
        string localName) =>
        element.Attributes()
            .FirstOrDefault(
                attribute =>
                    string.Equals(
                        attribute.Name.LocalName,
                        localName,
                        StringComparison.Ordinal))?
            .Value ??
        throw new InvalidDataException(
            "EPUB package element is missing a required attribute.");

    #endregion

    #region Methods XHTML

    private static StructuredNativeContentUnit ExtractContentUnit(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        EpubSpineItemDescriptor spineItem,
        long maximumTextResourceBytes)
    {
        if (!string.Equals(
                spineItem.MediaType,
                "application/xhtml+xml",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StructuredNativeContentUnit(
                spineItem.ResourcePath,
                []);
        }

        var document =
            LoadXml(
                GetRequiredEntry(
                    entries,
                    spineItem.ResourcePath),
                maximumTextResourceBytes);

        var body =
            document
                .Descendants()
                .FirstOrDefault(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "body",
                            StringComparison.OrdinalIgnoreCase));

        if (body is null)
        {
            throw new InvalidDataException(
                "EPUB XHTML spine resource does not contain a body.");
        }

        var blocks =
            new List<StructuredNativeTextBlock>();

        foreach (var child in
                 body.Elements())
        {
            VisitElement(
                child,
                spineItem,
                blocks);
        }

        if (blocks.Count ==
            0 &&
            !string.IsNullOrWhiteSpace(
                body.Value))
        {
            blocks.Add(
                new StructuredNativeTextBlock(
                    StructuredNativeTextBlockKind.Text,
                    new EpubDocumentSourceLocation(
                        spineItem.SpineIndex,
                        spineItem.ResourcePath,
                        blockIndex:
                            0,
                        fragmentId:
                            FragmentId(
                            body)),
                    body.Value));
        }

        return new StructuredNativeContentUnit(
            spineItem.ResourcePath,
            blocks);
    }

    private static void VisitElement(
        XElement element,
        EpubSpineItemDescriptor spineItem,
        ICollection<StructuredNativeTextBlock> blocks)
    {
        if (BlockKinds.TryGetValue(
                element.Name.LocalName,
                out var kind))
        {
            if (!string.IsNullOrWhiteSpace(
                    element.Value))
            {
                blocks.Add(
                    new StructuredNativeTextBlock(
                        kind,
                        new EpubDocumentSourceLocation(
                            spineItem.SpineIndex,
                            spineItem.ResourcePath,
                            blocks.Count,
                            FragmentId(
                                element)),
                        element.Value));
            }

            return;
        }

        if (element.Name.LocalName is
            "script" or
            "style" or
            "svg")
        {
            return;
        }

        foreach (var child in
                 element.Elements())
        {
            VisitElement(
                child,
                spineItem,
                blocks);
        }
    }

    private static string? FragmentId(
        XElement element) =>
        element.Attributes()
            .FirstOrDefault(
                attribute =>
                    string.Equals(
                        attribute.Name.LocalName,
                        "id",
                        StringComparison.Ordinal))?
            .Value;

    #endregion

    #region Methods XML

    private static XDocument LoadXml(
        ZipArchiveEntry entry,
        long maximumBytes)
    {
        if (entry.Length <=
                0 ||
            entry.Length >
                maximumBytes)
        {
            throw new InvalidDataException(
                "EPUB XML resource exceeds the configured V1 boundary.");
        }

        using var stream =
            entry.Open();

        using var reader =
            XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing =
                        DtdProcessing.Prohibit,
                    XmlResolver =
                        null,
                    MaxCharactersInDocument =
                        maximumBytes,
                    IgnoreComments =
                        true
                });

        return XDocument.Load(
            reader,
            LoadOptions.None);
    }

    #endregion

    #region Types

    private sealed record ManifestItem(
        string Id,
        string ResourcePath,
        string MediaType);

    #endregion
}
