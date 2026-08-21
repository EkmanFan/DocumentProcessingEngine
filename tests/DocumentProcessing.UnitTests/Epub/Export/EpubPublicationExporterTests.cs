using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Epub.Export;
using DocumentProcessing.Epub.Locations;

namespace DocumentProcessing.UnitTests.Epub.Export;

public sealed class EpubPublicationExporterTests
{
    private static readonly byte[] VisualBytes =
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAACCAIAAAC2fEmeAAAADElEQVQI12NgGOoAAADCAAHhxfJhAAAAAElFTkSuQmCC");

    [Fact]
    public async Task ExportAsync_WritesReflowablePublicationAndVerifiedVisual()
    {
        var document =
            CreateDocument();

        using var output =
            new MemoryStream();

        var callbackCount =
            0;

        var result =
            await new EpubPublicationExporter()
                .ExportAsync(
                    document,
                    CreateMetadata(),
                    output,
                    (element, asset, _) =>
                    {
                        callbackCount++;

                        Assert.Equal(
                            "visual-element",
                            element.ElementId);

                        Assert.Equal(
                            "visual-asset",
                            asset.AssetId);

                        return ValueTask.FromResult<Stream>(
                            new MemoryStream(
                                VisualBytes,
                                writable:
                                    false));
                    });

        Assert.Equal(
            1,
            callbackCount);

        Assert.Equal(
            "urn:test:publication",
            result.Identifier);

        Assert.Equal(
            1,
            result.ContentDocumentCount);

        Assert.Equal(
            1,
            result.VisualAssetCount);

        Assert.Equal(
            1,
            result.OmittedElementCount);

        Assert.True(
            output.CanWrite);

        AssertMimetypeLocalHeader(
            output.ToArray());

        output.Position =
            0;

        using var archive =
            new ZipArchive(
                output,
                ZipArchiveMode.Read,
                leaveOpen:
                    true);

        Assert.Equal(
            "mimetype",
            archive.Entries[0].FullName);

        Assert.Equal(
            "application/epub+zip",
            ReadText(
                archive,
                "mimetype"));

        var package =
            XDocument.Parse(
                ReadText(
                    archive,
                    "OEBPS/content.opf"));

        XNamespace opf =
            "http://www.idpf.org/2007/opf";

        XNamespace dc =
            "http://purl.org/dc/elements/1.1/";

        Assert.Equal(
            "Prototype title",
            package.Descendants(
                    dc +
                    "title")
                .Single()
                .Value);

        Assert.Contains(
            package.Descendants(
                opf +
                "item"),
            item =>
                (string?)item.Attribute(
                    "href") ==
                "images/visual-0001.png");

        var content =
            ReadText(
                archive,
                "OEBPS/section-0001.xhtml");

        Assert.Contains(
            "Chapter &amp; one",
            content,
            StringComparison.Ordinal);

        Assert.Contains(
            "A paragraph with &lt;markup&gt;.",
            content,
            StringComparison.Ordinal);

        Assert.Contains(
            "images/visual-0001.png",
            content,
            StringComparison.Ordinal);

        Assert.Equal(
            VisualBytes,
            ReadBytes(
                archive,
                "OEBPS/images/visual-0001.png"));
    }

