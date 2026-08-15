namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Pure deterministic policy boundary from page evidence to independent text
/// and visual processing requirements.
///
/// Implementations must not perform extraction, rasterization, layout analysis,
/// OCR, visual persistence, reconciliation or other I/O.
/// </summary>
public interface IPageProcessingRequirementsPolicy
{
    PageProcessingRequirements Decide(
        PageProcessingEvidence evidence);
}
