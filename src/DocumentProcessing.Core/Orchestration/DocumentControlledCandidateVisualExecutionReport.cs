namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative evidence produced by controlled candidate visual execution.
///
/// This report is diagnostics/evaluation evidence only. It never authorizes
/// candidate output to replace the authoritative legacy result.
/// </summary>
public sealed record DocumentControlledCandidateVisualExecutionReport
{
    public DocumentControlledCandidateVisualExecutionReport(
        string sourceDocumentSha256,
        DocumentControlledCandidateVisualExecutionStatus status,
        IEnumerable<DocumentControlledCandidateVisualPageExecution> pages,
        DocumentControlledCandidateVisualExecutionFailure? failure = null)
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
                    "Controlled visual pages cannot contain null values.",
                    nameof(pages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Controlled visual pages must be contiguous and one-based; " +
                    $"expected physical page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(pages));
            }
        }

        switch (status)
        {
            case DocumentControlledCandidateVisualExecutionStatus.Completed:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Completed controlled visual execution cannot carry failure evidence.",
                        nameof(failure));
                }

                break;

            case DocumentControlledCandidateVisualExecutionStatus.PlanningUnavailable:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Planning-unavailable controlled visual execution is not a failure.",
                        nameof(failure));
                }

                if (materialized.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Planning-unavailable controlled visual execution cannot carry pages.",
                        nameof(pages));
                }

                break;

            case DocumentControlledCandidateVisualExecutionStatus.Failed:
                if (failure is null)
                {
                    throw new ArgumentException(
                        "Failed controlled visual execution requires failure evidence.",
                        nameof(failure));
                }

                if (materialized.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Failed controlled visual execution discards partial page evidence.",
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

    public DocumentControlledCandidateVisualExecutionStatus Status { get; }

    public IReadOnlyList<DocumentControlledCandidateVisualPageExecution> Pages { get; }

    public DocumentControlledCandidateVisualExecutionFailure? Failure { get; }

    public int NoAdditionalSemanticProcessingElementCount =>
        Pages.Sum(
            page =>
                page.NoAdditionalSemanticProcessingElementCount);

    public int PreservationElementCount =>
        Pages.Sum(
            page =>
                page.PreservationElementCount);

    public int AnalysisElementCount =>
        Pages.Sum(
            page =>
                page.AnalysisElementCount);

    public int AnalyzedPageCount =>
        Pages.Count(
            page =>
                page.AnalysisElementCount >
                0);

    public int CandidateAddsIndependentVisualWorkToLegacyNativePageCount =>
        Pages.Count(
            page =>
                page.CandidateAddsIndependentVisualWorkToLegacyNativePage);
}
