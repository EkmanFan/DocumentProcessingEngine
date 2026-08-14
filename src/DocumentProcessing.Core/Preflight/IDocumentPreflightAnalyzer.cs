using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Preflight;

/// <summary>
/// Format-neutral capability boundary for deterministic preflight analysis over
/// an already-extracted document.
///
/// Preflight reports source-document evidence/classification. It does not choose
/// a page-processing policy or execute raster/layout/OCR work.
/// </summary>
public interface IDocumentPreflightAnalyzer
{
    bool CanAnalyze(
        DocumentFormatId format);

    DocumentPreflightResult Analyze(
        DocumentExtractionResult extraction);
}
