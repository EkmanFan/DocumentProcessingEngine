using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Executes the current authoritative PDF processing path behind the
/// format-specific processor boundary.
/// </summary>
/// <remarks>
/// This delegate is intentionally narrow. The owning PDF processor knows only
/// that one PDF source can be executed with optional PDF visual-destination
/// semantics. Concrete Engine construction remains a top-level composition
/// responsibility.
/// </remarks>
public delegate Task<DocumentIngestionResult> PdfDocumentExecution(
    DocumentSource source,
    PreservedLayoutVisualDestinationFactory?
        openPreservedVisualDestinationAsync,
    CancellationToken cancellationToken);
