using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Core.DualRun.Transport;

public enum DocumentDualRunWorkerResultStatus
{
    Completed,
    Failed
}

public enum DocumentDualRunWorkerFailureStage
{
    SourceValidation,
    Planning,
    CandidateExecution,
    Unexpected
}

/// <summary>
/// Sanitized worker-side failure evidence. Process launch/timeout/crash failures
/// remain parent-supervisor outcomes because the worker may be unable to write a
/// result file in those cases.
/// </summary>
public sealed record DocumentDualRunWorkerFailure
{
    #region Properties

    public DocumentDualRunWorkerFailureStage Stage { get; }

    public string ExceptionType { get; }

    public string Message { get; }

    public int? PhysicalPageNumber { get; }

    #endregion

    #region ctor

    public DocumentDualRunWorkerFailure(
        DocumentDualRunWorkerFailureStage stage,
        string exceptionType,
        string message,
        int? physicalPageNumber = null)
    {
        if (!Enum.IsDefined(
                stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage));
        }

        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        Stage =
            stage;

        ExceptionType =
            DocumentDualRunTransportValidation
                .RequiredText(
                    exceptionType,
                    nameof(exceptionType));

        Message =
            DocumentDualRunTransportValidation
                .RequiredText(
                    message,
                    nameof(message));

        PhysicalPageNumber =
            physicalPageNumber;
    }

    #endregion
}

/// <summary>
/// Compact visual evidence retained in the worker result without transporting
/// preserved raster bytes or the complete runtime evidence graph.
/// </summary>
public sealed record DocumentDualRunWorkerVisualEvidenceSummary
{
    #region Properties

    public int ObservationSequence { get; }

    public int? ReadingOrder { get; }

    public NormalizedRectangle Bounds { get; }

    public VisualEvidenceKind EvidenceKind { get; }

    public string? PreservedProfileId { get; }

    public string? PreservedMediaType { get; }

    public long? PreservedContentLength { get; }

    public string? PreservedContentSha256 { get; }

    public bool IsPreserved =>
        PreservedContentSha256 is not null;

    #endregion

    #region ctor

    public DocumentDualRunWorkerVisualEvidenceSummary(
        int observationSequence,
        int? readingOrder,
        NormalizedRectangle bounds,
        VisualEvidenceKind evidenceKind,
        string? preservedProfileId = null,
        string? preservedMediaType = null,
        long? preservedContentLength = null,
        string? preservedContentSha256 = null)
    {
        if (observationSequence <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationSequence));
        }

        if (readingOrder <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readingOrder));
        }

        if (!Enum.IsDefined(
                evidenceKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceKind));
        }

        var hasAnyPreservedField =
            preservedProfileId is not null ||
            preservedMediaType is not null ||
            preservedContentLength.HasValue ||
            preservedContentSha256 is not null;

        var hasAllPreservedFields =
            !string.IsNullOrWhiteSpace(
                preservedProfileId) &&
            !string.IsNullOrWhiteSpace(
                preservedMediaType) &&
            preservedContentLength.HasValue &&
            !string.IsNullOrWhiteSpace(
                preservedContentSha256);

        if (hasAnyPreservedField !=
            hasAllPreservedFields)
        {
            throw new ArgumentException(
                "Preserved visual summary fields must be supplied together.");
        }

        if (preservedContentLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preservedContentLength));
        }

        ObservationSequence =
            observationSequence;

        ReadingOrder =
            readingOrder;

        Bounds =
            bounds;

        EvidenceKind =
            evidenceKind;

        PreservedProfileId =
            hasAllPreservedFields
                ? DocumentDualRunTransportValidation
                    .RequiredText(
                        preservedProfileId,
                        nameof(preservedProfileId))
                : null;

        PreservedMediaType =
            hasAllPreservedFields
                ? DocumentDualRunTransportValidation
                    .RequiredText(
                        preservedMediaType,
                        nameof(preservedMediaType))
                    .ToLowerInvariant()
                : null;

        if (PreservedMediaType is not null &&
            !PreservedMediaType.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Preserved visual media type must be an image media type.",
                nameof(preservedMediaType));
        }

        PreservedContentLength =
            preservedContentLength;

        PreservedContentSha256 =
            hasAllPreservedFields
                ? DocumentDualRunTransportValidation
                    .Sha256(
                        preservedContentSha256,
                        nameof(preservedContentSha256))
                : null;
    }

    #endregion
}

