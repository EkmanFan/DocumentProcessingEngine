using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class AuthoritativeLayoutSpoolTests
{
    #region Tests

    [Fact]
    public async Task RoundTrip_PreservesNeutralRasterAndLayoutEvidence()
    {
        var root = CreateTemporaryRoot();

        try
        {
            await using var spool =
                AuthoritativeLayoutSpool.Create(root);

            var raster = FullPageRaster(physicalPageNumber: 7);
            var layout = Layout(physicalPageNumber: 7);

            await spool.WriteAsync(raster, layout);

            var restored = await spool.ReadAsync(7);

            Assert.Equal(raster, restored.PageRaster);
            Assert.Equal(layout.BackendId, restored.Layout.BackendId);
            Assert.Equal(layout.PhysicalPageNumber, restored.Layout.PhysicalPageNumber);
            Assert.Equal(layout.Observations.ToArray(), restored.Layout.Observations.ToArray());
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task DisposeAsync_RemovesSpoolDirectoryAndEntries()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var spool =
                AuthoritativeLayoutSpool.Create(root);

            await spool.WriteAsync(
                FullPageRaster(1),
                Layout(1));

            Assert.Single(Directory.GetDirectories(root));
            Assert.Single(Directory.GetFiles(
                Directory.GetDirectories(root).Single()));

            await spool.DisposeAsync();

            Assert.Empty(Directory.GetDirectories(root));
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task DisposeAsync_CleanupFailure_DoesNotMaskOriginalException()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () =>
                    {
                        await using var spool =
                            AuthoritativeLayoutSpool.Create(
                                root,
                                _ =>
                                    throw new IOException(
                                        "Simulated cleanup failure."));

                        throw new InvalidOperationException(
                            "Original authoritative failure.");
                    });

            Assert.Equal(
                "Original authoritative failure.",
                exception.Message);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task WriteAsync_RejectsRegionRasterMetadata()
    {
        var root = CreateTemporaryRoot();

        try
        {
            await using var spool =
                AuthoritativeLayoutSpool.Create(root);

            var raster =
                new RasterRenderResult(
                    physicalPageNumber: 1,
                    sourcePagePixelWidth: 1000,
                    sourcePagePixelHeight: 1400,
                    crop: new PixelRectangle(10, 20, 110, 120),
                    outputPixelWidth: 100,
                    outputPixelHeight: 100,
                    mediaType: "image/png",
                    profileId: "fixture-raster-v1",
                    contentLength: 123,
                    contentSha256: new string('a', 64));

            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await spool.WriteAsync(
                        raster,
                        Layout(1)));
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsPageWithoutSpoolEntry()
    {
        var root = CreateTemporaryRoot();

        try
        {
            await using var spool =
                AuthoritativeLayoutSpool.Create(root);

            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await spool.ReadAsync(9));
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    #endregion

    #region Helpers

    private static RasterRenderResult FullPageRaster(
        int physicalPageNumber) =>
        new(
            physicalPageNumber,
            sourcePagePixelWidth: 1000,
            sourcePagePixelHeight: 1400,
            crop: null,
            outputPixelWidth: 1000,
            outputPixelHeight: 1400,
            mediaType: "image/png",
            profileId: "fixture-raster-v1",
            contentLength: 4567,
            contentSha256: new string('b', 64));

    private static LayoutAnalysisResult Layout(
        int physicalPageNumber) =>
        new(
            "fixture-layout-v1",
            physicalPageNumber,
            new[]
            {
                new LayoutObservation(
                    physicalPageNumber,
                    observationSequence: 0,
                    readingOrder: 0,
                    LayoutObservationKind.Heading,
                    new NormalizedRectangle(0.1, 0.1, 0.9, 0.2),
                    rawLabel: "paragraph_title"),
                new LayoutObservation(
                    physicalPageNumber,
                    observationSequence: 1,
                    readingOrder: 1,
                    LayoutObservationKind.Text,
                    new NormalizedRectangle(0.1, 0.25, 0.9, 0.8),
                    rawLabel: "text")
            });

    private static string CreateTemporaryRoot()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"document-processing-layout-spool-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteIfPresent(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    #endregion
}
