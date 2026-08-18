namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative evidence produced by controlled candidate text execution.
///
/// The report can be observed and evaluated but is never consumed to select the
/// authoritative document-processing result.
/// </summary>
public sealed record DocumentControlledCandidateTextExecutionReport
{
    public DocumentControlledCandidateTextExecutionReport(
        string sourceDocumentSha256,
        DocumentControlledCandidateTextExecutionStatus status,
        IEnumerable<DocumentControlledCandidateTextPageComparison> pages,
        DocumentControlledCandidateTextExecutionFailure? failure = null)
    {
        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
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
            pages.ToArray();

        for (var index = 0;
             index <
             materialized.Length;
             index++)
        {
            var page =
                materialized[index] ??
                throw new ArgumentException(
                    "Controlled candidate pages cannot contain null values.",
                    nameof(pages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Controlled candidate pages must be contiguous and one-based; " +
                    $"expected physical page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(pages));
            }
        }

        switch (status)
        {
            case DocumentControlledCandidateTextExecutionStatus.Completed:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Completed candidate execution cannot carry failure evidence.",
                        nameof(failure));
                }
                break;

            case DocumentControlledCandidateTextExecutionStatus.PlanningUnavailable:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Planning-unavailable candidate execution is not a failure.",
                        nameof(failure));
                }

                if (materialized.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Planning-unavailable candidate execution cannot carry page comparisons.",
                        nameof(pages));
                }
                break;

            case DocumentControlledCandidateTextExecutionStatus.Failed:
                if (failure is null)
                {
                    throw new ArgumentException(
                        "Failed candidate execution must carry failure evidence.",
                        nameof(failure));
                }

                if (materialized.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Failed candidate execution discards partial page comparisons.",
                        nameof(pages));
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status));
        }

        SourceDocumentSha256 =
            sourceDocumentSha256.Trim();

        Status =
            status;

        Pages =
            Array.AsReadOnly(
                materialized);

        Failure =
            failure;
    }

    public string SourceDocumentSha256 { get; }

    public DocumentControlledCandidateTextExecutionStatus Status { get; }

    public IReadOnlyList<DocumentControlledCandidateTextPageComparison> Pages { get; }

    public DocumentControlledCandidateTextExecutionFailure? Failure { get; }

    public int ExecutedNativeTextPageCount =>
        Pages.Count(
            page =>
                page.Status ==
                DocumentControlledCandidateTextPageStatus.ExecutedNativeText);

    public int ExecutedTargetedOcrRecoveryPageCount =>
        Pages.Count(
            page =>
                page.Status ==
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrRecovery);

    public int ExecutedTargetedOcrVerificationPageCount =>
        Pages.Count(
            page =>
                page.Status ==
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrVerification);

    public int ExecutedTargetedOcrReconciliationPageCount =>
        Pages.Count(
            page =>
                page.Status ==
                DocumentControlledCandidateTextPageStatus
                    .ExecutedTargetedOcrReconciliation);

    public int ExecutedOcrBackedTextPageCount =>
        ExecutedTargetedOcrRecoveryPageCount +
        ExecutedTargetedOcrVerificationPageCount +
        ExecutedTargetedOcrReconciliationPageCount;

    public int DeferredNonNativeTextPageCount =>
        Pages.Count(
            page =>
                page.Status ==
                DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode);

    public int ExecutedCandidateRemovesAuthoritativeTextMlCount =>
        Pages.Count(
            page =>
                page.Status !=
                    DocumentControlledCandidateTextPageStatus.DeferredNonNativeTextMode &&
                page.CandidateRemovesAuthoritativeTextMl);

    public int ExecutedSelectedTextAgreementCount =>
        Pages.Count(
            page =>
                page.SelectedTextSequenceExact ==
                true);

    public int ExecutedTextProjectionAgreementCount =>
        Pages.Count(
            page =>
                page.TextProjectionExact ==
                true);

    public int PendingIndependentVisualWorkPageCount =>
        Pages.Count(
            page =>
                page.CandidateHasIndependentVisualWork);
}
