using DocumentProcessing.Core.Extraction;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Maps PdfPig crop-box display coordinates into the canonical media-box
/// display coordinate space used by the current PDF raster/layout pipeline.
///
/// PdfPig v0.1.15 translates the effective CropBox origin to (0,0) before
/// exposing page content coordinates. The current DPE raster evaluation uses
/// pdftoppm without -cropbox, which renders the MediaBox. Native and raster
/// evidence therefore require an explicit viewport transform before spatial
/// comparison.
///
/// The effective CropBox is also retained as ContentViewport inside that
/// canonical MediaBox space. Position-sensitive document heuristics can then
/// reason relative to the visible source viewport without corrupting cross-modal
/// canonical coordinates.
/// </summary>
internal readonly record struct PdfPageCoordinateSpace
{
    private PdfPageCoordinateSpace(
        double width,
        double height,
        double offsetX,
        double offsetY,
        double contentWidth,
        double contentHeight)
    {
        if (!double.IsFinite(width) ||
            width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width));
        }

        if (!double.IsFinite(height) ||
            height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height));
        }

        if (!double.IsFinite(offsetX))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetX));
        }

        if (!double.IsFinite(offsetY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetY));
        }

        if (!double.IsFinite(contentWidth) ||
            contentWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentWidth));
        }

        if (!double.IsFinite(contentHeight) ||
            contentHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentHeight));
        }

        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;

        ContentViewport =
            new NormalizedRectangle(
                offsetX / width,
                1 -
                (offsetY + contentHeight) /
                height,
                (offsetX + contentWidth) /
                width,
                1 - offsetY / height);
    }

    public double Width { get; }

    public double Height { get; }

    /// <summary>
    /// Effective CropBox viewport expressed in canonical MediaBox-normalized
    /// top-left coordinates.
    /// </summary>
    public NormalizedRectangle ContentViewport { get; }

    private double OffsetX { get; }

    private double OffsetY { get; }

    public static PdfPageCoordinateSpace Create(
        Page page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var media =
            page.MediaBox.Bounds;

        var crop =
            page.CropBox.Bounds;

        var mediaLeft =
            Convert.ToDouble(media.Left);

        var mediaBottom =
            Convert.ToDouble(media.Bottom);

        var mediaRight =
            Convert.ToDouble(media.Right);

        var mediaTop =
            Convert.ToDouble(media.Top);

        var cropLeft =
            Convert.ToDouble(crop.Left);

        var cropBottom =
            Convert.ToDouble(crop.Bottom);

        var cropRight =
            Convert.ToDouble(crop.Right);

        var cropTop =
            Convert.ToDouble(crop.Top);

        var mediaWidth =
            Convert.ToDouble(media.Width);

        var mediaHeight =
            Convert.ToDouble(media.Height);

        var cropWidth =
            Convert.ToDouble(crop.Width);

        var cropHeight =
            Convert.ToDouble(crop.Height);

        if (mediaWidth <= 0 ||
            mediaHeight <= 0 ||
            cropWidth <= 0 ||
            cropHeight <= 0)
        {
            throw new InvalidDataException(
                $"PDF page {page.Number} has invalid MediaBox/CropBox dimensions.");
        }

        return page.Rotation.Value switch
        {
            0 =>
                new PdfPageCoordinateSpace(
                    mediaWidth,
                    mediaHeight,
                    cropLeft - mediaLeft,
                    cropBottom - mediaBottom,
                    cropWidth,
                    cropHeight),

            90 =>
                new PdfPageCoordinateSpace(
                    mediaHeight,
                    mediaWidth,
                    cropBottom - mediaBottom,
                    mediaRight - cropRight,
                    cropHeight,
                    cropWidth),

            180 =>
                new PdfPageCoordinateSpace(
                    mediaWidth,
                    mediaHeight,
                    mediaRight - cropRight,
                    mediaTop - cropTop,
                    cropWidth,
                    cropHeight),

            270 =>
                new PdfPageCoordinateSpace(
                    mediaHeight,
                    mediaWidth,
                    mediaTop - cropTop,
                    cropLeft - mediaLeft,
                    cropHeight,
                    cropWidth),

            _ =>
                throw new InvalidDataException(
                    $"PDF page {page.Number} has unsupported rotation " +
                    $"{page.Rotation.Value}.")
        };
    }

    public NormalizedRectangle ToNormalizedRectangle(
        PdfRectangle cropDisplayBounds)
    {
        // PdfPig can expose rotated word/glyph rectangles whose semantic
        // Left/Right/Top/Bottom edges are not axis-aligned in display space.
        // DPE's NormalizedRectangle is axis-aligned, so first compute the
        // smallest axis-aligned rectangle containing all four PdfRectangle
        // corners. Only then translate CropBox display coordinates into the
        // canonical MediaBox display viewport.
        var axisAlignedCropDisplayBounds =
            cropDisplayBounds.Normalise();

        var left =
            Convert.ToDouble(
                axisAlignedCropDisplayBounds.Left) +
            OffsetX;

        var right =
            Convert.ToDouble(
                axisAlignedCropDisplayBounds.Right) +
            OffsetX;

        var bottom =
            Convert.ToDouble(
                axisAlignedCropDisplayBounds.Bottom) +
            OffsetY;

        var top =
            Convert.ToDouble(
                axisAlignedCropDisplayBounds.Top) +
            OffsetY;

        return new NormalizedRectangle(
            left / Width,
            1 - top / Height,
            right / Width,
            1 - bottom / Height);
    }
}
