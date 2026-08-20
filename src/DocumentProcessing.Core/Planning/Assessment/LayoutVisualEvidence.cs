using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Semantic evidence for one layout-detected visual region.
///
/// The underlying layout observation remains the spatial/provenance identity.
/// This type deliberately carries no visual disposition and no execution action.
/// </summary>
public sealed record LayoutVisualEvidence
{
    public LayoutVisualEvidence(
        LayoutObservation observation,
        VisualEvidenceKind kind)
    {
        Observation =
            observation ??
            throw new ArgumentNullException(
                nameof(observation));

        if (Observation.Kind !=
            LayoutObservationKind.Figure)
        {
            throw new ArgumentException(
                "Layout visual evidence requires a Figure observation.",
                nameof(observation));
        }

        if (!Enum.IsDefined(
                kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Visual evidence kind must be a defined value.");
        }

        Kind =
            kind;
    }

    public LayoutObservation Observation { get; }

    public VisualEvidenceKind Kind { get; }
}
