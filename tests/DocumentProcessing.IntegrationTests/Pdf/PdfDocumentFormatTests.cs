using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfDocumentFormatTests
{
    #region Methods Tests

    [Fact]
    public void Format_IsPdf()
    {
        IDocumentFormat format =
            new PdfDocumentFormat();

        Assert.Equal(
            DocumentFormatId.Pdf,
            format.Format);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_NotRecognizedForNonPdfAndRestoresPosition()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "not a pdf"));

        stream.Position =
            2;

        var source =
            new DocumentSource(
                stream,
                "misleading.pdf",
                "application/pdf");

        var result =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    source);

        Assert.IsType<
            NativeEvidenceExtractionResult.NotRecognized>(
            result);

        Assert.Equal(
            2,
            stream.Position);

        Assert.True(
            stream.CanRead);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_SuccessPreservesExactCurrentCoordinatedEvidence()
    {
        var pdfBytes =
            CreateSinglePagePdf(
                "Native evidence parity");

        await using var baselineStream =
            new MemoryStream(
                pdfBytes);

        var baselineSource =
            new DocumentSource(
                baselineStream,
                "fixture.pdf",
                "application/pdf");

        var baseline =
            await new PdfPigDocumentExtractor()
                .ExtractWithRasterObservationsAsync(
                    baselineSource,
                    DocumentFormatId.Pdf,
                    new PdfPigVisualRasterObservationSource());

        await using var candidateStream =
            new MemoryStream(
                pdfBytes);

        candidateStream.Position =
            7;

        var candidateSource =
            new DocumentSource(
                candidateStream,
                "fixture.pdf",
                "application/pdf");

        var outcome =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    candidateSource);

        var success =
            Assert.IsType<
                NativeEvidenceExtractionResult.Success>(
                outcome);

        Assert.Equal(
            DocumentFormatId.Pdf,
            success.Evidence.Extraction.Format);

        Assert.Equal(
            Serialize(
                baseline.Extraction),
            Serialize(
                success.Evidence.Extraction));

        Assert.Equal(
            Serialize(
                baseline.RasterObservations),
            Serialize(
                success.Evidence.RasterObservations));

        Assert.Equal(
            Serialize(
                baseline.RasterObservationFailure),
            Serialize(
                success.Evidence.RasterObservationFailure));

        Assert.Equal(
            7,
            candidateStream.Position);

        Assert.True(
            candidateStream.CanRead);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_RecognizedMalformedPdfReturnsInvalid()
    {
        var validPdf =
            CreateSinglePagePdf(
                "Will be truncated");

        var malformedPdf =
            validPdf[
                ..Math.Min(
                    32,
                    validPdf.Length)];

        Assert.True(
            malformedPdf
                .AsSpan()
                .StartsWith(
                    "%PDF-"u8));

        await using var stream =
            new MemoryStream(
                malformedPdf);

        var result =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    new DocumentSource(
                        stream,
                        "truncated.pdf",
                        "application/pdf"));

        var invalid =
            Assert.IsType<
                NativeEvidenceExtractionResult.Invalid>(
                result);

        Assert.False(
            string.IsNullOrWhiteSpace(
                invalid.Reason));
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_PropagatesCallerCancellation()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "%PDF-1.7\nfixture"));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            async () =>
                await new PdfDocumentFormat()
                    .TryExtractNativeEvidenceAsync(
                        new DocumentSource(
                            stream),
                        cancellation.Token));
    }

    #endregion

    #region Methods Fixtures

    private static byte[] CreateSinglePagePdf(
        string text)
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var page =
            builder.AddPage(
                PageSize.A4);

        page.AddText(
            text,
            12,
            new PdfPoint(
                72,
                720),
            font);

        return builder.Build();
    }

    private static string Serialize<T>(
        T value) =>
        JsonSerializer.Serialize(
            value);

    #endregion
}
