namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Independent page execution plan for text and visual work.
///
/// The plan stores closed execution modes rather than independent mutable
/// booleans. Shared prerequisites such as rasterization and layout analysis are
/// derived from those modes, preventing contradictory combinations.
///
/// This V1 plan deliberately contains neither <see cref="PageProcessingRoute"/>
/// nor <see cref="PageProcessingPlan"/> from the authoritative route-based planning model.
/// </summary>
public sealed record PageExecutionPlan
{
    public PageExecutionPlan(
        int physicalPageNumber,
        TextExecutionMode textMode,
        IEnumerable<VisualElementExecutionPlan> visualElements)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (!Enum.IsDefined(
                textMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textMode),
                textMode,
                "Text execution mode must be a defined value.");
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
                    "Visual execution plans cannot contain null elements.",
                    nameof(visualElements));
            }

            if (!indexes.Add(
                    visualElement.SourceVisualIndex))
            {
                throw new ArgumentException(
                    "Visual execution plans cannot contain duplicate source visual indexes.",
                    nameof(visualElements));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        TextMode =
            textMode;

        VisualElements =
            Array.AsReadOnly(
                materialized);
    }

    public int PhysicalPageNumber { get; }

    public TextExecutionMode TextMode { get; }

    public IReadOnlyList<VisualElementExecutionPlan> VisualElements { get; }

    /// <summary>
    /// Rasterization is required by any OCR-backed text mode or by unresolved
    /// visual analysis. Preserving an already-identified embedded visual does
    /// not require rasterization by itself.
    /// </summary>
    public bool RequiresRasterization =>
        TextMode !=
            TextExecutionMode.NativeText ||
        RequiresVisualAnalysis;

    /// <summary>
    /// Layout analysis is required by any OCR-backed text mode or by unresolved
    /// visual analysis. It is not required merely to preserve an already
    /// identified meaningful source visual.
    /// </summary>
    public bool RequiresLayoutAnalysis =>
        TextMode !=
            TextExecutionMode.NativeText ||
        RequiresVisualAnalysis;

    /// <summary>
    /// OCR is a text-axis capability only. Visual analysis by itself does not
    /// authorize OCR.
    /// </summary>
    public bool RequiresTargetedOcr =>
        TextMode !=
        TextExecutionMode.NativeText;

    /// <summary>
    /// Verification and corruption reconciliation compare native and OCR
    /// evidence. Missing-text recovery has no authoritative native text to
    /// reconcile.
    /// </summary>
    public bool RequiresNativeOcrReconciliation =>
        TextMode is
            TextExecutionMode.TargetedOcrVerification or
            TextExecutionMode.TargetedOcrReconciliation;

    public bool RequiresVisualAnalysis =>
        VisualElements.Any(
            visual =>
                visual.Action ==
                VisualExecutionAction.AnalyzeVisual);

    public bool RequiresMeaningfulVisualPreservation =>
        VisualElements.Any(
            visual =>
                visual.Action ==
                VisualExecutionAction.PreserveMeaningfulVisual);

    public bool RequiresUnqualifiedVisualPreservation =>
        VisualElements.Any(
            visual =>
                visual.Action ==
                VisualExecutionAction.PreserveUnqualifiedVisual);

    public bool RequiresVisualPreservation =>
        RequiresMeaningfulVisualPreservation ||
        RequiresUnqualifiedVisualPreservation;

    public bool HasAdditionalSemanticWork =>
        TextMode !=
            TextExecutionMode.NativeText ||
        RequiresVisualAnalysis ||
        RequiresVisualPreservation;
}
