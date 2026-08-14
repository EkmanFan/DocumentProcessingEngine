using System.Globalization;
using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfPigMediaCoordinateSpaceTests
{
    [Theory]
    [InlineData(0, 600d, 800d, 0.23d, 0.12d)]
    [InlineData(90, 800d, 600d, 0.88d, 0.23d)]
    [InlineData(180, 600d, 800d, 0.77d, 0.88d)]
    [InlineData(270, 800d, 600d, 0.12d, 0.77d)]
    public async Task ExtractAsync_MapsOffsetCropBoxEvidenceIntoMediaDisplaySpace(
        int rotation,
        double expectedWidth,
        double expectedHeight,
        double expectedCenterX,
        double expectedCenterY)
    {
        var pdfBytes =
            CreateOffsetCropBoxFixture(
                rotation);

        await using var stream =
            new MemoryStream(
                pdfBytes);

        var source =
            new DocumentSource(
                stream,
                "offset-cropbox.pdf",
                "application/pdf");

        var page =
            Assert.Single(
                (await new PdfPigDocumentExtractor()
                    .ExtractAsync(
                        source,
                        DocumentFormatId.Pdf))
                .Pages);

        Assert.Equal(
            expectedWidth,
            page.SourceWidth,
            precision: 6);

        Assert.Equal(
            expectedHeight,
            page.SourceHeight,
            precision: 6);

        var expectedContentViewport =
            rotation is 0 or 180
                ? new
                {
                    Left = 1d / 6d,
                    Top = 0.0625d,
                    Right = 5d / 6d,
                    Bottom = 0.9375d
                }
                : new
                {
                    Left = 0.0625d,
                    Top = 1d / 6d,
                    Right = 0.9375d,
                    Bottom = 5d / 6d
                };

        Assert.Equal(
            expectedContentViewport.Left,
            page.ContentViewport.Left,
            precision: 6);

        Assert.Equal(
            expectedContentViewport.Top,
            page.ContentViewport.Top,
            precision: 6);

        Assert.Equal(
            expectedContentViewport.Right,
            page.ContentViewport.Right,
            precision: 6);

        Assert.Equal(
            expectedContentViewport.Bottom,
            page.ContentViewport.Bottom,
            precision: 6);

        var word =
            Assert.Single(
                page.Words,
                candidate =>
                    candidate.Text.Contains(
                        "HELLO",
                        StringComparison.Ordinal));

        var centerX =
            (word.Bounds.Left +
             word.Bounds.Right) /
            2d;

        var centerY =
            (word.Bounds.Top +
             word.Bounds.Bottom) /
            2d;

        Assert.InRange(
            centerX,
            expectedCenterX - 0.08d,
            expectedCenterX + 0.08d);

        Assert.InRange(
            centerY,
            expectedCenterY - 0.08d,
            expectedCenterY + 0.08d);

        Assert.InRange(
            word.Bounds.Left,
            0d,
            1d);

        Assert.InRange(
            word.Bounds.Top,
            0d,
            1d);

        Assert.InRange(
            word.Bounds.Right,
            0d,
            1d);

        Assert.InRange(
            word.Bounds.Bottom,
            0d,
            1d);
    }

    private static byte[] CreateOffsetCropBoxFixture(
        int rotation)
    {
        var content =
            "BT /F1 12 Tf 120 700 Td (HELLO) Tj ET";

        var objects =
            new Dictionary<int, string>
            {
                [1] =
                    "<< /Type /Catalog /Pages 2 0 R >>",

                [2] =
                    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",

                [3] =
                    "<< /Type /Page /Parent 2 0 R " +
                    "/MediaBox [0 0 600 800] " +
                    "/CropBox [100 50 500 750] " +
                    $"/Rotate {rotation} " +
                    "/Resources << /Font << /F1 5 0 R >> >> " +
                    "/Contents 4 0 R >>",

                [4] =
                    $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\n" +
                    "stream\n" +
                    content +
                    "\nendstream",

                [5] =
                    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

        using var stream =
            new MemoryStream();

        var offsets =
            new long[6];

        WriteAscii(
            stream,
            "%PDF-1.4\n");

        for (var objectNumber = 1;
             objectNumber <= 5;
             objectNumber++)
        {
            offsets[objectNumber] =
                stream.Position;

            WriteAscii(
                stream,
                $"{objectNumber} 0 obj\n" +
                objects[objectNumber] +
                "\nendobj\n");
        }

        var xrefOffset =
            stream.Position;

        WriteAscii(
            stream,
            "xref\n" +
            "0 6\n" +
            "0000000000 65535 f \n");

        for (var objectNumber = 1;
             objectNumber <= 5;
             objectNumber++)
        {
            WriteAscii(
                stream,
                offsets[objectNumber]
                    .ToString(
                        "0000000000",
                        CultureInfo.InvariantCulture) +
                " 00000 n \n");
        }

        WriteAscii(
            stream,
            "trailer\n" +
            "<< /Size 6 /Root 1 0 R >>\n" +
            "startxref\n" +
            xrefOffset.ToString(
                CultureInfo.InvariantCulture) +
            "\n%%EOF\n");

        return stream.ToArray();
    }

    private static void WriteAscii(
        Stream stream,
        string value)
    {
        var bytes =
            Encoding.ASCII.GetBytes(
                value);

        stream.Write(
            bytes);
    }
}
