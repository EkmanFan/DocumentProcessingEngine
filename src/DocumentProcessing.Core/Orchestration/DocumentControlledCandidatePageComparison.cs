namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Cross-axis comparison evidence for one physical page.
///
/// The page combines the already-executed controlled text and controlled visual
/// evidence. It does not itself execute OCR, layout, rasterization or visual
/// persistence.
/// </summary>
public sealed record DocumentControlledCandidatePageComparison
{
    public DocumentControlledCandidatePageComparison(
        int physicalPageNumber,
        PageProcessingRoute authoritativeLegacyRoute,
        TextExecutionMode candidateTextMode,
        DocumentControlledCandidateTextPageStatus candidateTextStatus,
        bool? selectedTextSequenceExact,
        bool? textProjectionExact,
        IEnumerable<VisualExecutionAction> candidateVisualActions,
        bool visualPlanExecutionExact,
        bool candidateRemovesLegacyTextMl,
        bool candidateAddsIndependentVisualWorkToLegacyNativePage)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (!Enum.IsDefined(
                authoritativeLegacyRoute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoritativeLegacyRoute));
        }

        if (!Enum.IsDefined(
                candidateTextMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateTextMode));
        }

        if (!Enum.IsDefined(
                candidateTextStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateTextStatus));
        }

        ArgumentNullException.ThrowIfNull(
            candidateVisualActions);

        var actions =
            candidateVisualActions.ToArray();

        if (actions.Any(
                action =>
                    !Enum.IsDefined(
                        action)))
        {
            throw new ArgumentException(
                "Candidate visual actions must all be defined.",
                nameof(candidateVisualActions));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        AuthoritativeLegacyRoute =
            authoritativeLegacyRoute;

        CandidateTextMode =
            candidateTextMode;

        CandidateTextStatus =
            candidateTextStatus;

        SelectedTextSequenceExact =
            selectedTextSequenceExact;

        TextProjectionExact =
            textProjectionExact;

        CandidateVisualActions =
            Array.AsReadOnly(
                actions);

        VisualPlanExecutionExact =
            visualPlanExecutionExact;

        CandidateRemovesLegacyTextMl =
            candidateRemovesLegacyTextMl;

        CandidateAddsIndependentVisualWorkToLegacyNativePage =
            candidateAddsIndependentVisualWorkToLegacyNativePage;
    }

    public int PhysicalPageNumber { get; }

    public PageProcessingRoute AuthoritativeLegacyRoute { get; }

    public TextExecutionMode CandidateTextMode { get; }

    public DocumentControlledCandidateTextPageStatus CandidateTextStatus { get; }

    public bool? SelectedTextSequenceExact { get; }

    public bool? TextProjectionExact { get; }

    public IReadOnlyList<VisualExecutionAction> CandidateVisualActions { get; }

    public bool VisualPlanExecutionExact { get; }

    public bool CandidateRemovesLegacyTextMl { get; }

    public bool CandidateAddsIndependentVisualWorkToLegacyNativePage { get; }

    public bool TextExecutionComplete =>
        CandidateTextStatus !=
        DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode;

    public bool CandidateHasMeaningfulVisualPreservation =>
        CandidateVisualActions.Contains(
            VisualExecutionAction.PreserveMeaningfulVisual);

    public bool CandidateRequiresVisualAnalysis =>
        CandidateVisualActions.Contains(
            VisualExecutionAction.AnalyzeVisual);
}
