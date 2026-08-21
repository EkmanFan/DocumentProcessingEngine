using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Converts structured-publication facts into the shared neutral visual
/// evidence vocabulary. It does not decide storage or write any asset.
/// </summary>
internal static class StructuredNativeVisualEvidenceAssessor
{
    public static VisualEvidenceKind Assess(
        StructuredNativeVisual visual)
    {
        ArgumentNullException.ThrowIfNull(
            visual);

        return visual.IsPublicationCover ||
               visual.IsNavigation ||
               visual.IsExplicitlyPresentationOnly ||
               visual.IsPreliminaryMatter ||
               visual.IsRepeatedPresentationVisual ||
               visual.IsTerminalPresentationMatter
            ? VisualEvidenceKind.PublicationPresentationVisual
            : visual.IsStructuredFigure ||
              visual.HasBodyMatterBoundary
                ? VisualEvidenceKind.StructuredContentMeaningfulVisual
                : VisualEvidenceKind.Unknown;
    }
}
