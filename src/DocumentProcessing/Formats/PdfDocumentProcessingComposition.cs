using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Additive PDF composition exposing the current strategy surface and the new
/// Engine format-processing binding over one authoritative processor graph.
/// </summary>
/// <remarks>
/// This is composition data only. It performs no routing, selection or
/// processing and exists during the controlled Host migration.
/// </remarks>
internal sealed class PdfDocumentProcessingComposition
{
    #region ctor

    public PdfDocumentProcessingComposition(
        PdfDocumentFormatProcessor legacyProcessor,
        DocumentFormatProcessingBinding processingBinding)
    {
        LegacyProcessor =
            legacyProcessor ??
            throw new ArgumentNullException(
                nameof(legacyProcessor));

        ProcessingBinding =
            processingBinding ??
            throw new ArgumentNullException(
                nameof(processingBinding));

        if (LegacyProcessor.Format !=
            ProcessingBinding.Format)
        {
            throw new ArgumentException(
                $"Legacy PDF processor format '{LegacyProcessor.Format}' does not match " +
                $"Engine binding format '{ProcessingBinding.Format}'.",
                nameof(processingBinding));
        }
    }

    #endregion

    #region Properties

    public PdfDocumentFormatProcessor LegacyProcessor { get; }

    public DocumentFormatProcessingBinding ProcessingBinding { get; }

    #endregion
}
