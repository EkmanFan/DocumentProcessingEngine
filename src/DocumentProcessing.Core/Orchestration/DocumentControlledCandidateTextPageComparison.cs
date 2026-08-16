namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Comparison evidence for one physical page in H.4D.1.
///
/// Only NativeText candidate pages carry execution/comparison metrics.
/// OCR-backed candidate modes remain explicitly deferred. Visual work is an
/// independent axis and is only reported as pending in this increment.
/// </summary>
public sealed record DocumentControlledCandidateTextPageComparison
{
    public DocumentControlledCandidateTextPageComparison(
        int physicalPageNumber,
        PageProcessingRoute authoritativeLegacyRoute,
        TextExecutionMode candidateTextMode,
        DocumentControlledCandidateTextPageStatus status,
        bool candidateRemovesLegacyTextMl,
        bool candidateHasIndependentVisualWork,
        bool? selectedTextSequenceExact = null,
        bool? textProjectionExact = null,
        int? authoritativeTextElementCount = null,
        int? candidateTextElementCount = null,
        int? authoritativeReconciliationEvidenceCount = null,
        int? candidateReconciliationEvidenceCount = null)
    {
        if (physicalPageNumber <= 0)
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
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        var hasAllExecutionMetrics =
            selectedTextSequenceExact.HasValue &&
            textProjectionExact.HasValue &&
            authoritativeTextElementCount.HasValue &&
            candidateTextElementCount.HasValue &&
            authoritativeReconciliationEvidenceCount.HasValue &&
            candidateReconciliationEvidenceCount.HasValue;

        var hasAnyExecutionMetric =
            selectedTextSequenceExact.HasValue ||
            textProjectionExact.HasValue ||
            authoritativeTextElementCount.HasValue ||
            candidateTextElementCount.HasValue ||
            authoritativeReconciliationEvidenceCount.HasValue ||
            candidateReconciliationEvidenceCount.HasValue;

        switch (status)
        {
            case DocumentControlledCandidateTextPageStatus.ExecutedNativeText:
                if (candidateTextMode !=
                    TextExecutionMode.NativeText)
                {
                    throw new ArgumentException(
                        "ExecutedNativeText requires candidate NativeText mode.",
                        nameof(candidateTextMode));
                }

                if (!hasAllExecutionMetrics)
                {
                    throw new ArgumentException(
                        "ExecutedNativeText requires complete comparison metrics.");
                }

                ValidateNonNegative(
                    authoritativeTextElementCount!.Value,
                    nameof(authoritativeTextElementCount));

                ValidateNonNegative(
                    candidateTextElementCount!.Value,
                    nameof(candidateTextElementCount));

                ValidateNonNegative(
                    authoritativeReconciliationEvidenceCount!.Value,
                    nameof(authoritativeReconciliationEvidenceCount));

                ValidateNonNegative(
                    candidateReconciliationEvidenceCount!.Value,
                    nameof(candidateReconciliationEvidenceCount));
                break;

            case DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode:
                if (candidateTextMode ==
                    TextExecutionMode.NativeText)
                {
                    throw new ArgumentException(
                        "NativeText must be executed rather than deferred in H.4D.1.",
                        nameof(candidateTextMode));
                }

                if (hasAnyExecutionMetric)
                {
                    throw new ArgumentException(
                        "Deferred candidate text modes cannot carry execution metrics.");
                }

                if (candidateRemovesLegacyTextMl)
                {
                    throw new ArgumentException(
                        "A deferred non-native text mode cannot remove legacy text ML.",
                        nameof(candidateRemovesLegacyTextMl));
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        AuthoritativeLegacyRoute =
            authoritativeLegacyRoute;

        CandidateTextMode =
            candidateTextMode;

        Status =
            status;

        CandidateRemovesLegacyTextMl =
            candidateRemovesLegacyTextMl;

        CandidateHasIndependentVisualWork =
            candidateHasIndependentVisualWork;

        SelectedTextSequenceExact =
            selectedTextSequenceExact;

        TextProjectionExact =
            textProjectionExact;

        AuthoritativeTextElementCount =
            authoritativeTextElementCount;

        CandidateTextElementCount =
            candidateTextElementCount;

        AuthoritativeReconciliationEvidenceCount =
            authoritativeReconciliationEvidenceCount;

        CandidateReconciliationEvidenceCount =
            candidateReconciliationEvidenceCount;
    }

    public int PhysicalPageNumber { get; }

    public PageProcessingRoute AuthoritativeLegacyRoute { get; }

    public TextExecutionMode CandidateTextMode { get; }

    public DocumentControlledCandidateTextPageStatus Status { get; }

    public bool CandidateRemovesLegacyTextMl { get; }

    /// <summary>
    /// True means H.3C selected visual analysis and/or meaningful visual
    /// preservation. H.4D.1 does not execute that visual work.
    /// </summary>
    public bool CandidateHasIndependentVisualWork { get; }

    public bool? SelectedTextSequenceExact { get; }

    /// <summary>
    /// Stronger than selected text equality. Includes ordered element role,
    /// bounds, selected origin, native source-sequence identity, and whether
    /// reconciliation evidence is attached.
    /// </summary>
    public bool? TextProjectionExact { get; }

    public int? AuthoritativeTextElementCount { get; }

    public int? CandidateTextElementCount { get; }

    public int? AuthoritativeReconciliationEvidenceCount { get; }

    public int? CandidateReconciliationEvidenceCount { get; }

    private static void ValidateNonNegative(
        int value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName);
        }
    }
}
