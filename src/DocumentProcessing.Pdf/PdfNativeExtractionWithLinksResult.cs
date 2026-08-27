using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Pdf.Notes;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PDF-native extraction plus representation-local internal-link evidence.
/// </summary>
internal sealed record PdfNativeExtractionWithLinksResult(
    DocumentExtractionWithRasterObservationsResult
        ExtractionWithRasterObservations,
    IReadOnlyList<PdfNativeNumericLinkObservation> NativeNumericLinks);