    [Fact]
    public async Task ExportAsync_WithFixedMetadataIsByteDeterministic()
    {
        var first =
            await ExportAsync(
                VisualBytes);

        var second =
            await ExportAsync(
                VisualBytes);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public async Task ExportAsync_WhenVisualBytesDoNotMatchRejectsPublication()
    {
        using var output =
            new MemoryStream();

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    new EpubPublicationExporter()
                        .ExportAsync(
                            CreateDocument(),
                            CreateMetadata(),
                            output,
                            (_, _, _) =>
                                ValueTask.FromResult<Stream>(
                                    new MemoryStream(
                                        [1, 2, 3]))));

        Assert.Contains(
            "do not match",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WhenVisualReaderIsMissingRejectsPublication()
    {
        using var output =
            new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                new EpubPublicationExporter()
                    .ExportAsync(
                        CreateDocument(),
                        CreateMetadata(),
                        output));
    }

    private static async Task<byte[]> ExportAsync(
        byte[] visualBytes)
    {
        using var output =
            new MemoryStream();

        await new EpubPublicationExporter()
            .ExportAsync(
                CreateDocument(),
                CreateMetadata(),
                output,
                (_, _, _) =>
                    ValueTask.FromResult<Stream>(
                        new MemoryStream(
                            visualBytes,
                            writable:
                                false)));

        return output.ToArray();
    }

    private static EpubPublicationMetadata CreateMetadata() =>
        new(
            title:
                "Prototype title",
            language:
                "en",
            creator:
                "Prototype author",
            identifier:
                "urn:test:publication",
            modifiedAtUtc:
                new DateTimeOffset(
                    2026,
                    8,
                    22,
                    12,
                    0,
                    0,
                    TimeSpan.Zero));

    private static DocumentProcessingResult CreateDocument()
    {
        var heading =
            CreateTextElement(
                "heading",
                ordinal:
                    0,
                DocumentElementKind.Heading,
                "Chapter & one",
                blockIndex:
                    0);

        var paragraph =
            CreateTextElement(
                "paragraph",
                ordinal:
                    1,
                DocumentElementKind.Text,
                "A paragraph with <markup>.",
                blockIndex:
                    1);

        var visual =
            new DocumentElement(
                elementId:
                    "visual-element",
                ordinal:
                    2,
                DocumentElementKind.Visual,
                new EpubDocumentSourceLocation(
                    spineIndex:
                        0,
                    resourcePath:
                        "chapter.xhtml",
                    blockIndex:
                        2),
                segmentId:
                    null,
                text:
                    null,
                textSha256:
                    null);

        var unresolved =
            new DocumentElement(
                elementId:
                    "unresolved",
                ordinal:
                    3,
                DocumentElementKind.UnresolvedText,
                new EpubDocumentSourceLocation(
                    spineIndex:
                        0,
                    resourcePath:
                        "chapter.xhtml",
                    blockIndex:
                        3),
                segmentId:
                    null,
                text:
                    null,
                textSha256:
                    null);

        var visualSha256 =
            ComputeSha256(
                VisualBytes);

        return new DocumentProcessingResult(
            new DocumentSourceDescriptor(
                new DocumentFormatId(
                    "pdf"),
                new string(
                    'a',
                    64),
                byteLength:
                    1024,
                fileName:
                    "prototype.pdf",
                declaredMediaType:
                    "application/pdf"),
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "test-native",
                        "test-native-v1"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    ["test-visual-v1"],
                assemblyProfileId:
                    "test-assembly-v1",
                normalizationProfileId:
                    "test-normalization-v1",
                segmentationProfileId:
                    "test-segmentation-v1"),
            [
                heading,
                paragraph,
                visual,
                unresolved
            ],
            [
                CreateEvidence(
                    heading),
                CreateEvidence(
                    paragraph),
                CreateEvidence(
                    unresolved)
            ],
            structuralSegments:
                [],
            segmentProcessingEvidence:
                [],
            [
                new DocumentVisualAsset(
                    assetId:
                        "visual-asset",
                    elementId:
                        visual.ElementId,
                    preservationProfileId:
                        "test-visual-v1",
                    mediaType:
                        "image/png",
                    contentLength:
                        VisualBytes.Length,
                    visualSha256)
            ],
            DocumentProcessingQualityObservations.Empty);
    }

    private static DocumentElement CreateTextElement(
        string elementId,
        int ordinal,
        DocumentElementKind kind,
        string text,
        int blockIndex) =>
        new(
            elementId,
            ordinal,
            kind,
            new EpubDocumentSourceLocation(
                spineIndex:
                    0,
                resourcePath:
                    "chapter.xhtml",
                blockIndex),
            segmentId:
                null,
            text,
            ProvenanceTextHashing.ComputeUtf8Sha256(
                text));

    private static DocumentElementProcessingEvidence CreateEvidence(
        DocumentElement element)
    {
        var isResolved =
            element.Text is not null;

        return new DocumentElementProcessingEvidence(
            element.ElementId,
            isResolved
                ? DocumentTextSourceKind.Native
                : DocumentTextSourceKind.None,
            selectedSourceText:
                element.Text,
            selectedSourceTextSha256:
                element.TextSha256,
            nativeCandidateSequence:
                isResolved
                    ? element.Ordinal
                    : null,
            layoutCandidateSequence:
                null,
            ocrBackendId:
                null,
            ocrProfileId:
                null,
            reconciliationDecision:
                null,
            textsEquivalent:
                null,
            hasReconciliationDivergence:
                false,
            selectedTextPreparation:
                null,
            normalizationDehyphenation:
                null,
            normalizationChangedText:
                false,
            exclusionReason:
                null,
            isResolved,
            layoutKind:
                null);
    }

    private static string ReadText(
        ZipArchive archive,
        string entryName) =>
        Encoding.UTF8.GetString(
            ReadBytes(
                archive,
                entryName));

    private static byte[] ReadBytes(
        ZipArchive archive,
        string entryName)
    {
        var entry =
            archive.GetEntry(
                entryName) ??
            throw new InvalidOperationException(
                $"Missing EPUB entry '{entryName}'.");

        using var input =
            entry.Open();

        using var output =
            new MemoryStream();

        input.CopyTo(
            output);

        return output.ToArray();
    }

    private static void AssertMimetypeLocalHeader(
        byte[] epubBytes)
    {
        Assert.Equal(
            0x04034b50u,
            BitConverter.ToUInt32(
                epubBytes,
                0));

        Assert.Equal(
            0,
            BitConverter.ToUInt16(
                epubBytes,
                8));

        var fileNameLength =
            BitConverter.ToUInt16(
                epubBytes,
                26);

        var extraFieldLength =
            BitConverter.ToUInt16(
                epubBytes,
                28);

        Assert.Equal(
            8,
            fileNameLength);

        Assert.Equal(
            0,
            extraFieldLength);

        Assert.Equal(
            "mimetype",
            Encoding.ASCII.GetString(
                epubBytes,
                30,
                fileNameLength));
    }

    private static string ComputeSha256(
        byte[] value) =>
        Convert.ToHexString(
                SHA256.HashData(
                    value))
            .ToLowerInvariant();
}
