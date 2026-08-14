namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Open/closed variation boundary for deterministic page-routing policy.
///
/// Implementations decide which already-supported V1 processing route should be
/// used for an assessed page. They must not perform extraction, rasterization,
/// layout analysis, OCR, visual persistence, reconciliation or other I/O.
/// </summary>
public interface IPageProcessingPolicy
{
    PageProcessingPlan Decide(
        PageProcessingAssessment assessment);
}
