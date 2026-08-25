using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfInlineSuperscriptOrderRepairTests
{
    #region Tests

    [Fact]
    public void FindAnchorIndex_SelectsDeDecretis746HumanAnchor()
    {
        var marker =
            Geometry(
                left: 315.706,
                right: 328.8532,
                centerY: 597.590165,
                pointSize: 9.13);

        var anchors =
            new[]
            {
                Geometry(
                    left: 274.467,
                    right: 315.706,
                    centerY: 593.773,
                    pointSize: 11),
                Geometry(
                    left: 290.663,
                    right: 304.567,
                    centerY: 609.173,
                    pointSize: 11)
            };

        Assert.Equal(
            0,
            PdfInlineSuperscriptOrderRepair
                .FindAnchorIndex(
                    marker,
                    anchors));
    }

    [Fact]
    public void FindAnchorIndex_SelectsDeDecretis747HumanAnchor()
    {
        var marker =
            Geometry(
                left: 471.394,
                right: 484.5412,
                centerY: 550.92508,
                pointSize: 9.13);

        var anchors =
            new[]
            {
                Geometry(
                    left: 430.782,
                    right: 471.394,
                    centerY: 546.8905,
                    pointSize: 11),
                Geometry(
                    left: 413.229,
                    right: 471.584,
                    centerY: 561.1575,
                    pointSize: 11)
            };

        Assert.Equal(
            0,
            PdfInlineSuperscriptOrderRepair
                .FindAnchorIndex(
                    marker,
                    anchors));
    }

    [Fact]
    public void FindAnchorIndex_SelectsDeDecretis748HumanAnchor()
    {
        var marker =
            Geometry(
                left: 242.193,
                right: 255.3402,
                centerY: 488.914775,
                pointSize: 9.13);

        var anchors =
            new[]
            {
                Geometry(
                    left: 209.578,
                    right: 242.193,
                    centerY: 485.125,
                    pointSize: 11),
                Geometry(
                    left: 255.34,
                    right: 257.848,
                    centerY: 483.4695,
                    pointSize: 11)
            };

        Assert.Equal(
            0,
            PdfInlineSuperscriptOrderRepair
                .FindAnchorIndex(
                    marker,
                    anchors));
    }

    [Fact]
    public void FindAnchorIndex_RejectsSameSizeFootnoteBodyNumber()
    {
        var marker =
            Geometry(
                left: 108,
                right: 120.96,
                centerY: 247.5,
                pointSize: 9);

        var anchors =
            new[]
            {
                Geometry(
                    left: 70,
                    right: 108,
                    centerY: 244.5,
                    pointSize: 9)
            };

        Assert.Null(
            PdfInlineSuperscriptOrderRepair
                .FindAnchorIndex(
                    marker,
                    anchors));
    }

    [Fact]
    public void FindAnchorIndex_FailsClosedWhenAnchorIsAmbiguous()
    {
        var marker =
            Geometry(
                left: 100,
                right: 112,
                centerY: 204,
                pointSize: 8);

        var anchors =
            new[]
            {
                Geometry(
                    left: 70,
                    right: 100,
                    centerY: 200,
                    pointSize: 10),
                Geometry(
                    left: 72,
                    right: 100.2,
                    centerY: 200,
                    pointSize: 10)
            };

        Assert.Null(
            PdfInlineSuperscriptOrderRepair
                .FindAnchorIndex(
                    marker,
                    anchors));
    }

    #endregion


    #region Methods

    private static PdfInlineWordGeometry Geometry(
        double left,
        double right,
        double centerY,
        double pointSize) =>
        new(
            left,
            right,
            centerY - pointSize / 2.0,
            centerY + pointSize / 2.0,
            centerY,
            pointSize);

    #endregion
}
