using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Raster;

public sealed class RasterExecutionContractTests
{
    private const string Sha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void FullPageResult_RequiresOutputToMatchPageDimensions()
    {
        var result =
            new RasterRenderResult(
                physicalPageNumber:
                    2,
                sourcePagePixelWidth:
                    1200,
                sourcePagePixelHeight:
                    1600,
                crop:
                    null,
                outputPixelWidth:
                    1200,
                outputPixelHeight:
                    1600,
                mediaType:
                    "image/png",
                profileId:
                    "profile-v1",
                contentLength:
                    123,
                contentSha256:
                    Sha);

        Assert.True(
            result.IsFullPage);

        Assert.Null(
            result.Crop);

        Assert.Throws<ArgumentException>(
            () =>
                new RasterRenderResult(
                    physicalPageNumber:
                        2,
                    sourcePagePixelWidth:
                        1200,
                    sourcePagePixelHeight:
                        1600,
                    crop:
                        null,
                    outputPixelWidth:
                        1000,
                    outputPixelHeight:
                        1600,
                    mediaType:
                        "image/png",
                    profileId:
                        "profile-v1",
                    contentLength:
                        123,
                    contentSha256:
                        Sha));
    }

    [Fact]
    public void RegionResult_RequiresExactCropDimensionsAndBounds()
    {
        var crop =
            new PixelRectangle(
                100,
                200,
                500,
                700);

        var result =
            new RasterRenderResult(
                physicalPageNumber:
                    1,
                sourcePagePixelWidth:
                    1000,
                sourcePagePixelHeight:
                    1200,
                crop,
                outputPixelWidth:
                    400,
                outputPixelHeight:
                    500,
                mediaType:
                    "image/png",
                profileId:
                    "profile-v1",
                contentLength:
                    123,
                contentSha256:
                    Sha);

        Assert.False(
            result.IsFullPage);

        Assert.Equal(
            crop,
            result.Crop);

        Assert.Throws<ArgumentException>(
            () =>
                new RasterRenderResult(
                    physicalPageNumber:
                        1,
                    sourcePagePixelWidth:
                        1000,
                    sourcePagePixelHeight:
                        1200,
                    crop,
                    outputPixelWidth:
                        399,
                    outputPixelHeight:
                        500,
                    mediaType:
                        "image/png",
                    profileId:
                        "profile-v1",
                    contentLength:
                        123,
                    contentSha256:
                        Sha));

        Assert.Throws<ArgumentException>(
            () =>
                new RasterRenderResult(
                    physicalPageNumber:
                        1,
                    sourcePagePixelWidth:
                        450,
                    sourcePagePixelHeight:
                        1200,
                    crop,
                    outputPixelWidth:
                        400,
                    outputPixelHeight:
                        500,
                    mediaType:
                        "image/png",
                    profileId:
                        "profile-v1",
                    contentLength:
                        123,
                    contentSha256:
                        Sha));
    }

    [Fact]
    public void PdftoppmRasterizer_AdvertisesOnlyPdfCapability()
    {
        var rasterizer =
            new PdftoppmDocumentRasterizer();

        Assert.True(
            rasterizer.CanRasterize(
                DocumentFormatId.Pdf));

        Assert.False(
            rasterizer.CanRasterize(
                new DocumentFormatId(
                    "epub")));
    }

    [Fact]
    public async Task OpenAsync_MaterializesOnceAndRestoresSeekableCallerPosition()
    {
        var content =
            new MemoryStream(
                "synthetic-pdf-bytes"u8.ToArray());

        content.Position =
            5;

        var source =
            new DocumentSource(
                content,
                "source.pdf",
                "application/pdf");

        var rasterizer =
            new PdftoppmDocumentRasterizer(
                maxSourceBytes:
                    1024);

        await using var session =
            await rasterizer
                .OpenAsync(
                    source,
                    DocumentFormatId.Pdf);

        Assert.Equal(
            5,
            content.Position);

        Assert.Equal(
            "pdftoppm",
            session.BackendId);

        Assert.Equal(
            300,
            session.Dpi);

        Assert.Equal(
            "pdftoppm-300dpi-rgb-png-direct-crop-v1",
            session.ProfileId);
    }

    [Fact]
    public async Task OpenAsync_RejectsSourceOverConfiguredBoundAndRestoresPosition()
    {
        var content =
            new MemoryStream(
                new byte[32]);

        content.Position =
            7;

        var rasterizer =
            new PdftoppmDocumentRasterizer(
                maxSourceBytes:
                    16);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var _ =
                    await rasterizer
                        .OpenAsync(
                            new DocumentSource(
                                content,
                                "source.pdf",
                                "application/pdf"),
                            DocumentFormatId.Pdf);
            });

        Assert.Equal(
            7,
            content.Position);
    }

    [Fact]
    public async Task OpenAsync_RejectsUnsupportedFormatBeforeMaterialization()
    {
        var content =
            new MemoryStream(
                "content"u8.ToArray());

        var rasterizer =
            new PdftoppmDocumentRasterizer();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () =>
            {
                await using var _ =
                    await rasterizer
                        .OpenAsync(
                            new DocumentSource(
                                content,
                                "source.epub",
                                "application/epub+zip"),
                            new DocumentFormatId(
                                "epub"));
            });

        Assert.Equal(
            0,
            content.Position);
    }
}
