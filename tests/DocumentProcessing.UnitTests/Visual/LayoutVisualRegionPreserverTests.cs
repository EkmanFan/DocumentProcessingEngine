using System.Security.Cryptography;
using System.Text;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Visual;

public sealed class LayoutVisualRegionPreserverTests
{
    [Fact]
    public async Task PreserveAsync_CaptionedMeaningfulVisual_PreservesExactRegion()
    {
        var evidence =
            Evidence(
                VisualEvidenceKind.CaptionedMeaningfulVisual);

        await using var rasterSession =
            new RecordingRasterizationSession();

        await using var destination =
            new MemoryStream();

        var preserved =
            await new LayoutVisualRegionPreserver()
                .PreserveAsync(
                    evidence,
                    rasterSession,
                    FullPageRaster(),
                    SourceSha,
                    destination);

        var expectedCrop =
            new PixelRectangle(
                left:
                    200,
                top:
                    360,
                right:
                    700,
                bottom:
                    960);

        Assert.Equal(
            1,
            rasterSession.RegionRenderCount);

        Assert.Equal(
            expectedCrop,
            rasterSession.LastCrop);

        Assert.Equal(
            expectedCrop,
            preserved.Crop);

        Assert.Equal(
            evidence.Observation,
            preserved.SourceLayoutObservation);

        Assert.Equal(
            RegionBytes,
            destination.ToArray());

        Assert.Equal(
            RegionBytes.Length,
            preserved.ContentLength);

        Assert.Equal(
            Hash(
                RegionBytes),
            preserved.ContentSha256);
    }

    [Theory]
    [InlineData(
        VisualEvidenceKind.Unknown)]
    [InlineData(
        VisualEvidenceKind.TinyOrNoise)]
    [InlineData(
        VisualEvidenceKind.NativeTextContainerOrFrame)]
    public async Task PreserveAsync_NonMeaningfulEvidence_FailsBeforeRasterIo(
        VisualEvidenceKind evidenceKind)
    {
        var evidence =
            Evidence(
                evidenceKind);

        await using var rasterSession =
            new RecordingRasterizationSession();

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await new LayoutVisualRegionPreserver()
                    .PreserveAsync(
                        evidence,
                        rasterSession,
                        FullPageRaster(),
                        SourceSha,
                        destination)
                    .AsTask());

        Assert.Equal(
            0,
            rasterSession.RegionRenderCount);

        Assert.Empty(
            destination.ToArray());
    }

    [Fact]
    public async Task PreserveAsync_LargeIndependentVisual_UsesSamePreserveDisposition()
    {
        var evidence =
            Evidence(
                VisualEvidenceKind.LargeIndependentVisual);

        await using var rasterSession =
            new RecordingRasterizationSession();

        await using var destination =
            new MemoryStream();

        var preserved =
            await new LayoutVisualRegionPreserver()
                .PreserveAsync(
                    evidence,
                    rasterSession,
                    FullPageRaster(),
                    SourceSha,
                    destination);

        Assert.Equal(
            1,
            rasterSession.RegionRenderCount);

        Assert.Equal(
            Hash(
                RegionBytes),
            preserved.ContentSha256);
    }

    [Fact]
    public async Task PreserveAsync_PageMismatch_FailsBeforeRasterIo()
    {
        var evidence =
            Evidence(
                VisualEvidenceKind.CaptionedMeaningfulVisual);

        await using var rasterSession =
            new RecordingRasterizationSession();

        await using var destination =
            new MemoryStream();

        var pageRaster =
            new RasterRenderResult(
                physicalPageNumber:
                    2,
                sourcePagePixelWidth:
                    1000,
                sourcePagePixelHeight:
                    1200,
                crop:
                    null,
                outputPixelWidth:
                    1000,
                outputPixelHeight:
                    1200,
                mediaType:
                    "image/png",
                profileId:
                    RecordingRasterizationSession.Profile,
                contentLength:
                    1,
                contentSha256:
                    PageSha);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await new LayoutVisualRegionPreserver()
                    .PreserveAsync(
                        evidence,
                        rasterSession,
                        pageRaster,
                        SourceSha,
                        destination)
                    .AsTask());

        Assert.Equal(
            0,
            rasterSession.RegionRenderCount);
    }

    private static LayoutVisualEvidence Evidence(
        VisualEvidenceKind kind) =>
        new(
            new LayoutObservation(
                physicalPageNumber:
                    1,
                observationSequence:
                    4,
                readingOrder:
                    4,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    left:
                        0.20,
                    top:
                        0.30,
                    right:
                        0.70,
                    bottom:
                        0.80),
                rawLabel:
                    "image"),
            kind);

    private static RasterRenderResult FullPageRaster() =>
        new(
            physicalPageNumber:
                1,
            sourcePagePixelWidth:
                1000,
            sourcePagePixelHeight:
                1200,
            crop:
                null,
            outputPixelWidth:
                1000,
            outputPixelHeight:
                1200,
            mediaType:
                "image/png",
            profileId:
                RecordingRasterizationSession.Profile,
            contentLength:
                1,
            contentSha256:
                PageSha);

    private static string Hash(
        byte[] bytes) =>
        Convert.ToHexString(
                SHA256.HashData(
                    bytes))
            .ToLowerInvariant();

    private static readonly byte[] RegionBytes =
        Encoding.UTF8.GetBytes(
            "meaningful-layout-region");

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string PageSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private sealed class RecordingRasterizationSession
        : IDocumentRasterizationSession
    {
        public const string Profile =
            "fake-raster-profile-v1";

        public string BackendId =>
            "fake-raster";

        public string ProfileId =>
            Profile;

        public int Dpi =>
            300;

        public int RegionRenderCount { get; private set; }

        public PixelRectangle? LastCrop { get; private set; }

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Regional preservation must reuse the caller's existing page raster.");

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RegionRenderCount++;
            LastCrop =
                crop;

            destination.Write(
                RegionBytes);

            return ValueTask.FromResult(
                new RasterRenderResult(
                    physicalPageNumber,
                    sourcePagePixelWidth,
                    sourcePagePixelHeight,
                    crop,
                    outputPixelWidth:
                        crop.Width,
                    outputPixelHeight:
                        crop.Height,
                    mediaType:
                        "image/png",
                    profileId:
                        Profile,
                    contentLength:
                        RegionBytes.Length,
                    contentSha256:
                        Hash(
                            RegionBytes)));
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
