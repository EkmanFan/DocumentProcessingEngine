using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Visual;

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
        PageProcessingRoute authoritativeRoute,
        TextExecutionMode candidateTextMode,
        DocumentControlledCandidateTextPageStatus status,
        bool candidateRemovesAuthoritativeTextMl,
        bool candidateHasIndependentVisualWork,
        bool? selectedTextSequenceExact = null,
        bool? textProjectionExact = null,
        int? authoritativeTextElementCount = null,
        int? candidateTextElementCount = null,
        int? authoritativeReconciliationEvidenceCount = null,
        int? candidateReconciliationEvidenceCount = null,
        HybridDocumentPage? candidatePage = null,
        IEnumerable<LayoutVisualEvidence>?
            candidateLayoutVisualEvidence = null,
        IEnumerable<PreservedVisualEvidence>?
            candidatePreservedLayoutVisuals = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (!Enum.IsDefined(
                authoritativeRoute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoritativeRoute));
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

            if (candidateRemovesAuthoritativeTextMl)
            {
                throw new ArgumentException(
                    "A deferred non-native text mode cannot remove legacy text ML.",
                    nameof(candidateRemovesAuthoritativeTextMl));
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
                candidateRemovesAuthoritativeTextMl)
            {
                throw new ArgumentException(
                    "Only an executed NativeText candidate can remove legacy text ML.",
                    nameof(candidateRemovesAuthoritativeTextMl));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        AuthoritativeRoute =
            authoritativeRoute;

        CandidateTextMode =
            candidateTextMode;

        Status =
            status;

        CandidateRemovesAuthoritativeTextMl =
            candidateRemovesAuthoritativeTextMl;

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

        var visualEvidence =
            candidateLayoutVisualEvidence?.ToArray() ??
            [];

        if (visualEvidence.Any(
                evidence =>
                    evidence is null))
        {
            throw new ArgumentException(
                "Candidate layout visual evidence cannot contain null values.",
                nameof(candidateLayoutVisualEvidence));
        }

        if (visualEvidence.Any(
                evidence =>
                    evidence.Observation.PhysicalPageNumber !=
                    physicalPageNumber))
        {
            throw new ArgumentException(
                "Candidate layout visual evidence must belong to the comparison page.",
                nameof(candidateLayoutVisualEvidence));
        }

        if (visualEvidence
            .GroupBy(
                evidence =>
                    evidence.Observation.ObservationSequence)
            .Any(
                group =>
                    group.Count() >
                    1))
        {
            throw new ArgumentException(
                "Candidate layout visual evidence cannot duplicate observation sequence.",
                nameof(candidateLayoutVisualEvidence));
        }

        if (visualEvidence.Length >
                0 &&
            status is not (
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrRecovery or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrVerification or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrReconciliation))
        {
            throw new ArgumentException(
                "Only executed OCR-backed text pages can carry candidate layout visual evidence.",
                nameof(candidateLayoutVisualEvidence));
        }

        var preservedLayoutVisuals =
            candidatePreservedLayoutVisuals?.ToArray() ??
            [];

        if (preservedLayoutVisuals.Any(
                visual =>
                    visual is null))
        {
            throw new ArgumentException(
                "Candidate preserved layout visuals cannot contain null values.",
                nameof(candidatePreservedLayoutVisuals));
        }

        if (preservedLayoutVisuals.Any(
                visual =>
                    visual.SourceLayoutObservation.PhysicalPageNumber !=
                    physicalPageNumber))
        {
            throw new ArgumentException(
                "Candidate preserved layout visuals must belong to the comparison page.",
                nameof(candidatePreservedLayoutVisuals));
        }

        if (preservedLayoutVisuals
            .GroupBy(
                visual =>
                    visual.SourceLayoutObservation.ObservationSequence)
            .Any(
                group =>
                    group.Count() >
                    1))
        {
            throw new ArgumentException(
                "Candidate preserved layout visuals cannot duplicate observation sequence.",
                nameof(candidatePreservedLayoutVisuals));
        }

        if (preservedLayoutVisuals.Any(
                visual =>
                    !visualEvidence.Any(
                        evidence =>
                            evidence.Observation.Equals(
                                visual.SourceLayoutObservation))))
        {
            throw new ArgumentException(
                "Every preserved layout visual must retain matching candidate layout visual evidence.",
                nameof(candidatePreservedLayoutVisuals));
        }

        if (preservedLayoutVisuals.Length >
                0 &&
            status is not (
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrRecovery or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrVerification or
                DocumentControlledCandidateTextPageStatus.ExecutedTargetedOcrReconciliation))
        {
            throw new ArgumentException(
                "Only executed OCR-backed candidate pages can carry preserved layout visuals.",
                nameof(candidatePreservedLayoutVisuals));
        }

        CandidatePage =
            candidatePage;

        CandidateLayoutVisualEvidence =
            Array.AsReadOnly(
                visualEvidence);

        CandidatePreservedLayoutVisuals =
            Array.AsReadOnly(
                preservedLayoutVisuals);
    }

    public int PhysicalPageNumber { get; }

    public PageProcessingRoute AuthoritativeRoute { get; }

    public TextExecutionMode CandidateTextMode { get; }

    public DocumentControlledCandidateTextPageStatus Status { get; }

    public bool CandidateRemovesAuthoritativeTextMl { get; }

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

    public IReadOnlyList<LayoutVisualEvidence>
        CandidateLayoutVisualEvidence { get; }

    /// <summary>
    /// Non-authoritative regional visual materialization retained from the
    /// controlled OCR-backed candidate path. These values are evidence only;
    /// they do not transfer document authority or choose persistence.
    /// </summary>
    public IReadOnlyList<PreservedVisualEvidence>
        CandidatePreservedLayoutVisuals { get; }

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
