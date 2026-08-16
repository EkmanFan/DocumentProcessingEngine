namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// H.4D.4 cross-axis evidence over H.4C planning, H.4D.2B text execution and
/// H.4D.3B visual execution.
///
/// This report is non-authoritative. ReadyForGuardedCutover can become true only
/// when every explicit blocker has been cleared by evidence.
/// </summary>
public sealed record DocumentControlledCandidateComparisonReport
{
    public DocumentControlledCandidateComparisonReport(
        string sourceDocumentSha256,
        DocumentControlledCandidateComparisonStatus status,
        IEnumerable<DocumentControlledCandidatePageComparison> pages,
        IEnumerable<DocumentControlledCandidateCutoverBlocker> cutoverBlockers,
        DocumentControlledCandidateComparisonFailure? failure = null)
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

        ArgumentNullException.ThrowIfNull(
            cutoverBlockers);

        var materializedPages =
            pages.ToArray();

        for (var index = 0;
             index <
             materializedPages.Length;
             index++)
        {
            var page =
                materializedPages[index] ??
                throw new ArgumentException(
                    "Comparison pages cannot contain null values.",
                    nameof(pages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Comparison pages must be contiguous and one-based; expected " +
                    $"physical page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(pages));
            }
        }

        var blockers =
            cutoverBlockers
                .Distinct()
                .OrderBy(
                    blocker =>
                        blocker)
                .ToArray();

        if (blockers.Any(
                blocker =>
                    !Enum.IsDefined(
                        blocker)))
        {
            throw new ArgumentException(
                "Cutover blockers must all be defined.",
                nameof(cutoverBlockers));
        }

        switch (status)
        {
            case DocumentControlledCandidateComparisonStatus.Completed:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Completed comparison cannot carry failure evidence.",
                        nameof(failure));
                }

                if (materializedPages.Length ==
                    0)
                {
                    throw new ArgumentException(
                        "Completed comparison requires page evidence.",
                        nameof(pages));
                }

                break;

            case DocumentControlledCandidateComparisonStatus.PlanningUnavailable:
            case DocumentControlledCandidateComparisonStatus.CandidateExecutionUnavailable:
                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Unavailable comparison state is not a comparison failure.",
                        nameof(failure));
                }

                if (materializedPages.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Unavailable comparison state cannot carry page evidence.",
                        nameof(pages));
                }

                break;

            case DocumentControlledCandidateComparisonStatus.Failed:
                if (failure is null)
                {
                    throw new ArgumentException(
                        "Failed comparison requires failure evidence.",
                        nameof(failure));
                }

                if (materializedPages.Length !=
                    0)
                {
                    throw new ArgumentException(
                        "Failed comparison discards partial page evidence.",
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
                materializedPages);

        CutoverBlockers =
            Array.AsReadOnly(
                blockers);

        Failure =
            failure;
    }

    public string SourceDocumentSha256 { get; }

    public DocumentControlledCandidateComparisonStatus Status { get; }

    public IReadOnlyList<DocumentControlledCandidatePageComparison> Pages { get; }

    public IReadOnlyList<DocumentControlledCandidateCutoverBlocker> CutoverBlockers { get; }

    public DocumentControlledCandidateComparisonFailure? Failure { get; }

    public bool ReadyForGuardedCutover =>
        Status ==
            DocumentControlledCandidateComparisonStatus.Completed &&
        CutoverBlockers.Count ==
            0;

    public bool PortableOutputCompared =>
        !CutoverBlockers.Contains(
            DocumentControlledCandidateCutoverBlocker.PortableOutputNotCompared);

    public bool ProvenanceCompared =>
        !CutoverBlockers.Contains(
            DocumentControlledCandidateCutoverBlocker.ProvenanceNotCompared);

    public int ExactSelectedTextPageCount =>
        Pages.Count(
            page =>
                page.SelectedTextSequenceExact ==
                true);

    public int ExactTextProjectionPageCount =>
        Pages.Count(
            page =>
                page.TextProjectionExact ==
                true);

    public int ExactVisualPlanExecutionPageCount =>
        Pages.Count(
            page =>
                page.VisualPlanExecutionExact);

    public int CandidateRemovesLegacyTextMlPageCount =>
        Pages.Count(
            page =>
                page.CandidateRemovesLegacyTextMl);

    public int CandidateAddsIndependentVisualWorkToLegacyNativePageCount =>
        Pages.Count(
            page =>
                page.CandidateAddsIndependentVisualWorkToLegacyNativePage);
}
