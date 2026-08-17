using DocumentProcessing.Core.Hybrid;

using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative comparison evidence for one physical page.
///
/// H.4D.1 executes NativeText. H.4D.2B additionally permits controlled
/// OCR-backed text execution when explicitly composed. Independent visual work
/// remains outside this report's execution authority.
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
        int? candidateReconciliationEvidenceCount = null,
        HybridDocumentPage? candidatePage = null,
        IEnumerable<LayoutObservation>?
            candidateLayoutVisualObservations = null)
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

        TextExecutionMode? expectedMode =
            status switch
            {
                DocumentControlledCandidateTextPageStatus.ExecutedNativeText =>
                    TextExecutionMode.NativeText,

                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrRecovery =>
                    TextExecutionMode.TargetedOcrRecovery,

                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrVerification =>
                    TextExecutionMode.TargetedOcrVerification,

                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrReconciliation =>
                    TextExecutionMode.TargetedOcrReconciliation,

                DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode =>
                    null,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(status))
            };

        if (status ==
            DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode)
        {
            if (candidateTextMode ==
                TextExecutionMode.NativeText)
            {
                throw new ArgumentException(
                    "NativeText must be executed rather than deferred.",
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
        }
        else
        {
            if (candidateTextMode !=
                expectedMode)
            {
                throw new ArgumentException(
                    $"Controlled status '{status}' requires candidate text mode " +
                    $"'{expectedMode}', observed '{candidateTextMode}'.",
                    nameof(candidateTextMode));
            }

            if (!hasAllExecutionMetrics)
            {
                throw new ArgumentException(
                    "Executed candidate text modes require complete comparison metrics.");
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

            if (status !=
                    DocumentControlledCandidateTextPageStatus.ExecutedNativeText &&
                candidateRemovesLegacyTextMl)
            {
                throw new ArgumentException(
                    "Only an executed NativeText candidate can remove legacy text ML.",
                    nameof(candidateRemovesLegacyTextMl));
            }
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

        if (candidatePage is not null &&
            candidatePage.PhysicalPageNumber !=
                physicalPageNumber)
        {
            throw new ArgumentException(
                "Retained candidate page must belong to the comparison page.",
                nameof(candidatePage));
        }

        if (status ==
                DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode &&
            candidatePage is not null)
        {
            throw new ArgumentException(
                "Deferred candidate text execution cannot retain an executed page.",
                nameof(candidatePage));
        }

        var visualObservations =
            candidateLayoutVisualObservations?.ToArray() ??
            [];

        if (visualObservations.Any(
                observation =>
                    observation is null))
        {
            throw new ArgumentException(
                "Candidate layout visual observations cannot contain null values.",
                nameof(candidateLayoutVisualObservations));
        }

        if (visualObservations.Any(
                observation =>
                    observation.PhysicalPageNumber !=
                        physicalPageNumber ||
                    observation.Kind !=
                        LayoutObservationKind.Figure))
        {
            throw new ArgumentException(
                "Candidate layout visual observations must be same-page Figure evidence.",
                nameof(candidateLayoutVisualObservations));
        }

        if (visualObservations
            .GroupBy(
                observation =>
                    observation.ObservationSequence)
            .Any(
                group =>
                    group.Count() >
                    1))
        {
            throw new ArgumentException(
                "Candidate layout visual observations cannot duplicate observation sequence.",
                nameof(candidateLayoutVisualObservations));
        }

        if (visualObservations.Length >
                0 &&
            status is not (
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrRecovery or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrVerification or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrReconciliation))
        {
            throw new ArgumentException(
                "Only executed OCR-backed text pages can carry candidate layout visual observations.",
                nameof(candidateLayoutVisualObservations));
        }

        CandidatePage =
            candidatePage;

        CandidateLayoutVisualObservations =
            Array.AsReadOnly(
                visualObservations);
    }

    public int PhysicalPageNumber { get; }

    public PageProcessingRoute AuthoritativeLegacyRoute { get; }

    public TextExecutionMode CandidateTextMode { get; }

    public DocumentControlledCandidateTextPageStatus Status { get; }

    public bool CandidateRemovesLegacyTextMl { get; }

    /// <summary>
    /// True means H.3C selected visual analysis and/or meaningful visual
    /// preservation. H.4D.2B still does not execute that visual work.
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

    /// <summary>
    /// Actual executed candidate page retained for H.4D.4B projection.
    /// Null remains valid for deferred/manual legacy comparison evidence.
    /// </summary>
    public HybridDocumentPage? CandidatePage { get; }

    public IReadOnlyList<LayoutObservation>
        CandidateLayoutVisualObservations { get; }

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
