using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfFormatValidatorTests
{
    #region Methods Tests

    [Fact]
    public async Task ValidateAsync_AcceptsPdfSignatureAndRestoresStreamPosition()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "%PDF-1.7\nfixture"));

        stream.Position =
            3;

        var source =
            new DocumentSource(
                stream,
                "fixture.bin",
                "application/octet-stream");

        var validator =
            new PdfFormatValidator();

        var isValid =
            await validator
                .ValidateAsync(
                    source);

        Assert.True(
            isValid);

        Assert.Equal(
            3,
            stream.Position);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotTrustFileNameOrDeclaredMediaType()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "not a pdf"));

        var source =
            new DocumentSource(
                stream,
                "fixture.pdf",
                "application/pdf");

        var validator =
            new PdfFormatValidator();

        var isValid =
            await validator
                .ValidateAsync(
                    source);

        Assert.False(
            isValid);
    }

    #endregion
}
