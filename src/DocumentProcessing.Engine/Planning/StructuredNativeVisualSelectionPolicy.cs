using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Engine-owned deterministic policy for selecting structured-document visual
/// candidates for preservation.
/// </summary>
internal static class StructuredNativeVisualSelectionPolicy
{
    public static bool ShouldPreserve(
        StructuredNativeVisual visual)
    {
        ArgumentNullException.ThrowIfNull(
            visual);

        return !visual.IsPublicationCover &&
               !visual.IsNavigation &&
               !visual.IsExplicitlyPresentationOnly;
    }
}
