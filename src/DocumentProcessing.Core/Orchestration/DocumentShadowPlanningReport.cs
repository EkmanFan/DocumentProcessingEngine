using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Immutable non-authoritative report emitted by true shadow planning.
///
/// This report is diagnostics/evaluation evidence. It is not consumed by
/// DocumentProcessor to choose runtime execution.
/// </summary>
public sealed record DocumentShadowPlanningReport
{
    public DocumentShadowPlanningReport(
        string sourceDocumentSha256,
        DocumentFormatId format,
        DocumentShadowPlanningStatus status,
        IEnumerable<DocumentShadowPageComparison> pages,
        DocumentShadowPlanningFailure? failure = null)
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
                nameof(status),
                status,
                "Shadow-planning status must be defined.");
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
                    "Shadow-planning pages cannot contain null values.",
                    nameof(pages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Shadow-planning pages must be contiguous and one-based; " +
                    $"expected physical page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(pages));
            }
        }

        if (status ==
                DocumentShadowPlanningStatus.Completed &&
            failure is not null)
        {
            throw new ArgumentException(
                "Completed shadow planning cannot carry a failure.",
                nameof(failure));
        }

        if (status ==
                DocumentShadowPlanningStatus.UnsupportedFormat &&
            failure is not null)
        {
            throw new ArgumentException(
                "Unsupported-format shadow planning is not a failure.",
                nameof(failure));
        }

        if (status ==
                DocumentShadowPlanningStatus.Failed &&
            failure is null)
        {
            throw new ArgumentException(
                "Failed shadow planning must carry failure evidence.",
                nameof(failure));
        }

        SourceDocumentSha256 =
            sourceDocumentSha256.Trim();

        Format =
            format;

        Status =
            status;

        Pages =
            Array.AsReadOnly(
                materialized);

        Failure =
            failure;
    }

    public string SourceDocumentSha256 { get; }

    public DocumentFormatId Format { get; }

    public DocumentShadowPlanningStatus Status { get; }

    public IReadOnlyList<DocumentShadowPageComparison> Pages { get; }

    public DocumentShadowPlanningFailure? Failure { get; }

    public bool IsCompleted =>
        Status ==
        DocumentShadowPlanningStatus.Completed;

    public bool LegacyPlanningAgreementExact =>
        IsCompleted &&
        Pages.All(
            page =>
                page.LegacyPlanningAgreement);

    public int CandidateRemovesLegacyTextMlCount =>
        Pages.Count(
            page =>
                page.CandidateRemovesLegacyTextMl);

    public int CandidateAddsIndependentVisualWorkToLegacyNativePageCount =>
        Pages.Count(
            page =>
                page.CandidateAddsIndependentVisualWorkToLegacyNativePage);

    public int CandidateNativeTextPageCount =>
        Pages.Count(
            page =>
                page.Shadow.Candidate.Plan.TextMode ==
                TextExecutionMode.NativeText);

    public int CandidateTargetedOcrPageCount =>
        Pages.Count -
        CandidateNativeTextPageCount;

    public int CandidateVisualAnalysisPageCount =>
        Pages.Count(
            page =>
                page.Shadow.Candidate.Plan.RequiresVisualAnalysis);

    public int CandidateMeaningfulVisualPreservationPageCount =>
        Pages.Count(
            page =>
                page.Shadow.Candidate.Plan.RequiresMeaningfulVisualPreservation);
}
