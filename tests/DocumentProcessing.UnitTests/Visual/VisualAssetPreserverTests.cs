using System.Security.Cryptography;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Raster;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Visual;

public sealed class VisualAssetPreserverTests
{
    [Fact]
    public async Task PreserveAsync_Figure_CopiesBytesAndReturnsIntegrityEvidence()
    {
        var bytes =
            new byte[] { 10, 20, 30, 40, 50 };

        await using var source =
            new MemoryStream(
                bytes,
                writable: false);
        await using var destination =
            new MemoryStream();

        var figure =
            FigureObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                2000);

        var preserver =
            new VisualAssetPreserver();

        var result =
            await preserver.PreserveAsync(
                source,
                destination,
                SourceDocumentSha256,
                ProfileId,
                "image/png",
                figure,
                crop,
                1000,
                2000);

        Assert.Equal(
            SourceDocumentSha256,
            result.SourceDocumentSha256);
        Assert.Equal(
            ProfileId,
            result.ProfileId);
        Assert.Equal(
            "image/png",
            result.MediaType);
        Assert.Same(
            figure,
            result.SourceLayoutObservation);
        Assert.Equal(
            1000,
            result.SourceRasterPixelWidth);
        Assert.Equal(
            2000,
            result.SourceRasterPixelHeight);
        Assert.Equal(
            crop,
            result.Crop);
        Assert.Equal(
            bytes.Length,
            result.ContentLength);
        Assert.Equal(
            Convert.ToHexString(
                    SHA256.HashData(bytes))
                .ToLowerInvariant(),
            result.ContentSha256);
        Assert.Equal(
            bytes,
            destination.ToArray());
        Assert.Equal(
            0,
            source.Position);
    }

    [Fact]
    public async Task PreserveAsync_SeekableSourceFromCurrentPosition_RestoresPosition()
    {
        var bytes =
            new byte[] { 1, 2, 3, 4, 5 };

        await using var source =
            new MemoryStream(
                bytes,
                writable: false);
        source.Position = 2;

        await using var destination =
            new MemoryStream();

        var figure =
            FigureObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                2000);

        var result =
            await new VisualAssetPreserver()
                .PreserveAsync(
                    source,
                    destination,
                    SourceDocumentSha256,
                    ProfileId,
                    "image/png",
                    figure,
                    crop,
                    1000,
                    2000);

        var expected =
            bytes[2..];

        Assert.Equal(
            expected,
            destination.ToArray());
        Assert.Equal(
            expected.Length,
            result.ContentLength);
        Assert.Equal(
            2,
            source.Position);
    }

    [Fact]
    public async Task PreserveAsync_TextRegion_DoesNotApplySemanticAuthorization()
    {
        var bytes =
            new byte[] { 1, 2, 3 };

        await using var source =
            new MemoryStream(
                bytes,
                writable: false);

        await using var destination =
            new MemoryStream();

        var text =
            new LayoutObservation(
                physicalPageNumber: 233,
                observationSequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.1,
                    0.2,
                    0.4,
                    0.3),
                rawLabel: "text");

        var crop =
            RasterCropGeometry.FromNormalized(
                text.Bounds,
                1000,
                2000);

        var result =
            await new VisualAssetPreserver()
                .PreserveAsync(
                    source,
                    destination,
                    SourceDocumentSha256,
                    ProfileId,
                    "image/png",
                    text,
                    crop,
                    1000,
                    2000);

        Assert.Equal(
            bytes,
            destination.ToArray());

        Assert.Same(
            text,
            result.SourceLayoutObservation);
    }

    [Fact]
    public async Task PreserveAsync_MismatchedCrop_FailsBeforeWriting()
    {
        await using var source =
            new MemoryStream(
                new byte[] { 1, 2, 3 },
                writable: false);
        await using var destination =
            new MemoryStream();

        var figure =
            FigureObservation();

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await new VisualAssetPreserver()
                    .PreserveAsync(
                        source,
                        destination,
                        SourceDocumentSha256,
                        ProfileId,
                        "image/png",
                        figure,
                        new PixelRectangle(
                            0,
                            0,
                            10,
                            10),
                        1000,
                        2000)
                    .AsTask());

        Assert.Empty(
            destination.ToArray());
    }

    [Fact]
    public async Task PreserveAsync_OversizedSeekableInput_FailsBeforeWriting()
    {
        await using var source =
            new MemoryStream(
                new byte[] { 1, 2, 3, 4 },
                writable: false);
        await using var destination =
            new MemoryStream();

        var figure =
            FigureObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                2000);

        var preserver =
            new VisualAssetPreserver(
                maxInputBytes: 3);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await preserver
                    .PreserveAsync(
                        source,
                        destination,
                        SourceDocumentSha256,
                        ProfileId,
                        "image/png",
                        figure,
                        crop,
                        1000,
                        2000)
                    .AsTask());

        Assert.Empty(
            destination.ToArray());
    }

    [Fact]
    public async Task PreserveAsync_EmptySeekableInput_FailsBeforeWriting()
    {
        await using var source =
            new MemoryStream(
                Array.Empty<byte>(),
                writable: false);
        await using var destination =
            new MemoryStream();

        var figure =
            FigureObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                2000);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await new VisualAssetPreserver()
                    .PreserveAsync(
                        source,
                        destination,
                        SourceDocumentSha256,
                        ProfileId,
                        "image/png",
                        figure,
                        crop,
                        1000,
                        2000)
                    .AsTask());
    }

    [Fact]
    public async Task PreserveAsync_NonEmptySeekableDestination_IsRejected()
    {
        await using var source =
            new MemoryStream(
                new byte[] { 1, 2, 3 },
                writable: false);
        await using var destination =
            new MemoryStream(
                new byte[] { 9 },
                writable: true);

        var figure =
            FigureObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                2000);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await new VisualAssetPreserver()
                    .PreserveAsync(
                        source,
                        destination,
                        SourceDocumentSha256,
                        ProfileId,
                        "image/png",
                        figure,
                        crop,
                        1000,
                        2000)
                    .AsTask());
    }

    private static LayoutObservation FigureObservation() =>
        new(
            physicalPageNumber: 233,
            observationSequence: 4,
            readingOrder: 4,
            LayoutObservationKind.Figure,
            new NormalizedRectangle(
                0.236697,
                0.429652,
                0.582942,
                0.865355),
            rawLabel: "image");

    private const string SourceDocumentSha256 =
        "f4600ad840fea7e6edf68c74244f71fec07335e792e228db1265b1619da19bbe";

    private const string ProfileId =
        "pdftoppm-26.01.0-300dpi-rgb-png-v1";
}
