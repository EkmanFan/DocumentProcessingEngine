using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies that preserved-visual custody is portable across rasterized and
/// directly preserved document formats.
/// </summary>
public sealed class PortableVisualCustodyTests
{
    #region Variables and Constants

    private const string ContentSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    #endregion

    #region Methods Tests

    [Fact]
    public void VisualAsset_AllowsDirectPreservationWithoutRasterDerivation()
    {
        var asset =
            new DocumentVisualAsset(
                assetId:
                    "asset-1",
                elementId:
                    "visual-element-1",
                preservationProfileId:
                    "embedded-image-v1",
                mediaType:
                    "image/png",
                contentLength:
                    123,
                ContentSha);

        Assert.Null(
            asset.RasterDerivation);

        Assert.Equal(
            "visual-element-1",
            asset.ElementId);
    }

    [Fact]
    public void VisualAsset_RetainsOptionalRasterDerivation()
    {
        var derivation =
            new DocumentRasterVisualDerivationEvidence(
                sourcePixelWidth:
                    1200,
                sourcePixelHeight:
                    1600,
                new PixelRectangle(
                    left:
                        100,
                    top:
                        200,
                    right:
                        700,
                    bottom:
                        900));

        var asset =
            new DocumentVisualAsset(
                assetId:
                    "asset-2",
                elementId:
                    "visual-element-2",
                preservationProfileId:
                    "pdf-raster-v1",
                mediaType:
                    "image/png",
                contentLength:
                    456,
                ContentSha,
                derivation);

        Assert.Same(
            derivation,
            asset.RasterDerivation);

        Assert.Equal(
            1200,
            derivation.SourcePixelWidth);
    }

    [Fact]
    public void VisualAsset_HasNoPhysicalPageProperty()
    {
        Assert.Null(
            typeof(DocumentVisualAsset)
                .GetProperty(
                    "PhysicalPageNumber"));

        Assert.Null(
            typeof(DocumentRasterVisualDerivationEvidence)
                .GetProperty(
                    "PhysicalPageNumber"));
    }

    [Fact]
    public void VisualAsset_RejectsNonImageMediaType()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentVisualAsset(
                    assetId:
                        "asset-1",
                    elementId:
                        "visual-element-1",
                    preservationProfileId:
                        "profile-v1",
                    mediaType:
                        "application/octet-stream",
                    contentLength:
                        1,
                    ContentSha));
    }

    [Fact]
    public void RasterDerivation_RejectsCropOutsideSourceRaster()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentRasterVisualDerivationEvidence(
                    sourcePixelWidth:
                        100,
                    sourcePixelHeight:
                        100,
                    new PixelRectangle(
                        left:
                            10,
                        top:
                            10,
                        right:
                            101,
                        bottom:
                            90)));
    }

    #endregion
}
