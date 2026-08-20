using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Production composition of deterministic page assessment and routing policy.
///
/// Phase 21C.3 consumes this planner from <see cref="DocumentProcessor"/> before
/// any page route is executed. The planner remains side-effect free: it
/// classifies extracted evidence and selects routes, while the processor owns
/// execution of those already-defined routes.
/// </summary>
public sealed class DocumentPageProcessingPlanner
{
    #region Variables and Constants

    private readonly IPageProcessingAssessor _assessor;
    private readonly IPageProcessingPolicy _policy;

    #endregion

    #region ctor

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

    #endregion

    #region Methods

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

    #endregion
}
