using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.UnitTests.Locations;

/// <summary>
/// Verifies the first format-neutral source-location contract.
/// </summary>
public sealed class DocumentSourceLocationTests
{
    #region Methods Tests

    [Fact]
    public void PagedLocation_RetainsPhysicalPageAndBounds()
    {
        var bounds =
            new NormalizedRectangle(
                0.10,
                0.20,
                0.80,
                0.90);

        DocumentSourceLocation location =
            new PagedDocumentSourceLocation(
                physicalPageNumber:
                    7,
                bounds);

        var paged =
            Assert.IsType<PagedDocumentSourceLocation>(
                location);

        Assert.Equal(
            7,
            paged.PhysicalPageNumber);

        Assert.Equal(
            bounds,
            paged.Bounds);
    }

    [Fact]
    public void PagedLocation_AllowsWholePhysicalPageLocation()
    {
        var location =
            new PagedDocumentSourceLocation(
                physicalPageNumber:
                    3);

        Assert.Equal(
            3,
            location.PhysicalPageNumber);

        Assert.Null(
            location.Bounds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PagedLocation_RejectsNonPositivePhysicalPageNumber(
        int physicalPageNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PagedDocumentSourceLocation(
                    physicalPageNumber));
    }

    #endregion
}
