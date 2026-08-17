using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Single deterministic mapping from neutral visual evidence to semantic
/// disposition.
///
/// This mapping is shared by source-visual and layout-region processing so the
/// evidence vocabulary cannot acquire divergent preservation semantics.
/// </summary>
internal static class VisualEvidenceDispositionPolicy
{
    public static VisualDisposition Decide(
        VisualEvidenceKind evidenceKind)
    {
        if (!Enum.IsDefined(
                evidenceKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceKind),
                evidenceKind,
                "Visual evidence kind must be a defined value.");
        }

        return evidenceKind switch
        {
            VisualEvidenceKind.Unknown =>
                VisualDisposition.RequiresVisualAnalysis,

            VisualEvidenceKind.BlankCanvas or
            VisualEvidenceKind.TinyOrNoise or
            VisualEvidenceKind.SmallHeadingAssociatedVisual or
            VisualEvidenceKind.HeadingBackplateOrPresentation or
            VisualEvidenceKind.NativeTextContainerOrFrame =>
                VisualDisposition.PresentationOnly,

            VisualEvidenceKind.CaptionedMeaningfulVisual or
            VisualEvidenceKind.LargeIndependentVisual =>
                VisualDisposition.PreserveMeaningfulVisual,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceKind),
                    evidenceKind,
                    "Unsupported visual evidence kind.")
        };
    }
}