/// <summary>
/// Compact page-level comparison emitted by the worker.
/// </summary>
public sealed record DocumentDualRunWorkerPageResult
{
    #region Properties

    public int PhysicalPageNumber { get; }

    public bool AuthoritativePlanningAgreement { get; }

    public TextExecutionMode CandidateTextMode { get; }

    public bool CandidateRemovesAuthoritativeTextMl { get; }

    public bool CandidateRequiresVisualAnalysis { get; }

    public bool CandidateRequiresMeaningfulVisualPreservation { get; }

    public bool CandidateHasIndependentVisualWork =>
        CandidateRequiresVisualAnalysis ||
        CandidateRequiresMeaningfulVisualPreservation;

    public DocumentDualRunCandidateTextPageStatus?
        CandidateExecutionStatus { get; }

    public bool? SelectedTextSequenceExact { get; }

    public bool? TextProjectionExact { get; }

    public int? AuthoritativeTextElementCount { get; }

    public int? CandidateTextElementCount { get; }

    public int? AuthoritativeReconciliationEvidenceCount { get; }

    public int? CandidateReconciliationEvidenceCount { get; }

    public IReadOnlyList<DocumentDualRunWorkerVisualEvidenceSummary>
        CandidateVisualEvidence { get; }

    #endregion

    #region ctor

