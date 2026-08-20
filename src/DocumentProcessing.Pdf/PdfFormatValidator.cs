using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Validates that a candidate source carries a PDF binary signature.
/// </summary>
/// <remarks>
/// Filename and declared media type are hints only and are not accepted as
/// proof that the underlying source is a PDF.
/// </remarks>
public sealed class PdfFormatValidator
    : IFormatValidator
{
    #region Properties

    private static ReadOnlySpan<byte> PdfSignature =>
        "%PDF-"u8;

    #endregion

    #region Methods Validation

    public async ValueTask<bool> ValidateAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var stream =
            source.Content;

        if (!stream.CanSeek)
        {
            return false;
        }

        var originalPosition =
            stream.Position;

        try
        {
            stream.Position =
                0;

            var header =
                new byte[PdfSignature.Length];

            var totalRead =
                0;

            while (totalRead <
                   header.Length)
            {
                var read =
                    await stream
                        .ReadAsync(
                            header.AsMemory(
                                totalRead),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                totalRead +=
                    read;
            }

            return totalRead ==
                       header.Length &&
                   header
                       .AsSpan()
                       .SequenceEqual(
                           PdfSignature);
        }
        finally
        {
            stream.Position =
                originalPosition;
        }
    }

    #endregion
}
