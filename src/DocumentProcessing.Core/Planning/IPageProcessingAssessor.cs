using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Deterministic evidence-to-assessment boundary for one extracted page.
///
/// The assessor classifies available native evidence. It does not execute
/// rasterization, layout analysis, OCR, reconciliation or downstream policy.
/// </summary>
public interface IPageProcessingAssessor
{
    PageProcessingAssessment Assess(
        DocumentExtractionPage page);
}
