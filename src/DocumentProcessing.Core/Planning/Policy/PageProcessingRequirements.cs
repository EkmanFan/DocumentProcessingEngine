namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Immutable two-axis processing requirements selected from
/// <see cref="PageProcessingEvidence"/>.
///
/// The text requirement and per-visual dispositions are intentionally
/// independent. This contract contains no <see cref="PageProcessingRoute"/> and
/// no <see cref="PageProcessingPlan"/>.
/// </summary>
public sealed record PageProcessingRequirements
{
    public PageProcessingRequirements(
        int physicalPageNumber,
        TextProcessingRequirement textRequirement,
        IEnumerable<VisualElementDisposition> visualElements)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (!Enum.IsDefined(
                textRequirement))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textRequirement),
                textRequirement,
                "Text processing requirement must be a defined value.");
        }

        ArgumentNullException.ThrowIfNull(
            visualElements);

        var materialized =
            visualElements.ToArray();

        var indexes =
            new HashSet<int>();

        foreach (var visualElement in
                 materialized)
        {
            if (visualElement is null)
            {
                throw new ArgumentException(
                    "Visual dispositions cannot contain null elements.",
                    nameof(visualElements));
            }

            if (!indexes.Add(
                    visualElement.SourceVisualIndex))
            {
                throw new ArgumentException(
                    "Visual dispositions cannot contain duplicate source visual indexes.",
                    nameof(visualElements));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        TextRequirement =
            textRequirement;

        VisualElements =
            Array.AsReadOnly(
                materialized);
    }

    public int PhysicalPageNumber { get; }

    public TextProcessingRequirement TextRequirement { get; }

    /// <summary>
    /// Per-source-visual policy decisions. An empty collection means the page
    /// has no classified visual occurrence.
    /// </summary>
    public IReadOnlyList<VisualElementDisposition> VisualElements { get; }

    public bool UsesNativeTextWithoutVerification =>
        TextRequirement ==
        TextProcessingRequirement.UseNativeText;

    public bool RequiresTextRecovery =>
        TextRequirement ==
        TextProcessingRequirement.RecoverMissingNativeText;

    public bool RequiresTextVerification =>
        TextRequirement ==
        TextProcessingRequirement.VerifyNativeText;

    public bool RequiresTextReconciliation =>
        TextRequirement ==
        TextProcessingRequirement.ReconcileCorruptedNativeText;

    public bool RequiresVisualAnalysis =>
        VisualElements.Any(
            visual =>
                visual.Disposition ==
                VisualDisposition.RequiresVisualAnalysis);

    public bool HasMeaningfulVisuals =>
        VisualElements.Any(
            visual =>
                visual.Disposition ==
                VisualDisposition.PreserveMeaningfulVisual);
}
