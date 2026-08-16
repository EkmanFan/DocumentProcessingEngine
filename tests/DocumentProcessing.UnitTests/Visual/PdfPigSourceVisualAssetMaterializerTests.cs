using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.UnitTests.Visual;

public sealed class PdfPigSourceVisualAssetMaterializerTests
{
    private static readonly byte[] ExpectedPngSignature =
    [
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    [Fact]
    public void Materialization_NormalizesProfileMediaTypeAndSha()
    {
        var result =
            new SourceVisualAssetMaterialization(
                physicalPageNumber:
                    2,
                sourceVisualIndex:
                    3,
                declaredPageBounds:
                    new NormalizedRectangle(
                        0.1,
                        0.2,
                        0.7,
                        0.8),
                profileId:
                    "  profile-v1  ",
                mediaType:
                    " IMAGE/PNG ",
                contentLength:
                    12,
                contentSha256:
                    new string(
                        'A',
                        64));

        Assert.Equal(
            2,
            result.PhysicalPageNumber);

        Assert.Equal(
            3,
            result.SourceVisualIndex);

        Assert.Equal(
            "profile-v1",
            result.ProfileId);

        Assert.Equal(
            "image/png",
            result.MediaType);

        Assert.Equal(
            new string(
                'a',
                64),
            result.ContentSha256);
    }

    [Fact]
    public void Materialization_RejectsInvalidIntegrityMetadata()
    {
        var bounds =
            new NormalizedRectangle(
                0,
                0,
                1,
                1);

        Assert.Throws<ArgumentException>(
            () =>
                new SourceVisualAssetMaterialization(
                    1,
                    0,
                    bounds,
                    "profile",
                    "application/octet-stream",
                    1,
                    new string(
                        'a',
                        64)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new SourceVisualAssetMaterialization(
                    1,
                    0,
                    bounds,
                    "profile",
                    "image/png",
                    0,
                    new string(
                        'a',
                        64)));

        Assert.Throws<ArgumentException>(
            () =>
                new SourceVisualAssetMaterialization(
                    1,
                    0,
                    bounds,
                    "profile",
                    "image/png",
                    1,
                    "not-a-sha"));
    }

    [Fact]
    public async Task PdfMaterializer_MaterializesExactSourceOccurrenceAsStandalonePng()
    {
        var pdfBytes =
            BuildPdfWithEmbeddedPng();

        await using var sourceStream =
            new MemoryStream(
                pdfBytes);

        var source =
            new DocumentSource(
                sourceStream,
                "generated.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        Assert.Equal(
            1,
            Assert.Single(
                extraction.Pages)
                .RasterImageCount);

        sourceStream.Position =
            7;

        await using var destination =
            new MemoryStream();

        var materializer =
            new PdfPigSourceVisualAssetMaterializer();

        var result =
            await materializer
                .MaterializeAsync(
                    source,
                    DocumentFormatId.Pdf,
                    extraction,
                    physicalPageNumber:
                        1,
                    sourceVisualIndex:
                        0,
                    destination);

        Assert.Equal(
            7,
            sourceStream.Position);

        Assert.Equal(
            1,
            result.PhysicalPageNumber);

        Assert.Equal(
            0,
            result.SourceVisualIndex);

        Assert.Equal(
            PdfPigSourceVisualAssetMaterializer.PngProfileId,
            result.ProfileId);

        Assert.Equal(
            "image/png",
            result.MediaType);

        Assert.Equal(
            destination.Length,
            result.ContentLength);

        Assert.Equal(
            Convert
                .ToHexString(
                    SHA256.HashData(
                        destination.ToArray()))
                .ToLowerInvariant(),
            result.ContentSha256);

        Assert.Equal(
            ExpectedPngSignature,
            destination
                .ToArray()
                .Take(
                    ExpectedPngSignature.Length));

        Assert.Equal(
            0.25,
            result.DeclaredPageBounds.Left,
            precision:
                12);

        Assert.Equal(
            0.25,
            result.DeclaredPageBounds.Top,
            precision:
                12);

        Assert.Equal(
            0.75,
            result.DeclaredPageBounds.Right,
            precision:
                12);

        Assert.Equal(
            0.75,
            result.DeclaredPageBounds.Bottom,
            precision:
                12);
    }

    [Fact]
    public async Task PdfMaterializer_PreservesStandaloneEmbeddedJpegBytesExactly()
    {
        var jpegBytes =
            Convert.FromBase64String(
                "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAADAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD58ooor83P10//2Q==");

        var builder =
            new PdfDocumentBuilder();

        var page =
            builder.AddPage(
                400,
                400);

        page.AddJpeg(
            jpegBytes,
            new PdfRectangle(
                100,
                50,
                300,
                350));

        await using var sourceStream =
            new MemoryStream(
                builder.Build());

        var source =
            new DocumentSource(
                sourceStream,
                "embedded-jpeg.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        Assert.Equal(
            1,
            Assert.Single(
                extraction.Pages)
                .RasterImageCount);

        await using var destination =
            new MemoryStream();

        var result =
            await new PdfPigSourceVisualAssetMaterializer()
                .MaterializeAsync(
                    source,
                    DocumentFormatId.Pdf,
                    extraction,
                    physicalPageNumber:
                        1,
                    sourceVisualIndex:
                        0,
                    destination);

        Assert.Equal(
            PdfPigSourceVisualAssetMaterializer.RawJpegProfileId,
            result.ProfileId);

        Assert.Equal(
            "image/jpeg",
            result.MediaType);

        Assert.Equal(
            jpegBytes,
            destination.ToArray());

        Assert.Equal(
            jpegBytes.LongLength,
            result.ContentLength);

        Assert.Equal(
            Convert
                .ToHexString(
                    SHA256.HashData(
                        jpegBytes))
                .ToLowerInvariant(),
            result.ContentSha256);
    }

    [Fact]
    public async Task PdfMaterializer_RejectsUnsupportedFormat()
    {
        await using var sourceStream =
            new MemoryStream(
                BuildPdfWithEmbeddedPng());

        var source =
            new DocumentSource(
                sourceStream);

        await using var destination =
            new MemoryStream();

        var unsupported =
            new DocumentFormatId(
                "text");

        await Assert.ThrowsAsync<NotSupportedException>(
            async () =>
                await new PdfPigSourceVisualAssetMaterializer()
                    .MaterializeAsync(
                        source,
                        unsupported,
                        new DocumentExtractionResult(
                            unsupported),
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            0,
                        destination));
    }

    [Fact]
    public async Task PdfMaterializer_RejectsImageCountDriftAgainstExtraction()
    {
        await using var sourceStream =
            new MemoryStream(
                BuildPdfWithEmbeddedPng());

        var source =
            new DocumentSource(
                sourceStream);

        var driftedExtraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    new DocumentExtractionPage(
                        physicalPageNumber:
                            1,
                        sourceText:
                            string.Empty,
                        wordCount:
                            0,
                        rasterImageCount:
                            2)
                ]);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await new PdfPigSourceVisualAssetMaterializer()
                    .MaterializeAsync(
                        source,
                        DocumentFormatId.Pdf,
                        driftedExtraction,
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            0,
                        destination));

