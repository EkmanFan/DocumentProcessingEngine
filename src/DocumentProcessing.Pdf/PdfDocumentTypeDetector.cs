using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Pdf;

public sealed class PdfDocumentTypeDetector : IDocumentTypeDetector
{
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    public async ValueTask<DocumentTypeDetectionResult> DetectAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var stream = source.Content;
        if (!stream.CanSeek)
        {
            return DocumentTypeDetectionResult.Unknown;
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            var header = new byte[PdfSignature.Length];
            var totalRead = 0;

            while (totalRead < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(totalRead), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            if (totalRead != header.Length || !header.AsSpan().SequenceEqual(PdfSignature))
            {
                return DocumentTypeDetectionResult.Unknown;
            }

            return new DocumentTypeDetectionResult(
                DocumentFormatId.Pdf,
                "application/pdf",
                IsSupported: true);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
