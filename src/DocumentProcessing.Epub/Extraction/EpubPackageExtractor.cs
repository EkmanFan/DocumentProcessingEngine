using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Documents.Notes;
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

    private const int MaximumRepeatedPresentationResourceBytes =
        1024;

    private static readonly ProcessingComponentIdentity
        NativeExtractionIdentity =
            new(
                "epub-xhtml",
                "epub-xhtml-native-v4+epubcheck-5.3.0");

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
            ["td"] =
                StructuredNativeTextBlockKind.Text,
            ["th"] =
                StructuredNativeTextBlockKind.Text,
            ["aside"] =
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

        var navigationReferences =
            ReadNavigationReferences(
                entries,
                manifest,
                options.MaximumTextResourceBytes);

        var coverContentResourcePaths =
            ReadCoverContentResourcePaths(
                package,
                packagePath);

        var spineItems =
            ReadSpine(
                package,
                manifest);

        var bodyMatterResourcePath =
            navigationReferences.BodyMatterResourcePath ??
            ReadGuideBodyMatterResourcePath(
                package,
                packagePath);

        var bodyMatterStartSpineIndex =
            bodyMatterResourcePath is null
                ? (int?)null
                : spineItems
                    .Where(
                        item =>
                            string.Equals(
                                item.ResourcePath,
                                bodyMatterResourcePath,
                                StringComparison.Ordinal))
                    .Select(
                        item =>
                            (int?)item.SpineIndex)
                    .FirstOrDefault();

        var contentUnits =
            new List<StructuredNativeContentUnit>(
                spineItems.Count);

        var visualUsages =
            new List<VisualUsage>();

        var notePayloadCandidates =
            new List<NotePayloadCandidate>();

        var noteReferenceCandidates =
            new List<NoteReferenceCandidate>();

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
                    bodyMatterStartSpineIndex,
                    navigationReferences,
                    isTerminalSpineResource:
                        spineItem.SpineIndex ==
                        spineItems.Count -
                        1,
                    options.MaximumTextResourceBytes);

            contentUnits.Add(
                extracted.ContentUnit);

            visualUsages.AddRange(
                extracted.VisualUsages);

            if (!extracted.ContentUnit.IsPresentationOnly)
            {
                notePayloadCandidates.AddRange(
                    extracted.NotePayloadCandidates);

                noteReferenceCandidates.AddRange(
                    extracted.NoteReferenceCandidates);
            }
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
                    "language"),
                bodyMatterStartSpineIndex);

        source.Position =
            0;

        var noteExtraction =
            BuildDocumentNotes(
                notePayloadCandidates,
                noteReferenceCandidates);

        return new StructuredNativeDocumentEvidence(
            structure,
            contentUnits,
            NativeExtractionIdentity,
            BuildVisualCandidates(
                entries,
                visualUsages),
            noteExtraction.DocumentNotes,
            noteExtraction.PayloadCandidateLocations);
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

    private static NavigationReferences ReadNavigationReferences(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IReadOnlyDictionary<string, ManifestItem> manifest,
        long maximumTextResourceBytes)
    {
        var tocTargets =
            new HashSet<NavigationTarget>();

        var listedResourcePaths =
            new HashSet<string>(
                StringComparer.Ordinal);

        string? bodyMatterResourcePath =
            null;

        foreach (var navigationItem in
                 manifest.Values.Where(
                     item =>
                         item.Properties.Contains(
                             "nav") &&
                         string.Equals(
                             item.MediaType,
                             "application/xhtml+xml",
                             StringComparison.OrdinalIgnoreCase)))
        {
            var navigation =
                LoadXml(
                    GetRequiredEntry(
                        entries,
                        navigationItem.ResourcePath),
                    maximumTextResourceBytes);

            foreach (var navigationElement in
                     navigation.Descendants()
                         .Where(
                             element =>
                                 string.Equals(
                                     element.Name.LocalName,
                                     "nav",
                                     StringComparison.OrdinalIgnoreCase)))
            {
                var isTableOfContents =
                    HasToken(
                        AttributeValue(
                            navigationElement,
                            "type"),
                        "toc");

                var isLandmarks =
                    HasToken(
                        AttributeValue(
                            navigationElement,
                            "type"),
                        "landmarks");

                foreach (var anchor in
                         navigationElement.Descendants()
                             .Where(
                                 element =>
                                     string.Equals(
                                         element.Name.LocalName,
                                         "a",
                                         StringComparison.OrdinalIgnoreCase)))
                {
                    if (!TryReadNavigationTarget(
                            navigationItem.ResourcePath,
                            AttributeValue(
                                anchor,
                                "href"),
                            out var target))
                    {
                        continue;
                    }

                    listedResourcePaths.Add(
                        target.ResourcePath);

                    if (isTableOfContents)
                    {
                        tocTargets.Add(
                            target);
                    }

                    if (bodyMatterResourcePath is null &&
                        isLandmarks &&
                        HasToken(
                            AttributeValue(
                                anchor,
                                "type"),
                            "bodymatter"))
                    {
                        bodyMatterResourcePath =
                            target.ResourcePath;
                    }
                }
            }
        }

        return new NavigationReferences(
            tocTargets.ToArray(),
            listedResourcePaths,
            bodyMatterResourcePath);
    }

    private static bool TryReadNavigationTarget(
        string navigationResourcePath,
        string? href,
        out NavigationTarget target)
    {
        target =
            null!;

        if (string.IsNullOrWhiteSpace(
                href) ||
            Uri.TryCreate(
                href.Trim(),
                UriKind.Absolute,
                out _))
        {
            return false;
        }

        var reference =
            href.Trim();

        var fragmentIndex =
            reference.IndexOf(
                '#');

        var fragmentId =
            fragmentIndex <
            0
                ? null
                : Uri.UnescapeDataString(
                    reference[(fragmentIndex +
                               1)..]);

        target =
            new NavigationTarget(
                EpubArchivePath.Resolve(
                    navigationResourcePath,
                    reference),
                string.IsNullOrWhiteSpace(
                    fragmentId)
                    ? null
                    : fragmentId);

        return true;
    }

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

    private static string? ReadGuideBodyMatterResourcePath(
        XDocument package,
        string packagePath)
    {
        var guideReference =
            package.Descendants()
                .FirstOrDefault(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "reference",
                            StringComparison.Ordinal) &&
                        HasToken(
                            AttributeValue(
                                element,
                                "type"),
                            "text"));

        var guideHref =
            guideReference is null
                ? null
                : AttributeValue(
                    guideReference,
                    "href");

        return string.IsNullOrWhiteSpace(
            guideHref)
            ? null
            : EpubArchivePath.Resolve(
                packagePath,
                guideHref);
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
        int? bodyMatterStartSpineIndex,
        NavigationReferences navigationReferences,
        bool isTerminalSpineResource,
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
                [],
                [],
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

        var isTerminalPresentationResource =
            IsTerminalPresentationResource(
                body,
                spineItem.ResourcePath,
                navigationReferences,
                isTerminalSpineResource,
                bodyMatterStartSpineIndex.HasValue);

        var navigationHeadingBlocks =
            FindNavigationHeadingBlocks(
                body,
                spineItem.ResourcePath,
                navigationReferences.TocTargets);

        var blocks =
            new List<StructuredNativeTextBlock>();

        var blockLocations =
            new Dictionary<XElement, EpubDocumentSourceLocation>();

        foreach (var child in
                 body.Elements())
        {
            VisitElement(
                child,
                spineItem,
                navigationHeadingBlocks,
                blocks,
                blockLocations);
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
                        spineItem.ResourcePath),
                isPreliminaryMatter:
                    bodyMatterStartSpineIndex.HasValue &&
                    spineItem.SpineIndex <
                    bodyMatterStartSpineIndex.Value,
                hasBodyMatterBoundary:
                    bodyMatterStartSpineIndex.HasValue,
                isTerminalPresentationResource:
                    isTerminalPresentationResource);

        return new SpineResourceExtraction(
            new StructuredNativeContentUnit(
                spineItem.ResourcePath,
                blocks,
                isPresentationOnly:
                    isTerminalPresentationResource),
            visualUsages,
            ReadNotePayloadCandidates(
                document,
                spineItem,
                blockLocations),
            ReadNoteReferenceCandidates(
                document,
                spineItem,
                blockLocations));
    }

    private static IReadOnlyList<VisualUsage> ReadVisualUsages(
        XDocument document,
        EpubSpineItemDescriptor spineItem,
        IReadOnlyDictionary<string, ManifestItem> manifestByResourcePath,
        IReadOnlySet<string> excludedImageResourcePaths,
        bool isNavigationContentResource,
        bool isCoverContentResource,
        bool isPreliminaryMatter,
        bool hasBodyMatterBoundary,
        bool isTerminalPresentationResource)
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
                            element),
                    IsStructuredFigure:
                        IsStructuredFigure(
                            element),
                    HasEmptyAlternativeText:
                        HasEmptyAlternativeText(
                            element),
                    IsPreliminaryMatter:
                        isPreliminaryMatter,
                    HasBodyMatterBoundary:
                        hasBodyMatterBoundary,
                    IsTerminalPresentationMatter:
                        isTerminalPresentationResource));
        }

        return usages;
    }

    private static IReadOnlyList<StructuredNativeVisual> BuildVisualCandidates(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
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
            var groupedUsages =
                group.ToArray();

            var preferredUsage =
                groupedUsages.FirstOrDefault(
                    usage =>
                        !usage.IsPublicationCover &&
                        !usage.IsNavigation &&
                        !usage.IsExplicitlyPresentationOnly &&
                        !usage.IsAuxiliary) ??
                groupedUsages[0];

            var isRepeatedPresentationVisual =
                groupedUsages.Length >
                    1 &&
                groupedUsages.All(
                    usage =>
                        usage.HasEmptyAlternativeText &&
                        !usage.IsStructuredFigure) &&
                entries.TryGetValue(
                    preferredUsage.ResourcePath,
                    out var imageEntry) &&
                imageEntry.Length <=
                MaximumRepeatedPresentationResourceBytes;

            candidates.Add(
                new StructuredNativeVisual(
                    $"structured-visual-{candidates.Count + 1:D6}",
                    preferredUsage.Location,
                    preferredUsage.ResourcePath,
                    preferredUsage.MediaType,
                    isAuxiliary:
                        groupedUsages.All(
                            usage =>
                                usage.IsAuxiliary),
                    isPublicationCover:
                        groupedUsages.Any(
                            usage =>
                                usage.IsPublicationCover),
                    isNavigation:
                        groupedUsages.All(
                            usage =>
                                usage.IsNavigation),
                    isExplicitlyPresentationOnly:
                        groupedUsages.All(
                            usage =>
                                usage.IsExplicitlyPresentationOnly),
                    isPreliminaryMatter:
                        groupedUsages.All(
                            usage =>
                                usage.IsPreliminaryMatter),
                    hasBodyMatterBoundary:
                        groupedUsages.All(
                            usage =>
                                usage.HasBodyMatterBoundary),
                    isStructuredFigure:
                        groupedUsages.Any(
                            usage =>
                                usage.IsStructuredFigure),
                    isRepeatedPresentationVisual:
                        isRepeatedPresentationVisual,
                    isTerminalPresentationMatter:
                        groupedUsages.All(
                            usage =>
                                usage.IsTerminalPresentationMatter)));
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

    private static bool IsStructuredFigure(
        XElement element) =>
        element.AncestorsAndSelf()
            .Any(
                candidate =>
                    string.Equals(
                        candidate.Name.LocalName,
                        "figure",
                        StringComparison.OrdinalIgnoreCase));

    private static bool HasEmptyAlternativeText(
        XElement element) =>
        string.Equals(
            element.Name.LocalName,
            "img",
            StringComparison.OrdinalIgnoreCase) &&
        AttributeValue(
            element,
            "alt") is { } alternativeText &&
        string.IsNullOrWhiteSpace(
            alternativeText);

    private static bool IsTerminalPresentationResource(
        XElement body,
        string resourcePath,
        NavigationReferences navigationReferences,
        bool isTerminalSpineResource,
        bool hasBodyMatterBoundary)
    {
        if (!isTerminalSpineResource ||
            hasBodyMatterBoundary)
        {
            return false;
        }

        var images =
            body.Descendants()
                .Where(
                    element =>
                        element.Name.LocalName is
                            "img" or
                            "image")
                .ToArray();

        if (images.Length ==
            0)
        {
            return false;
        }

        var textBlockCount =
            body.Descendants()
                .Count(
                    element =>
                        BlockKinds.ContainsKey(
                            element.Name.LocalName) &&
                        !string.IsNullOrWhiteSpace(
                            element.Value));

        if (textBlockCount ==
            0)
        {
            return true;
        }

        if (images.Length <
                2 ||
            navigationReferences.ListedResourcePaths.Contains(
                resourcePath))
        {
            return false;
        }

        var externalLinkCount =
            body.Descendants()
                .Count(
                    element =>
                        string.Equals(
                            element.Name.LocalName,
                            "a",
                            StringComparison.OrdinalIgnoreCase) &&
                        Uri.TryCreate(
                            AttributeValue(
                                element,
                                "href"),
                            UriKind.Absolute,
                            out var uri) &&
                        uri.Scheme is
                            "http" or
                            "https");

        return externalLinkCount >=
               images.Length;
    }

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

    private static IReadOnlySet<XElement> FindNavigationHeadingBlocks(
        XElement body,
        string resourcePath,
        IReadOnlyList<NavigationTarget> tocTargets)
    {
        var targets =
            tocTargets
                .Where(
                    target =>
                        string.Equals(
                            target.ResourcePath,
                            resourcePath,
                            StringComparison.Ordinal))
                .ToArray();

        var headingBlocks =
            new HashSet<XElement>();

        var textBlocks =
            body.Descendants()
                .Where(
                    IsTextBlock)
                .ToArray();

        foreach (var target in
                 targets)
        {
            XElement? associatedBlock;

            if (target.FragmentId is null)
            {
                associatedBlock =
                    textBlocks.FirstOrDefault();
            }
            else
            {
                var targetElement =
                    body.DescendantsAndSelf()
                        .FirstOrDefault(
                            element =>
                                string.Equals(
                                    FragmentId(
                                        element),
                                    target.FragmentId,
                                    StringComparison.Ordinal));

                associatedBlock =
                    targetElement is null
                        ? null
                        : FindAssociatedTextBlock(
                            targetElement);
            }

            if (associatedBlock is not null)
            {
                headingBlocks.Add(
                    associatedBlock);
            }
        }

        return headingBlocks;
    }

    private static XElement? FindAssociatedTextBlock(
        XElement targetElement) =>
        targetElement.AncestorsAndSelf()
            .FirstOrDefault(
                IsTextBlock) ??
        targetElement.Descendants()
            .FirstOrDefault(
                IsTextBlock) ??
        targetElement.ElementsAfterSelf()
            .SelectMany(
                element =>
                    element.DescendantsAndSelf())
            .FirstOrDefault(
                IsTextBlock);

    private static bool IsTextBlock(
        XElement element) =>
        BlockKinds.ContainsKey(
            element.Name.LocalName) &&
        !string.IsNullOrWhiteSpace(
            element.Value);

    private static void VisitElement(
        XElement element,
        EpubSpineItemDescriptor spineItem,
        IReadOnlySet<XElement> navigationHeadingBlocks,
        ICollection<StructuredNativeTextBlock> blocks,
        IDictionary<XElement, EpubDocumentSourceLocation> blockLocations)
    {
        if (BlockKinds.TryGetValue(
                element.Name.LocalName,
                out var kind))
        {
            if (!string.IsNullOrWhiteSpace(
                    element.Value))
            {
                var location =
                    new EpubDocumentSourceLocation(
                        spineItem.SpineIndex,
                        spineItem.ResourcePath,
                        blocks.Count,
                        FragmentId(
                            element));

                blocks.Add(
                    new StructuredNativeTextBlock(
                        navigationHeadingBlocks.Contains(
                            element)
                            ? StructuredNativeTextBlockKind.Heading
                            : kind,
                        location,
                        element.Value));

                blockLocations.Add(
                    element,
                    location);
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
                navigationHeadingBlocks,
                blocks,
                blockLocations);
        }
    }

    #region Methods Notes

    private static string ReadElementText(
        XElement element,
        IReadOnlySet<XElement> excludedLinks)
    {
        var sourceTextNodes =
            element
                .DescendantNodesAndSelf()
                .OfType<XText>()
                .ToArray();

        var text =
            string.Concat(
                sourceTextNodes
                .Where(
                    sourceText =>
                        !sourceText.Ancestors()
                            .Any(
                                excludedLinks.Contains))
                .Select(
                    sourceText =>
                        sourceText.Value));

        var firstMeaningfulText =
            sourceTextNodes.FirstOrDefault(
                sourceText =>
                    !string.IsNullOrWhiteSpace(
                        sourceText.Value));

        if (firstMeaningfulText is not null &&
            firstMeaningfulText.Ancestors()
                .Any(
                    excludedLinks.Contains))
        {
            text =
                text.TrimStart()
                    .TrimStart(
                        '.',
                        ':',
                        ')',
                        '\u200B')
                    .TrimStart();
        }

        return text;
    }

    private static bool IsNoteBacklink(
        XElement element) =>
        string.Equals(
            element.Name.LocalName,
            "a",
            StringComparison.OrdinalIgnoreCase) &&
        (HasToken(
             AttributeValue(
                 element,
                 "type"),
             "backlink") ||
         HasToken(
             AttributeValue(
                 element,
                 "role"),
             "doc-backlink"));

    private static IReadOnlyList<NotePayloadCandidate>
        ReadNotePayloadCandidates(
        XDocument document,
        EpubSpineItemDescriptor spineItem,
        IReadOnlyDictionary<XElement, EpubDocumentSourceLocation>
            blockLocations)
    {
        var candidates =
            new List<NotePayloadCandidate>();

        foreach (var pair in
                 blockLocations)
        {
            var fragmentId =
                FragmentId(
                    pair.Key);

            if (fragmentId is null)
            {
                continue;
            }

            var backlinks =
                ReadPotentialNoteBacklinks(
                    pair.Key,
                    spineItem.ResourcePath);

            var explicitlyAnnotated =
                IsNotePayload(
                    pair.Key);

            if (!explicitlyAnnotated &&
                backlinks.Count ==
                    0)
            {
                continue;
            }

            candidates.Add(
                new NotePayloadCandidate(
                    new NoteTarget(
                        spineItem.ResourcePath,
                        fragmentId),
                    pair.Value,
                    pair.Key,
                    explicitlyAnnotated,
                    backlinks));
        }

        return candidates;
    }

    private static IReadOnlyList<NoteBacklinkCandidate>
        ReadPotentialNoteBacklinks(
        XElement payload,
        string resourcePath)
    {
        var backlinks =
            new List<NoteBacklinkCandidate>();

        foreach (var link in
                 payload.Descendants()
                     .Where(
                         element =>
                             string.Equals(
                                 element.Name.LocalName,
                                 "a",
                                 StringComparison.OrdinalIgnoreCase) &&
                             !IsNoteReference(
                                 element)))
        {
            if (!TryResolveNoteTarget(
                    resourcePath,
                    AttributeValue(
                        link,
                        "href"),
                    out var target))
            {
                continue;
            }

            backlinks.Add(
                new NoteBacklinkCandidate(
                    target,
                    link.Value.Trim(),
                    link,
                    IsNoteBacklink(
                        link)));
        }

        return backlinks;
    }

    private static IReadOnlyList<NoteReferenceCandidate>
        ReadNoteReferenceCandidates(
        XDocument document,
        EpubSpineItemDescriptor spineItem,
        IReadOnlyDictionary<XElement, EpubDocumentSourceLocation>
            blockLocations)
    {
        var candidates =
            new List<NoteReferenceCandidate>();

        foreach (var element in
                 document.Descendants()
                     .Where(
                         IsNoteReference))
        {
            if (!TryResolveNoteTarget(
                    spineItem.ResourcePath,
                    AttributeValue(
                        element,
                        "href"),
                    out var target))
            {
                continue;
            }

            var label =
                element.Value.Trim();

            var markerTargets =
                ReadNoteReferenceMarkerTargets(
                    element,
                    spineItem.ResourcePath);

            var owner =
                element.Ancestors()
                    .FirstOrDefault(
                        blockLocations.ContainsKey);

            if (owner is null ||
                label.Length ==
                    0)
            {
                candidates.Add(
                    new NoteReferenceCandidate(
                        target,
                        label,
                        markerTargets,
                        OwnerLocation:
                            null,
                        Location:
                            null));

                continue;
            }

            var ownerLocation =
                blockLocations[owner];

            candidates.Add(
                new NoteReferenceCandidate(
                    target,
                    label,
                    markerTargets,
                    ownerLocation,
                    new EpubDocumentSourceLocation(
                        spineItem.SpineIndex,
                        spineItem.ResourcePath,
                        ownerLocation.BlockIndex,
                        FragmentId(
                            element))));
        }

        return candidates;
    }

    private static IReadOnlyList<NoteTarget>
        ReadNoteReferenceMarkerTargets(
        XElement reference,
        string resourcePath)
    {
        var fragmentIds =
            new List<string?>
            {
                FragmentId(
                    reference),
                reference.ElementsBeforeSelf()
                    .LastOrDefault() is { } previous
                    ? FragmentId(
                        previous)
                    : null
            };

        return fragmentIds
            .Where(
                fragmentId =>
                    !string.IsNullOrWhiteSpace(
                        fragmentId))
            .Select(
                fragmentId =>
                    new NoteTarget(
                        resourcePath,
                        fragmentId!))
            .Distinct()
            .ToArray();
    }

    private static DocumentNoteExtraction BuildDocumentNotes(
        IReadOnlyList<NotePayloadCandidate> payloadCandidates,
        IReadOnlyList<NoteReferenceCandidate> referenceCandidates)
    {
        var payloadsByTarget =
            payloadCandidates
                .GroupBy(
                    candidate =>
                        candidate.Target)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.ToArray());

        var referenceMarkerTargets =
            referenceCandidates
                .SelectMany(
                    reference =>
                        reference.MarkerTargets)
                .ToHashSet();

        var qualifiedPayloads =
            payloadCandidates
                .Where(
                    payload =>
                        payload.IsExplicitlyAnnotated ||
                        payload.Backlinks.Any(
                            backlink =>
                                referenceMarkerTargets.Contains(
                                    backlink.Target)))
                .ToArray();

        var mappings =
            new List<NoteRelationMapping>();

        foreach (var reference in
                 referenceCandidates)
        {
            var reciprocalMatches =
                qualifiedPayloads
                    .Where(
                        payload =>
                            payload.Backlinks.Any(
                                backlink =>
                                    reference.MarkerTargets.Contains(
                                        backlink.Target) &&
                                    (backlink.IsSemanticallyAnnotated ||
                                     string.Equals(
                                         backlink.Label,
                                         reference.Label,
                                         StringComparison.Ordinal))))
                    .ToArray();

            NotePayloadCandidate? payload =
                reciprocalMatches.Length ==
                    1
                    ? reciprocalMatches[0]
                    : null;

            if (reciprocalMatches.Length ==
                    0 &&
                payloadsByTarget.TryGetValue(
                    reference.ForwardTarget,
                    out var forwardPayloads) &&
                forwardPayloads.Length ==
                    1 &&
                forwardPayloads[0].IsExplicitlyAnnotated)
            {
                payload =
                    forwardPayloads[0];
            }

            if (payload is not null)
            {
                mappings.Add(
                    new NoteRelationMapping(
                        reference,
                        payload));
            }
        }

        var notes =
            new List<NativeDocumentNote>();

        foreach (var relationGroup in
                 mappings
                     .GroupBy(
                         mapping =>
                             mapping.Payload.Target)
                     .OrderBy(
                         group =>
                             group.Min(
                                 mapping =>
                                     mapping.Reference.OwnerLocation?
                                         .SpineIndex ??
                                     int.MaxValue))
                     .ThenBy(
                         group =>
                             group.Min(
                                 mapping =>
                                     mapping.Reference.OwnerLocation?
                                         .BlockIndex ??
                                     int.MaxValue)))
        {
            var references =
                relationGroup
                    .Select(
                        mapping =>
                            mapping.Reference)
                    .ToArray();

            if (references.Any(
                    reference =>
                        reference.OwnerLocation is null ||
                        reference.Location is null))
            {
                continue;
            }

            var labels =
                references
                    .Select(
                        reference =>
                            reference.Label)
                    .Distinct(
                        StringComparer.Ordinal)
                    .ToArray();

            if (labels.Length !=
                1)
            {
                continue;
            }

            var payload =
                relationGroup.First()
                    .Payload;

            var excludedBacklinks =
                payload.Backlinks
                    .Where(
                        backlink =>
                            backlink.IsSemanticallyAnnotated ||
                            references.Any(
                                reference =>
                                    reference.MarkerTargets.Contains(
                                        backlink.Target) &&
                                    string.Equals(
                                        backlink.Label,
                                        reference.Label,
                                        StringComparison.Ordinal)))
                    .Select(
                        backlink =>
                            backlink.Element)
                    .ToHashSet();

            var text =
                ReadElementText(
                    payload.Element,
                    excludedBacklinks);

            if (string.IsNullOrWhiteSpace(
                    text))
            {
                continue;
            }

            notes.Add(
                new StructuredNativeDocumentNote(
                    labels[0],
                    text,
                    references
                        .Select(
                            reference =>
                                new StructuredNativeNoteReference(
                                    reference.OwnerLocation!,
                                    reference.Location!))
                        .ToArray(),
                    [payload.Location]));
        }

        return new DocumentNoteExtraction(
            notes,
            qualifiedPayloads
                .Select(
                    payload =>
                        (DocumentProcessing.Core.Locations.DocumentSourceLocation)
                            payload.Location)
                .Distinct()
                .ToArray());
    }

    private static bool IsNotePayload(
        XElement element) =>
        HasToken(
            AttributeValue(
                element,
                "type"),
            "footnote") ||
        HasToken(
            AttributeValue(
                element,
                "type"),
            "endnote") ||
        HasToken(
            AttributeValue(
                element,
                "type"),
            "rearnote") ||
        HasToken(
            AttributeValue(
                element,
                "role"),
            "doc-footnote") ||
        HasToken(
            AttributeValue(
                element,
                "role"),
            "doc-endnote");

    private static bool IsNoteReference(
        XElement element) =>
        string.Equals(
            element.Name.LocalName,
            "a",
            StringComparison.OrdinalIgnoreCase) &&
        (HasToken(
             AttributeValue(
                 element,
                 "type"),
             "noteref") ||
         HasToken(
             AttributeValue(
                 element,
                 "role"),
             "doc-noteref"));

    private static bool TryResolveNoteTarget(
        string baseResourcePath,
        string? reference,
        out NoteTarget target)
    {
        target =
            default;

        if (string.IsNullOrWhiteSpace(
                reference) ||
            Uri.TryCreate(
                reference,
                UriKind.Absolute,
                out _))
        {
            return false;
        }

        var value =
            reference.Trim();

        var fragmentSeparator =
            value.IndexOf(
                '#');

        if (fragmentSeparator <
            0)
        {
            return false;
        }

        var fragment =
            Uri.UnescapeDataString(
                value[(fragmentSeparator + 1)..]
                    .Split(
                        '?',
                        2)[0]);

        if (string.IsNullOrWhiteSpace(
                fragment))
        {
            return false;
        }

        try
        {
            var resourceReference =
                value[..fragmentSeparator];

            var resourcePath =
                resourceReference.Length ==
                    0
                    ? baseResourcePath
                    : EpubArchivePath.Resolve(
                        baseResourcePath,
                        resourceReference);

            target =
                new NoteTarget(
                    resourcePath,
                    fragment);

            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    #endregion

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

    private sealed record NavigationTarget(
        string ResourcePath,
        string? FragmentId);

    private sealed record NavigationReferences(
        IReadOnlyList<NavigationTarget> TocTargets,
        IReadOnlySet<string> ListedResourcePaths,
        string? BodyMatterResourcePath);

    private sealed record VisualUsage(
        string ResourcePath,
        string MediaType,
        EpubVisualSourceLocation Location,
        bool IsAuxiliary,
        bool IsPublicationCover,
        bool IsNavigation,
        bool IsExplicitlyPresentationOnly,
        bool IsStructuredFigure,
        bool HasEmptyAlternativeText,
        bool IsPreliminaryMatter,
        bool HasBodyMatterBoundary,
        bool IsTerminalPresentationMatter);

    private readonly record struct NoteTarget(
        string ResourcePath,
        string FragmentId);

    private sealed record NotePayloadCandidate(
        NoteTarget Target,
        EpubDocumentSourceLocation Location,
        XElement Element,
        bool IsExplicitlyAnnotated,
        IReadOnlyList<NoteBacklinkCandidate> Backlinks);

    private sealed record NoteBacklinkCandidate(
        NoteTarget Target,
        string Label,
        XElement Element,
        bool IsSemanticallyAnnotated);

    private sealed record NoteReferenceCandidate(
        NoteTarget ForwardTarget,
        string Label,
        IReadOnlyList<NoteTarget> MarkerTargets,
        EpubDocumentSourceLocation? OwnerLocation,
        EpubDocumentSourceLocation? Location);

    private sealed record NoteRelationMapping(
        NoteReferenceCandidate Reference,
        NotePayloadCandidate Payload);

    private sealed record DocumentNoteExtraction(
        IReadOnlyList<NativeDocumentNote> DocumentNotes,
        IReadOnlyList<DocumentProcessing.Core.Locations.DocumentSourceLocation>
            PayloadCandidateLocations);

    private sealed record SpineResourceExtraction(
        StructuredNativeContentUnit ContentUnit,
        IReadOnlyList<VisualUsage> VisualUsages,
        IReadOnlyList<NotePayloadCandidate> NotePayloadCandidates,
        IReadOnlyList<NoteReferenceCandidate> NoteReferenceCandidates);

    #endregion
}
