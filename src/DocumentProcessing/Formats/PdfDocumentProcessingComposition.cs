using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Temporary PDF composition wrapper exposing the current authoritative format
/// processor during controlled Host migration.
/// </summary>
internal sealed class PdfDocumentProcessingComposition
{
    #region ctor

    public PdfDocumentProcessingComposition(
        PdfDocumentFormatProcessor legacyProcessor)
    {
        LegacyProcessor =
            legacyProcessor ??
            throw new ArgumentNullException(
                nameof(legacyProcessor));
    }

    #endregion

    #region Properties

    public PdfDocumentFormatProcessor LegacyProcessor { get; }

    #endregion
}
