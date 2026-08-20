namespace DocumentProcessing.Pdf;

/// <summary>
/// Remaining V1 PDF integration configuration.
/// </summary>
/// <remarks>
/// Shared PP-StructureV3 and PaddleOCR configuration no longer belongs here.
/// This transitional type now retains only the visual-destination callback and
/// is expected to disappear when that callback is moved to a portable contract.
/// </remarks>
public sealed class PdfDocumentProcessingOptions
{
    #region ctor

    public PdfDocumentProcessingOptions(
        PdfPreservedVisualDestinationFactory?
            openPreservedVisualDestinationAsync = null)
    {
        OpenPreservedVisualDestinationAsync =
            openPreservedVisualDestinationAsync;
    }

    #endregion

    #region Properties

    public PdfPreservedVisualDestinationFactory?
        OpenPreservedVisualDestinationAsync { get; }

    #endregion
}