    public DocumentDualRunWorkerPageResult(
        int physicalPageNumber,
        bool authoritativePlanningAgreement,
        TextExecutionMode candidateTextMode,
        bool candidateRemovesAuthoritativeTextMl,
        bool candidateRequiresVisualAnalysis,
        bool candidateRequiresMeaningfulVisualPreservation,
        DocumentDualRunCandidateTextPageStatus? candidateExecutionStatus = null,
        bool? selectedTextSequenceExact = null,
        bool? textProjectionExact = null,
        int? authoritativeTextElementCount = null,
        int? candidateTextElementCount = null,
        int? authoritativeReconciliationEvidenceCount = null,
        int? candidateReconciliationEvidenceCount = null,
        IEnumerable<DocumentDualRunWorkerVisualEvidenceSummary>?
            candidateVisualEvidence = null)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (!Enum.IsDefined(
                candidateTextMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateTextMode));
        }

        if (candidateExecutionStatus.HasValue &&
            !Enum.IsDefined(
                candidateExecutionStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateExecutionStatus));
        }

        var visualEvidence =
            candidateVisualEvidence
                ?.ToArray() ??
            [];

        if (visualEvidence.Any(
                evidence =>
                    evidence is null))
        {
            throw new ArgumentException(
                "Candidate visual evidence cannot contain null values.",
                nameof(candidateVisualEvidence));
        }

        if (visualEvidence
            .GroupBy(
                evidence =>
                    evidence.ObservationSequence)
            .Any(
                group =>
                    group.Count() >
                    1))
        {
            throw new ArgumentException(
                "Candidate visual evidence cannot duplicate observation sequence.",
                nameof(candidateVisualEvidence));
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

        if (!candidateExecutionStatus.HasValue)
        {
            if (hasAnyExecutionMetric ||
                visualEvidence.Length >
                0)
            {
                throw new ArgumentException(
                    "Planning-only page results cannot carry candidate execution evidence.");
            }
        }
        else if (candidateExecutionStatus.Value ==
                 DocumentDualRunCandidateTextPageStatus.DeferredNonNativeTextMode)
        {
            if (candidateTextMode ==
                TextExecutionMode.NativeText)
            {
                throw new ArgumentException(
                    "NativeText candidate execution cannot be deferred.",
                    nameof(candidateTextMode));
            }

            if (candidateRemovesAuthoritativeTextMl)
            {
                throw new ArgumentException(
                    "Deferred non-native candidate execution cannot remove authoritative text ML.",
                    nameof(candidateRemovesAuthoritativeTextMl));
            }

            if (hasAnyExecutionMetric ||
                visualEvidence.Length >
                0)
            {
                throw new ArgumentException(
                    "Deferred candidate execution cannot carry execution metrics or visual evidence.");
            }
        }
        else
        {
            if (!hasAllExecutionMetrics)
            {
                throw new ArgumentException(
                    "Executed candidate page results require complete comparison metrics.");
            }

            ValidateCount(
                authoritativeTextElementCount!.Value,
                nameof(authoritativeTextElementCount));

            ValidateCount(
                candidateTextElementCount!.Value,
                nameof(candidateTextElementCount));

            ValidateCount(
                authoritativeReconciliationEvidenceCount!.Value,
                nameof(authoritativeReconciliationEvidenceCount));

            ValidateCount(
                candidateReconciliationEvidenceCount!.Value,
                nameof(candidateReconciliationEvidenceCount));

            if (authoritativeReconciliationEvidenceCount >
                authoritativeTextElementCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoritativeReconciliationEvidenceCount));
            }

            if (candidateReconciliationEvidenceCount >
                candidateTextElementCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidateReconciliationEvidenceCount));
            }

            var expectedStatus =
                candidateTextMode switch
                {
                    TextExecutionMode.NativeText =>
                        DocumentDualRunCandidateTextPageStatus
                            .ExecutedNativeText,

                    TextExecutionMode.TargetedOcrRecovery =>
                        DocumentDualRunCandidateTextPageStatus
                            .ExecutedTargetedOcrRecovery,

                    TextExecutionMode.TargetedOcrVerification =>
                        DocumentDualRunCandidateTextPageStatus
                            .ExecutedTargetedOcrVerification,

                    TextExecutionMode.TargetedOcrReconciliation =>
                        DocumentDualRunCandidateTextPageStatus
                            .ExecutedTargetedOcrReconciliation,

                    _ =>
                        throw new ArgumentOutOfRangeException(
                            nameof(candidateTextMode))
                };

            if (candidateExecutionStatus.Value !=
                expectedStatus)
            {
                throw new ArgumentException(
                    $"Candidate text mode '{candidateTextMode}' requires execution " +
                    $"status '{expectedStatus}', observed " +
                    $"'{candidateExecutionStatus.Value}'.",
                    nameof(candidateExecutionStatus));
            }

            if (candidateTextMode !=
                    TextExecutionMode.NativeText &&
                candidateRemovesAuthoritativeTextMl)
            {
                throw new ArgumentException(
                    "Only executed NativeText candidate work can remove authoritative text ML.",
                    nameof(candidateRemovesAuthoritativeTextMl));
            }

            if (candidateTextMode ==
                    TextExecutionMode.NativeText &&
                visualEvidence.Length >
                    0)
            {
                throw new ArgumentException(
                    "NativeText candidate execution cannot carry OCR-layout visual evidence.",
                    nameof(candidateVisualEvidence));
            }
        }

        PhysicalPageNumber =
            physicalPageNumber;

        AuthoritativePlanningAgreement =
            authoritativePlanningAgreement;

        CandidateTextMode =
            candidateTextMode;

        CandidateRemovesAuthoritativeTextMl =
            candidateRemovesAuthoritativeTextMl;

        CandidateRequiresVisualAnalysis =
            candidateRequiresVisualAnalysis;

        CandidateRequiresMeaningfulVisualPreservation =
            candidateRequiresMeaningfulVisualPreservation;

        CandidateExecutionStatus =
            candidateExecutionStatus;

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

        CandidateVisualEvidence =
            Array.AsReadOnly(
                visualEvidence);
    }

    #endregion

    #region Methods Validation

    private static void ValidateCount(
        int value,
        string parameterName)
    {
        if (value <
            0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName);
        }
    }

    #endregion
}

