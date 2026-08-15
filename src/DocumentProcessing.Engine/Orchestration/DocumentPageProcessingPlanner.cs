using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Production composition of deterministic page assessment and routing policy.
///
/// Phase 21B intentionally stops at planning. The end-to-end
/// <see cref="DocumentProcessor"/> remains unchanged until Phase 21C can execute
/// the non-native routes instead of temporarily planning them and immediately
/// throwing.
/// </summary>
public sealed class DocumentPageProcessingPlanner
{
    private readonly IPageProcessingAssessor _assessor;
    private readonly IPageProcessingPolicy _policy;

    public DocumentPageProcessingPlanner(
        IPageProcessingAssessor assessor,
        IPageProcessingPolicy policy)
    {
        _assessor =
            assessor ??
            throw new ArgumentNullException(
                nameof(assessor));

        _policy =
            policy ??
            throw new ArgumentNullException(
                nameof(policy));
    }

    public IReadOnlyList<PageProcessingDecision> Plan(
        DocumentExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        var decisions =
            new PageProcessingDecision[
                extraction.Pages.Count];

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var page =
                extraction.Pages[index];

            var assessment =
                _assessor.Assess(
                    page);

            if (assessment.PhysicalPageNumber !=
                page.PhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Page assessor returned physical page {assessment.PhysicalPageNumber} " +
                    $"for extraction page {page.PhysicalPageNumber}.");
            }

            var plan =
                _policy.Decide(
                    assessment);

            decisions[index] =
                new PageProcessingDecision(
                    assessment,
                    plan);
        }

        return decisions;
    }

    public static DocumentPageProcessingPlanner CreateDefault() =>
        new(
            new DefaultPageProcessingAssessor(),
            new DefaultPageProcessingPolicy());
}
