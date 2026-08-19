using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Formats.Pdf;

/// <summary>
/// Opens caller-owned storage for a meaningful visual selected by the current
/// authoritative PDF processing pipeline.
/// </summary>
/// <remarks>
/// This delegate belongs to the temporary composition bridge used while the
/// existing PDF-shaped authoritative pipeline still lives in
/// DocumentProcessing.Engine.
///
/// It is intentionally not part of <c>IDocumentFormatProcessor</c>. B2 will
/// relocate PDF orchestration into the PDF module; at that point this bridge
/// can move with the concrete PDF strategy without introducing a dependency
/// from the PDF module to the generic engine module.
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
