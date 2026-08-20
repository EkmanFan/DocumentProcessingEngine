using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Composes the PDF format adapter from PDF-specific capabilities and already
/// composed shared processing capabilities.
/// </summary>
/// <remarks>
/// This type deliberately does not construct PP-StructureV3, PaddleOCR, service
/// HTTP clients, Engine-internal planner/hybrid/visual implementation details,
/// or format-specific option containers that do not represent PDF semantics.
/// </remarks>
internal static class PdfDocumentFormatProcessorComposition
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "pdfpig",
            "pdfpig-native-v1");

    #endregion

    #region Methods Composition

    public static PdfDocumentFormatProcessor Create(
        string engineVersion,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer,
        ProcessingComponentIdentity layoutAnalysisIdentity,
        PreservedLayoutVisualDestinationFactory?
            openPreservedLayoutVisualDestinationAsync)
    {
        ArgumentNullException.ThrowIfNull(
            layoutAnalyzer);

        ArgumentNullException.ThrowIfNull(
            textRecognizer);

        ArgumentNullException.ThrowIfNull(
            layoutAnalysisIdentity);

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        var authoritativeProcessor =
            DocumentProcessorFactory.CreateHybrid(
                DocumentFormatId.Pdf,
                new PdfPigDocumentExtractor(),
                new PdfPreflightAnalyzer(),
                new PdftoppmDocumentRasterizer(
                    dpi:
                        300),
                new PdfPigVisualRasterObservationSource(),
                layoutAnalyzer,
                textRecognizer,
                engineVersion,
                NativeIdentity,
                layoutAnalysisIdentity);

        return new PdfDocumentFormatProcessor(
            ExecuteAsync,
            openPreservedLayoutVisualDestinationAsync);

        Task<DocumentIngestionResult> ExecuteAsync(
            DocumentSource source,
            PreservedLayoutVisualDestinationFactory?
                openPreservedVisualDestinationAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            return openPreservedVisualDestinationAsync is null
                ? authoritativeProcessor.ProcessAsync(
                    source,
                    cancellationToken)
                : authoritativeProcessor.ProcessAsync(
                    source,
                    (visual, token) =>
                        openPreservedVisualDestinationAsync(
                            source,
                            visual,
                            token),
                    cancellationToken);
        }
    }

    #endregion
}
