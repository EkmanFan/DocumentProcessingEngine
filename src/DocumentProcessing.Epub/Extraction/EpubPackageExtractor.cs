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

        var manifestByResourcePath =
            manifest.Values
                .ToDictionary(
                    item =>
                        item.ResourcePath,
                    StringComparer.Ordinal);

        var excludedImageResourcePaths =
            ReadCoverImageResourcePaths(
                package,
                manifest);

        var navigationContentResourcePaths =
            ReadNavigationContentResourcePaths(
                manifest);

        var coverContentResourcePaths =
            ReadCoverContentResourcePaths(
                package,
                packagePath);

        var spineItems =
            ReadSpine(
                package,
                manifest);

        var contentUnits =
            new List<StructuredNativeContentUnit>(
                spineItems.Count);

        var visualUsages =
            new List<VisualUsage>();

        foreach (var spineItem in
                 spineItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extracted =
                ExtractSpineResource(
                    entries,
                    spineItem,
                    manifestByResourcePath,
                    excludedImageResourcePaths,
                    navigationContentResourcePaths,
                    coverContentResourcePaths,
                    options.MaximumTextResourceBytes);

            contentUnits.Add(
                extracted.ContentUnit);

            visualUsages.AddRange(
                extracted.VisualUsages);
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
            NativeExtractionIdentity,
            BuildVisualCandidates(
                visualUsages));
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

            var properties =
                ReadTokenAttribute(
                    item,
                    "properties");

            if (!manifest.TryAdd(
                    id,
                    new ManifestItem(
                        id,
                        resourcePath,
                        mediaType,
                        properties)))
            {
                throw new InvalidDataException(
                    "EPUB manifest contains duplicate item IDs.");
            }
        }

        return manifest;
    }

    private static IReadOnlySet<string> ReadCoverImageResourcePaths(
        XDocument package,
        IReadOnlyDictionary<string, ManifestItem> manifest)
    {
        var coverIds =
            new HashSet<string>(
                manifest.Values
                    .Where(
                        item =>
                            item.Properties.Contains(
                                "cover-image"))
                    .Select(
                        item =>
                            item.Id),
                StringComparer.Ordinal);

        foreach (var metadataCoverId in
                 package.Descendants()
                     .Where(
                         element =>
                             string.Equals(
                                 element.Name.LocalName,
                                 "meta",
                                 StringComparison.Ordinal) &&
                             string.Equals(
                                 AttributeValue(
                                     element,
                                     "name"),
                                 "cover",
                                 StringComparison.OrdinalIgnoreCase))
                     .Select(
                         element =>
                             AttributeValue(
                                 element,
                                 "content"))
                     .Where(
                         value =>
                             !string.IsNullOrWhiteSpace(
                                 value)))
        {
            coverIds.Add(
                metadataCoverId!);
        }

        return coverIds
            .Where(
                manifest.ContainsKey)
            .Select(
                id =>
                    manifest[id].ResourcePath)
            .ToHashSet(
                StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> ReadNavigationContentResourcePaths(
        IReadOnlyDictionary<string, ManifestItem> manifest)
        => manifest.Values
            .Where(
                item =>
                    item.Properties.Contains(
                        "nav"))
            .Select(
                item =>
                    item.ResourcePath)
            .ToHashSet(
                StringComparer.Ordinal);

    private static IReadOnlySet<string> ReadCoverContentResourcePaths(
        XDocument package,
        string packagePath)
    {
        var paths =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var reference in
                 package.Descendants()
                     .Where(
                         element =>
                             string.Equals(
                                 element.Name.LocalName,
                                 "reference",
                                 StringComparison.Ordinal) &&
                             HasToken(
                                 AttributeValue(
                                     element,
                                     "type"),
                                 "cover")))
        {
            var href =
                AttributeValue(
                    reference,
                    "href");

            if (!string.IsNullOrWhiteSpace(
                    href))
            {
                paths.Add(
                    EpubArchivePath.Resolve(
                        packagePath,
                        href));
            }
        }

        return paths;
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

    private static SpineResourceExtraction ExtractSpineResource(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        EpubSpineItemDescriptor spineItem,
        IReadOnlyDictionary<string, ManifestItem> manifestByResourcePath,
        IReadOnlySet<string> excludedImageResourcePaths,
        IReadOnlySet<string> navigationContentResourcePaths,
        IReadOnlySet<string> coverContentResourcePaths,
        long maximumTextResourceBytes)
    {
        if (!string.Equals(
                spineItem.MediaType,
                "application/xhtml+xml",
                StringComparison.OrdinalIgnoreCase))
        {
            return new SpineResourceExtraction(
                new StructuredNativeContentUnit(
                    spineItem.ResourcePath,
                    []),
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

        var visualUsages =
            ReadVisualUsages(
                document,
                spineItem,
                manifestByResourcePath,
                excludedImageResourcePaths,
                isNavigationContentResource:
                    navigationContentResourcePaths.Contains(
                        spineItem.ResourcePath),
                isCoverContentResource:
                    coverContentResourcePaths.Contains(
                        spineItem.ResourcePath));

        return new SpineResourceExtraction(
            new StructuredNativeContentUnit(
                spineItem.ResourcePath,
                blocks),
            visualUsages);
    }

    private static IReadOnlyList<VisualUsage> ReadVisualUsages(
        XDocument document,
        EpubSpineItemDescriptor spineItem,
        IReadOnlyDictionary<string, ManifestItem> manifestByResourcePath,
        IReadOnlySet<string> excludedImageResourcePaths,
        bool isNavigationContentResource,
        bool isCoverContentResource)
    {
        var usages =
            new List<VisualUsage>();

        var occurrenceIndex =
            0;

        foreach (var element in
                 document.Descendants()
                     .Where(
                         element =>
                             element.Name.LocalName is
                                 "img" or
                                 "image"))
        {
            var currentOccurrence =
                occurrenceIndex++;

            var reference =
                element.Name.LocalName ==
                "img"
                    ? AttributeValue(
                        element,
                        "src")
                    : AttributeValue(
                        element,
                        "href");

            if (!TryResolvePackagedResource(
                    spineItem.ResourcePath,
                    reference,
                    out var resourcePath) ||
                !manifestByResourcePath.TryGetValue(
                    resourcePath,
                    out var manifestItem) ||
                !manifestItem.MediaType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            usages.Add(
                new VisualUsage(
                    manifestItem.ResourcePath,
                    manifestItem.MediaType,
                    new EpubVisualSourceLocation(
                        spineItem.SpineIndex,
                        spineItem.ResourcePath,
                        manifestItem.ResourcePath,
                        currentOccurrence,
                        FragmentId(
                            element),
                        isAuxiliary:
                            !spineItem.IsLinear),
                    IsAuxiliary:
                        !spineItem.IsLinear,
                    IsPublicationCover:
                        excludedImageResourcePaths.Contains(
                            resourcePath) ||
                        isCoverContentResource ||
                        IsCoverContent(
                            element),
                    IsNavigation:
                        isNavigationContentResource,
                    IsExplicitlyPresentationOnly:
                        IsPresentationOnly(
                            element)));
        }

        return usages;
    }

    private static IReadOnlyList<StructuredNativeVisual> BuildVisualCandidates(
        IReadOnlyList<VisualUsage> usages)
    {
        var candidates =
            new List<StructuredNativeVisual>();

        foreach (var group in
                 usages.GroupBy(
                     usage =>
                         usage.ResourcePath,
                     StringComparer.Ordinal))
        {
            var preferredUsage =
                group.FirstOrDefault(
                    usage =>
                        !usage.IsPublicationCover &&
                        !usage.IsNavigation &&
                        !usage.IsExplicitlyPresentationOnly &&
                        !usage.IsAuxiliary) ??
                group.First();

            candidates.Add(
                new StructuredNativeVisual(
                    $"structured-visual-{candidates.Count + 1:D6}",
                    preferredUsage.Location,
                    preferredUsage.ResourcePath,
                    preferredUsage.MediaType,
                    isAuxiliary:
                        group.All(
                            usage =>
                                usage.IsAuxiliary),
                    isPublicationCover:
                        group.Any(
                            usage =>
                                usage.IsPublicationCover),
                    isNavigation:
                        group.All(
                            usage =>
                                usage.IsNavigation),
                    isExplicitlyPresentationOnly:
                        group.All(
                            usage =>
                                usage.IsExplicitlyPresentationOnly)));
        }

        return candidates;
    }

    private static bool IsPresentationOnly(
        XElement element) =>
        element.AncestorsAndSelf()
            .Any(
                candidate =>
                    string.Equals(
                        AttributeValue(
                            candidate,
                            "aria-hidden"),
                        "true",
                        StringComparison.OrdinalIgnoreCase) ||
                    HasToken(
                        AttributeValue(
                            candidate,
                            "role"),
                        "presentation") ||
                    HasToken(
                        AttributeValue(
                            candidate,
                            "role"),
                        "none"));

    private static bool IsCoverContent(
        XElement element) =>
        element.AncestorsAndSelf()
            .Any(
                candidate =>
                    HasToken(
                        AttributeValue(
                            candidate,
                            "type"),
                        "cover"));

    private static bool TryResolvePackagedResource(
        string baseResourcePath,
        string? reference,
        out string resourcePath)
    {
        resourcePath =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                reference) ||
            Uri.TryCreate(
                reference,
                UriKind.Absolute,
                out _))
        {
            return false;
        }

        resourcePath =
            EpubArchivePath.Resolve(
                baseResourcePath,
                reference);

        return true;
    }

    private static IReadOnlySet<string> ReadTokenAttribute(
        XElement element,
        string localName) =>
        (AttributeValue(
                element,
                localName) ??
         string.Empty)
        .Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet(
            StringComparer.Ordinal);

    private static bool HasToken(
        string? value,
        string expected) =>
        (value ??
         string.Empty)
        .Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)
        .Contains(
            expected,
            StringComparer.OrdinalIgnoreCase);

    private static string? AttributeValue(
        XElement element,
        string localName) =>
        element.Attributes()
            .FirstOrDefault(
                attribute =>
                    string.Equals(
                        attribute.Name.LocalName,
                        localName,
                        StringComparison.Ordinal))?
            .Value;

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
        string MediaType,
        IReadOnlySet<string> Properties);

    private sealed record VisualUsage(
        string ResourcePath,
        string MediaType,
        EpubVisualSourceLocation Location,
        bool IsAuxiliary,
        bool IsPublicationCover,
        bool IsNavigation,
        bool IsExplicitlyPresentationOnly);

    private sealed record SpineResourceExtraction(
        StructuredNativeContentUnit ContentUnit,
        IReadOnlyList<VisualUsage> VisualUsages);

    #endregion
}