        Assert.Equal(
            0,
            destination.Length);
    }

    [Fact]
    public async Task PdfMaterializer_RejectsMissingSourceVisualIndex()
    {
        var context =
            await CreateContextAsync();

        await using var sourceStream =
            context.SourceStream;

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await new PdfPigSourceVisualAssetMaterializer()
                    .MaterializeAsync(
                        context.Source,
                        DocumentFormatId.Pdf,
                        context.Extraction,
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            1,
                        destination));
    }

    [Fact]
    public async Task PdfMaterializer_RejectsNonEmptySeekableDestination()
    {
        var context =
            await CreateContextAsync();

        await using var sourceStream =
            context.SourceStream;

        await using var destination =
            new MemoryStream(
                [1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await new PdfPigSourceVisualAssetMaterializer()
                    .MaterializeAsync(
                        context.Source,
                        DocumentFormatId.Pdf,
                        context.Extraction,
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            0,
                        destination));
    }

    [Fact]
    public async Task PdfMaterializer_PreCancelledToken_PropagatesWithoutMutation()
    {
        var context =
            await CreateContextAsync();

        await using var sourceStream =
            context.SourceStream;

        sourceStream.Position =
            9;

        await using var destination =
            new MemoryStream();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await new PdfPigSourceVisualAssetMaterializer()
                    .MaterializeAsync(
                        context.Source,
                        DocumentFormatId.Pdf,
                        context.Extraction,
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            0,
                        destination,
                        cancellation.Token));

        Assert.Equal(
            9,
            sourceStream.Position);

        Assert.Equal(
            0,
            destination.Length);
    }

    [Fact]
    public async Task PdfMaterializer_OutputLimitFailsClosedAndRestoresSourcePosition()
    {
        var context =
            await CreateContextAsync();

        await using var sourceStream =
            context.SourceStream;

        sourceStream.Position =
            11;

        await using var destination =
            new MemoryStream();

        var materializer =
            new PdfPigSourceVisualAssetMaterializer(
                maxOutputBytes:
                    1);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await materializer
                    .MaterializeAsync(
                        context.Source,
                        DocumentFormatId.Pdf,
                        context.Extraction,
                        physicalPageNumber:
                            1,
                        sourceVisualIndex:
                            0,
                        destination));

        Assert.Equal(
            11,
            sourceStream.Position);

        Assert.Equal(
            0,
            destination.Length);

        Assert.Equal(
            0,
            destination.Position);
    }

    [Fact]
    public void PdfMaterializer_RejectsInvalidOperationalLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PdfPigSourceVisualAssetMaterializer(
                    maxSourcePixels:
                        0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PdfPigSourceVisualAssetMaterializer(
                    maxOutputBytes:
                        0));
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var sourceStream =
            new MemoryStream(
                BuildPdfWithEmbeddedPng());

        var source =
            new DocumentSource(
                sourceStream,
                "generated.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        return new TestContext(
            sourceStream,
            source,
            extraction);
    }

    private static byte[] BuildPdfWithEmbeddedPng()
    {
        var builder =
            new PdfDocumentBuilder();

        var page =
            builder.AddPage(
                400,
                400);

        page.AddPng(
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAMElEQVR4nGP8////fwYKABMlmkcNgAAWXBKMjIwofFyRNfBeoF0YEJtAB94Lw8AAANR7Ch2xuB6GAAAAAElFTkSuQmCC"),
            new PdfRectangle(
                100,
                100,
                300,
                300));

        return builder.Build();
    }

    private sealed record TestContext(
        MemoryStream SourceStream,
        DocumentSource Source,
        DocumentExtractionResult Extraction);
}
