using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Opens caller-owned storage for a meaningful visual selected by the current
/// authoritative PDF processing pipeline.
/// </summary>
/// <remarks>
/// This delegate is PDF-specific because the current authoritative PDF
/// processing path identifies preserved visuals through the portable layout
/// observation contract.
///
/// It intentionally remains outside <c>IDocumentFormatProcessor</c> so the
/// generic format-processing boundary does not acquire PDF-specific visual
/// destination semantics.
/// </remarks>
/// <param name="source">PDF source currently being processed.</param>
/// <param name="visual">
/// Layout observation for the visual selected for preservation.
/// </param>
/// <param name="cancellationToken">
/// Token used to cancel destination acquisition.
/// </param>
/// <returns>
/// A writable destination stream with the ownership semantics of the existing
/// authoritative processing pipeline.
/// </returns>
public delegate ValueTask<Stream> PdfPreservedVisualDestinationFactory(
    DocumentSource source,
    LayoutObservation visual,
    CancellationToken cancellationToken);
