using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Epub.Export;

/// <summary>
/// Exports the canonical Engine result as a reflowable EPUB publication.
/// </summary>
public sealed class EpubPublicationExporter
{
    private const string ContainerNamespace =
        "urn:oasis:names:tc:opendocument:xmlns:container";

    private const string DublinCoreNamespace =
        "http://purl.org/dc/elements/1.1/";

    private const string EpubNamespace =
        "http://www.idpf.org/2007/ops";

    private const string OpfNamespace =
        "http://www.idpf.org/2007/opf";

    private const string XhtmlNamespace =
        "http://www.w3.org/1999/xhtml";

    private static readonly UTF8Encoding Utf8WithoutBom =
        new(
            encoderShouldEmitUTF8Identifier:
                false);

    /// <summary>
    /// Writes one complete EPUB while leaving the destination stream open.
    /// </summary>
    public async Task<EpubPublicationExportResult> ExportAsync(
        DocumentProcessingResult document,
        EpubPublicationMetadata metadata,
        Stream destination,
        EpubVisualAssetReader? visualAssetReader = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            metadata);

        ArgumentNullException.ThrowIfNull(
            destination);

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "EPUB destination stream must be writable.",
                nameof(destination));
        }

        if (document.VisualAssets.Count > 0 &&
            visualAssetReader is null)
        {
            throw new ArgumentException(
                "A visual asset reader is required when the processing result contains preserved visuals.",
                nameof(visualAssetReader));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var identifier =
            metadata.Identifier ??
            $"urn:sha256:{document.Source.Sha256}";

        var modifiedAtUtc =
            metadata.ModifiedAtUtc ??
            DateTimeOffset.UtcNow;

        var zipTimestamp =
            NormalizeZipTimestamp(
                modifiedAtUtc);

        var visualElementsById =
            document.Elements
                .Where(
                    element =>
                        element.Kind ==
                        DocumentElementKind.Visual)
                .ToDictionary(
                    element =>
                        element.ElementId,
                    StringComparer.Ordinal);

        var visualEntries =
            CreateVisualEntries(
                document.VisualAssets,
                visualElementsById);

        var visualEntriesByElementId =
            visualEntries.ToDictionary(
                entry =>
                    entry.Asset.ElementId,
                StringComparer.Ordinal);

        var sections =
            CreateSections(
                document,
                visualEntriesByElementId);

        if (sections.Count == 0)
        {
            throw new ArgumentException(
                "The processing result contains no exportable text or visual content.",
                nameof(document));
        }

        var omittedElementCount =
            document.Elements.Count -
            sections.Sum(
                section =>
                    section.Elements.Count);

        using (var archive =
               new ZipArchive(
                   destination,
                   ZipArchiveMode.Create,
                   leaveOpen:
                       true))
        {
            WriteTextEntry(
                archive,
                "mimetype",
                "application/epub+zip",
                CompressionLevel.NoCompression,
                zipTimestamp);

            WriteTextEntry(
                archive,
                "META-INF/container.xml",
                CreateContainerDocument(),
                CompressionLevel.Optimal,
                zipTimestamp);

            WriteTextEntry(
                archive,
                "OEBPS/styles/publication.css",
                CreateStylesheet(),
                CompressionLevel.Optimal,
                zipTimestamp);

            WriteTextEntry(
                archive,
                "OEBPS/navigation.xhtml",
                CreateNavigationDocument(
                    metadata,
                    sections),
                CompressionLevel.Optimal,
                zipTimestamp);

            WriteTextEntry(
                archive,
                "OEBPS/content.opf",
                CreatePackageDocument(
                    metadata,
                    identifier,
                    modifiedAtUtc,
                    sections,
                    visualEntries),
                CompressionLevel.Optimal,
                zipTimestamp);

            foreach (var section in
                     sections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                WriteTextEntry(
                    archive,
                    $"OEBPS/{section.FileName}",
                    CreateContentDocument(
                        metadata,
                        section,
                        visualEntriesByElementId),
                    CompressionLevel.Optimal,
                    zipTimestamp);
            }

            foreach (var visualEntry in
                     visualEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await WriteVisualEntryAsync(
                        archive,
                        visualEntry,
                        visualElementsById[visualEntry.Asset.ElementId],
                        visualAssetReader!,
                        zipTimestamp,
                        cancellationToken)
                    .ConfigureAwait(
                        false);
            }
        }

        return new EpubPublicationExportResult(
            identifier,
            sections.Count,
            visualEntries.Count,
            omittedElementCount);
    }

    private static IReadOnlyList<ContentSection> CreateSections(
        DocumentProcessingResult document,
        IReadOnlyDictionary<string, VisualEntry> visualEntriesByElementId)
    {
        var sections =
            new List<ContentSection>();

        var currentElements =
            new List<DocumentElement>();

        string? currentSegmentId =
            null;

        var hasCurrentRun =
            false;

        foreach (var element in
                 document.Elements)
        {
            if (!IsExportable(
                    element,
                    visualEntriesByElementId))
            {
                continue;
            }

            if (hasCurrentRun &&
                !string.Equals(
                    currentSegmentId,
                    element.SegmentId,
                    StringComparison.Ordinal))
            {
                AddSection(
                    sections,
                    currentElements);

                currentElements =
                    [];
            }

            currentSegmentId =
                element.SegmentId;

            hasCurrentRun =
                true;

            currentElements.Add(
                element);
        }

        if (currentElements.Count > 0)
        {
            AddSection(
                sections,
                currentElements);
        }

        return sections;
    }

    private static bool IsExportable(
        DocumentElement element,
        IReadOnlyDictionary<string, VisualEntry> visualEntriesByElementId) =>
        element.Kind is
            DocumentElementKind.Text or
            DocumentElementKind.Heading or
            DocumentElementKind.Caption ||
        element.Kind ==
            DocumentElementKind.Visual &&
        visualEntriesByElementId.ContainsKey(
            element.ElementId);

    private static void AddSection(
        ICollection<ContentSection> sections,
        IReadOnlyList<DocumentElement> elements)
    {
        var ordinal =
            sections.Count + 1;

        var headingText =
            elements.FirstOrDefault(
                    element =>
                        element.Kind ==
                        DocumentElementKind.Heading)
                ?.Text;

        sections.Add(
            new ContentSection(
                ordinal,
                $"section-{ordinal:D4}.xhtml",
                headingText,
                elements.ToArray()));
    }

    private static IReadOnlyList<VisualEntry> CreateVisualEntries(
        IReadOnlyList<DocumentVisualAsset> visualAssets,
        IReadOnlyDictionary<string, DocumentElement> visualElementsById)
    {
        var entries =
            new List<VisualEntry>(
                visualAssets.Count);

        for (var index = 0;
             index < visualAssets.Count;
             index++)
        {
            var asset =
                visualAssets[index];

            if (!visualElementsById.ContainsKey(
                    asset.ElementId))
            {
                throw new ArgumentException(
                    $"Visual asset '{asset.AssetId}' references a missing visual element.",
                    nameof(visualAssets));
            }

            var extension =
                GetImageExtension(
                    asset.MediaType);

            entries.Add(
                new VisualEntry(
                    asset,
                    $"images/visual-{index + 1:D4}{extension}"));
        }

        return entries;
    }

    private static string GetImageExtension(
        string mediaType) =>
        mediaType switch
        {
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/svg+xml" => ".svg",
            "image/webp" => ".webp",
            _ => throw new NotSupportedException(
                $"EPUB export does not support visual media type '{mediaType}'.")
        };

    private static string CreateContainerDocument() =>
        CreateXml(
            writer =>
            {
                writer.WriteStartElement(
                    "container",
                    ContainerNamespace);

                writer.WriteAttributeString(
                    "version",
                    "1.0");

                writer.WriteStartElement(
                    "rootfiles",
                    ContainerNamespace);

                writer.WriteStartElement(
                    "rootfile",
                    ContainerNamespace);

                writer.WriteAttributeString(
                    "full-path",
                    "OEBPS/content.opf");

                writer.WriteAttributeString(
                    "media-type",
                    "application/oebps-package+xml");

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

    private static string CreatePackageDocument(
        EpubPublicationMetadata metadata,
        string identifier,
        DateTimeOffset modifiedAtUtc,
        IReadOnlyList<ContentSection> sections,
        IReadOnlyList<VisualEntry> visualEntries) =>
        CreateXml(
            writer =>
            {
                writer.WriteStartElement(
                    "package",
                    OpfNamespace);

                writer.WriteAttributeString(
                    "version",
                    "3.0");

                writer.WriteAttributeString(
                    "unique-identifier",
                    "publication-id");

                writer.WriteAttributeString(
                    "xml",
                    "lang",
                    null,
                    metadata.Language);

                writer.WriteStartElement(
                    "metadata",
                    OpfNamespace);

                writer.WriteStartElement(
                    "dc",
                    "identifier",
                    DublinCoreNamespace);

                writer.WriteAttributeString(
                    "id",
                    "publication-id");

                writer.WriteString(
                    identifier);

                writer.WriteEndElement();

                WriteDublinCoreElement(
                    writer,
                    "title",
                    metadata.Title);

                WriteDublinCoreElement(
                    writer,
                    "language",
                    metadata.Language);

                if (metadata.Creator is not null)
                {
                    WriteDublinCoreElement(
                        writer,
                        "creator",
                        metadata.Creator);
                }

                writer.WriteStartElement(
                    "meta",
                    OpfNamespace);

                writer.WriteAttributeString(
                    "property",
                    "dcterms:modified");

                writer.WriteString(
                    modifiedAtUtc
                        .ToUniversalTime()
                        .ToString(
                            "yyyy-MM-dd'T'HH:mm:ss'Z'",
                            CultureInfo.InvariantCulture));

                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement(
                    "manifest",
                    OpfNamespace);

                WriteManifestItem(
                    writer,
                    "navigation",
                    "navigation.xhtml",
                    "application/xhtml+xml",
                    "nav");

                WriteManifestItem(
                    writer,
                    "publication-style",
                    "styles/publication.css",
                    "text/css",
                    null);

                foreach (var section in
                         sections)
                {
                    WriteManifestItem(
                        writer,
                        $"section-{section.Ordinal:D4}",
                        section.FileName,
                        "application/xhtml+xml",
                        null);
                }

                for (var index = 0;
                     index < visualEntries.Count;
                     index++)
                {
                    var visualEntry =
                        visualEntries[index];

                    WriteManifestItem(
                        writer,
                        $"visual-{index + 1:D4}",
                        visualEntry.FileName,
                        visualEntry.Asset.MediaType,
                        null);
                }

                writer.WriteEndElement();

                writer.WriteStartElement(
                    "spine",
                    OpfNamespace);

                foreach (var section in
                         sections)
                {
                    writer.WriteStartElement(
                        "itemref",
                        OpfNamespace);

                    writer.WriteAttributeString(
                        "idref",
                        $"section-{section.Ordinal:D4}");

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            });

    private static void WriteDublinCoreElement(
        XmlWriter writer,
        string localName,
        string value)
    {
        writer.WriteStartElement(
            "dc",
            localName,
            DublinCoreNamespace);

        writer.WriteString(
            value);

        writer.WriteEndElement();
    }

    private static void WriteManifestItem(
        XmlWriter writer,
        string id,
        string href,
        string mediaType,
        string? properties)
    {
        writer.WriteStartElement(
            "item",
            OpfNamespace);

        writer.WriteAttributeString(
            "id",
            id);

        writer.WriteAttributeString(
            "href",
            href);

        writer.WriteAttributeString(
            "media-type",
            mediaType);

        if (properties is not null)
        {
            writer.WriteAttributeString(
                "properties",
                properties);
        }

        writer.WriteEndElement();
    }

    private static string CreateNavigationDocument(
        EpubPublicationMetadata metadata,
        IReadOnlyList<ContentSection> sections) =>
        CreateXml(
            writer =>
            {
                WriteXhtmlDocumentStart(
                    writer,
                    metadata,
                    "Table of contents");

                writer.WriteStartElement(
                    "nav",
                    XhtmlNamespace);

                writer.WriteAttributeString(
                    "epub",
                    "type",
                    EpubNamespace,
                    "toc");

                writer.WriteStartElement(
                    "h1",
                    XhtmlNamespace);

                writer.WriteString(
                    metadata.Title);

                writer.WriteEndElement();

                writer.WriteStartElement(
                    "ol",
                    XhtmlNamespace);

                var navigationSections =
                    sections.Where(
                            section =>
                                section.HeadingText is not null)
                        .ToArray();

                if (navigationSections.Length == 0)
                {
                    WriteNavigationItem(
                        writer,
                        sections[0].FileName,
                        metadata.Title);
                }
                else
                {
                    foreach (var section in
                             navigationSections)
                    {
                        WriteNavigationItem(
                            writer,
                            $"{section.FileName}#section-heading",
                            section.HeadingText!);
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

    private static void WriteNavigationItem(
        XmlWriter writer,
        string href,
        string label)
    {
        writer.WriteStartElement(
            "li",
            XhtmlNamespace);

        writer.WriteStartElement(
            "a",
            XhtmlNamespace);

        writer.WriteAttributeString(
            "href",
            href);

        writer.WriteString(
            label);

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string CreateContentDocument(
        EpubPublicationMetadata metadata,
        ContentSection section,
        IReadOnlyDictionary<string, VisualEntry> visualEntriesByElementId) =>
        CreateXml(
            writer =>
            {
                WriteXhtmlDocumentStart(
                    writer,
                    metadata,
                    section.HeadingText ??
                    metadata.Title);

                writer.WriteStartElement(
                    "section",
                    XhtmlNamespace);

                var headingAnchorWritten =
                    false;

                foreach (var element in
                         section.Elements)
                {
                    switch (element.Kind)
                    {
                        case DocumentElementKind.Heading:
                            writer.WriteStartElement(
                                "h1",
                                XhtmlNamespace);

                            if (!headingAnchorWritten)
                            {
                                writer.WriteAttributeString(
                                    "id",
                                    "section-heading");

                                headingAnchorWritten =
                                    true;
                            }

                            writer.WriteString(
                                element.Text!);

                            writer.WriteEndElement();
                            break;

                        case DocumentElementKind.Text:
                            WriteTextParagraph(
                                writer,
                                element.Text!,
                                null);
                            break;

                        case DocumentElementKind.Caption:
                            WriteTextParagraph(
                                writer,
                                element.Text!,
                                "caption");
                            break;

                        case DocumentElementKind.Visual:
                            WriteVisual(
                                writer,
                                visualEntriesByElementId[element.ElementId]);
                            break;
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

    private static void WriteXhtmlDocumentStart(
        XmlWriter writer,
        EpubPublicationMetadata metadata,
        string title)
    {
        writer.WriteStartElement(
            "html",
            XhtmlNamespace);

        writer.WriteAttributeString(
            "lang",
            metadata.Language);

        writer.WriteAttributeString(
            "xml",
            "lang",
            null,
            metadata.Language);

        writer.WriteStartElement(
            "head",
            XhtmlNamespace);

        writer.WriteStartElement(
            "title",
            XhtmlNamespace);

        writer.WriteString(
            title);

        writer.WriteEndElement();

        writer.WriteStartElement(
            "link",
            XhtmlNamespace);

        writer.WriteAttributeString(
            "rel",
            "stylesheet");

        writer.WriteAttributeString(
            "href",
            "styles/publication.css");

        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement(
            "body",
            XhtmlNamespace);
    }

    private static void WriteTextParagraph(
        XmlWriter writer,
        string text,
        string? cssClass)
    {
        writer.WriteStartElement(
            "p",
            XhtmlNamespace);

        if (cssClass is not null)
        {
            writer.WriteAttributeString(
                "class",
                cssClass);
        }

        writer.WriteString(
            text);

        writer.WriteEndElement();
    }

    private static void WriteVisual(
        XmlWriter writer,
        VisualEntry visualEntry)
    {
        writer.WriteStartElement(
            "figure",
            XhtmlNamespace);

        writer.WriteStartElement(
            "img",
            XhtmlNamespace);

        writer.WriteAttributeString(
            "src",
            visualEntry.FileName);

        writer.WriteAttributeString(
            "alt",
            "Preserved visual from the source document");

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string CreateStylesheet() =>
        """
        body {
          line-height: 1.45;
          margin: 5%;
        }

        p {
          margin: 0 0 0.8em 0;
        }

        h1 {
          break-before: page;
          margin: 1.5em 0 0.8em 0;
        }

        figure {
          break-inside: avoid;
          margin: 1em auto;
          text-align: center;
        }

        img {
          height: auto;
          max-width: 100%;
        }

        .caption {
          font-size: 0.9em;
          font-style: italic;
          text-align: center;
        }
        """;

    private static string CreateXml(
        Action<XmlWriter> write)
    {
        using var output =
            new MemoryStream();

        using (var writer =
               XmlWriter.Create(
                   output,
                   new XmlWriterSettings
                   {
                       Encoding = Utf8WithoutBom,
                       Indent = true,
                       NewLineChars = "\n",
                       OmitXmlDeclaration = false
                   }))
        {
            write(
                writer);
        }

        return Utf8WithoutBom.GetString(
            output.ToArray());
    }

    private static void WriteTextEntry(
        ZipArchive archive,
        string entryName,
        string content,
        CompressionLevel compressionLevel,
        DateTimeOffset timestamp)
    {
        var entry =
            archive.CreateEntry(
                entryName,
                compressionLevel);

        entry.LastWriteTime =
            timestamp;

        using var entryStream =
            entry.Open();

        using var writer =
            new StreamWriter(
                entryStream,
                Utf8WithoutBom,
                bufferSize:
                    1024,
                leaveOpen:
                    false);

        writer.Write(
            content);
    }

    private static async Task WriteVisualEntryAsync(
        ZipArchive archive,
        VisualEntry visualEntry,
        DocumentElement element,
        EpubVisualAssetReader visualAssetReader,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var source =
            await visualAssetReader(
                    element,
                    visualEntry.Asset,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (source is null ||
            !source.CanRead)
        {
            if (source is not null)
            {
                await source.DisposeAsync()
                    .ConfigureAwait(
                        false);
            }

            throw new InvalidOperationException(
                $"Visual asset reader returned no readable stream for '{visualEntry.Asset.AssetId}'.");
        }

        await using (source.ConfigureAwait(
                         false))
        {
            var entry =
                archive.CreateEntry(
                    $"OEBPS/{visualEntry.FileName}",
                    CompressionLevel.Optimal);

            entry.LastWriteTime =
                timestamp;

            await using var entryStream =
                entry.Open();

            using var hash =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

            var buffer =
                new byte[81920];

            long byteCount =
                0;

            int bytesRead;

            while ((bytesRead =
                    await source.ReadAsync(
                            buffer,
                            cancellationToken)
                        .ConfigureAwait(
                            false)) >
                   0)
            {
                await entryStream.WriteAsync(
                        buffer.AsMemory(
                            0,
                            bytesRead),
                        cancellationToken)
                    .ConfigureAwait(
                        false);

                hash.AppendData(
                    buffer,
                    0,
                    bytesRead);

                byteCount +=
                    bytesRead;
            }

            var actualSha256 =
                Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant();

            if (byteCount !=
                visualEntry.Asset.ContentLength ||
                !string.Equals(
                    actualSha256,
                    visualEntry.Asset.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Visual asset '{visualEntry.Asset.AssetId}' bytes do not match the processing result.");
            }
        }
    }

    private static DateTimeOffset NormalizeZipTimestamp(
        DateTimeOffset timestamp)
    {
        var utc =
            timestamp.ToUniversalTime();

        var minimum =
            new DateTimeOffset(
                1980,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var maximum =
            new DateTimeOffset(
                2107,
                12,
                31,
                23,
                59,
                58,
                TimeSpan.Zero);

        return utc < minimum
            ? minimum
            : utc > maximum
                ? maximum
                : utc;
    }

    private sealed record ContentSection(
        int Ordinal,
        string FileName,
        string? HeadingText,
        IReadOnlyList<DocumentElement> Elements);

    private sealed record VisualEntry(
        DocumentVisualAsset Asset,
        string FileName);
}