/// <summary>
/// Versioned worker result model. Process-level loss is represented by the
/// parent supervisor and therefore does not require a result.json file.
/// </summary>
public sealed record DocumentDualRunWorkerResult
{
    #region Properties

    public Guid JobId { get; }

    public DocumentDualRunExecutionMode ExecutionMode { get; }

    public string WorkerEngineVersion { get; }

    public string SourceDocumentSha256 { get; }

    public DocumentDualRunWorkerResultStatus Status { get; }

    public IReadOnlyList<DocumentDualRunWorkerPageResult> Pages { get; }

    public DocumentDualRunWorkerFailure? Failure { get; }

    #endregion

    #region ctor

    public DocumentDualRunWorkerResult(
        Guid jobId,
        DocumentDualRunExecutionMode executionMode,
        string workerEngineVersion,
        string sourceDocumentSha256,
        DocumentDualRunWorkerResultStatus status,
        IEnumerable<DocumentDualRunWorkerPageResult> pages,
        DocumentDualRunWorkerFailure? failure = null)
    {
        if (jobId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Dual Run job ID cannot be empty.",
                nameof(jobId));
        }

        if (!Enum.IsDefined(
                executionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionMode));
        }

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        ArgumentNullException.ThrowIfNull(
            pages);

        var materialized =
            pages
                .ToArray();

        for (var index = 0;
             index <
             materialized.Length;
             index++)
        {
            var page =
                materialized[index] ??
                throw new ArgumentException(
                    "Dual Run worker pages cannot contain null values.",
                    nameof(pages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Dual Run worker pages must be contiguous and one-based; " +
                    $"expected page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(pages));
            }

            if (executionMode ==
                    DocumentDualRunExecutionMode.PlanningOnly &&
                page.CandidateExecutionStatus.HasValue)
            {
                throw new ArgumentException(
                    "PlanningOnly worker results cannot carry candidate execution status.",
                    nameof(pages));
            }

            if (executionMode ==
                    DocumentDualRunExecutionMode.Full &&
                !page.CandidateExecutionStatus.HasValue)
            {
                throw new ArgumentException(
                    "Full worker results require candidate execution status for every page.",
                    nameof(pages));
            }
        }

        switch (status)
        {
            case DocumentDualRunWorkerResultStatus.Completed:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Completed Dual Run worker result cannot carry failure evidence.",
                        nameof(failure));
                }

                if (materialized.Length ==
                    0)
                {
                    throw new ArgumentException(
                        "Completed Dual Run worker result requires page comparisons.",
                        nameof(pages));
                }

                break;

            case DocumentDualRunWorkerResultStatus.Failed:
                if (failure is null)
                {
                    throw new ArgumentException(
                        "Failed Dual Run worker result requires failure evidence.",
                        nameof(failure));
                }

                if (materialized.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Failed Dual Run worker result discards partial page comparisons.",
                        nameof(pages));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status));
        }

        JobId =
            jobId;

        ExecutionMode =
            executionMode;

        WorkerEngineVersion =
            DocumentDualRunTransportValidation
                .RequiredText(
                    workerEngineVersion,
                    nameof(workerEngineVersion));

        SourceDocumentSha256 =
            DocumentDualRunTransportValidation
                .Sha256(
                    sourceDocumentSha256,
                    nameof(sourceDocumentSha256));

        Status =
            status;

        Pages =
            Array.AsReadOnly(
                materialized);

        Failure =
            failure;
    }

    #endregion
}
