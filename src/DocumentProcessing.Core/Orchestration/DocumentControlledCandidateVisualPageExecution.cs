using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative controlled visual-execution evidence for one physical page.
///
/// Source visual elements are complete and ordered by zero-based source visual
/// index. AnalyzeVisual shares one full-page raster/layout result per page.
/// </summary>
public sealed record DocumentControlledCandidateVisualPageExecution
{
    public DocumentControlledCandidateVisualPageExecution(
        int physicalPageNumber,
        PageProcessingRoute authoritativeLegacyRoute,
        IEnumerable<DocumentControlledCandidateVisualElementExecution> elements,
        RasterRenderResult? analysisRaster = null,
        LayoutAnalysisResult? analysisLayout = null)
    {
        if (physicalPageNumber <=
            0)
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

        ArgumentNullException.ThrowIfNull(
            elements);

        var materialized =
            elements.ToArray();

        for (var index = 0;
             index <
             materialized.Length;
             index++)
        {
            var element =
                materialized[index] ??
                throw new ArgumentException(
                    "Controlled visual page elements cannot contain null values.",
                    nameof(elements));

            if (element.SourceVisualIndex !=
                index)
            {
                throw new ArgumentException(
                    $"Controlled visual page elements must provide complete ordered " +
                    $"source visual coverage; expected index {index}, observed " +
                    $"{element.SourceVisualIndex}.",
                    nameof(elements));
            }

            if (element.Materialization is { } preservation &&
                preservation.PhysicalPageNumber !=
                physicalPageNumber)
            {
                throw new ArgumentException(
                    "Preserved source visual belongs to a different physical page.",
                    nameof(elements));
            }
        }

        var requiresAnalysis =
            materialized.Any(
                element =>
                    element.Action ==
                    VisualExecutionAction.AnalyzeVisual);

        if (requiresAnalysis)
        {
            if (analysisRaster is null)
            {
                throw new ArgumentNullException(
                    nameof(analysisRaster),
                    "AnalyzeVisual requires one page-level raster result.");
            }

            if (analysisLayout is null)
            {
                throw new ArgumentNullException(
                    nameof(analysisLayout),
                    "AnalyzeVisual requires one page-level layout-analysis result.");
            }

            if (analysisRaster.PhysicalPageNumber !=
                    physicalPageNumber ||
                !analysisRaster.IsFullPage)
            {
                throw new ArgumentException(
                    "AnalyzeVisual requires a full-page raster for the same physical page.",
                    nameof(analysisRaster));
            }

            if (analysisLayout.PhysicalPageNumber !=
                physicalPageNumber)
            {
                throw new ArgumentException(
                    "AnalyzeVisual layout result belongs to a different physical page.",
                    nameof(analysisLayout));
            }
        }
        else if (analysisRaster is not null ||
                 analysisLayout is not null)
        {
            throw new ArgumentException(
                "A page without AnalyzeVisual cannot carry page-level analysis evidence.");
        }

        PhysicalPageNumber =
            physicalPageNumber;

        AuthoritativeLegacyRoute =
            authoritativeLegacyRoute;

        Elements =
            Array.AsReadOnly(
                materialized);

        AnalysisRaster =
            analysisRaster;

        AnalysisLayout =
            analysisLayout;
    }

    public int PhysicalPageNumber { get; }

    public PageProcessingRoute AuthoritativeLegacyRoute { get; }

    public IReadOnlyList<DocumentControlledCandidateVisualElementExecution> Elements { get; }

    public RasterRenderResult? AnalysisRaster { get; }

    public LayoutAnalysisResult? AnalysisLayout { get; }

    public bool HasIndependentVisualWork =>
        Elements.Any(
            element =>
                element.Action is
                    VisualExecutionAction.PreserveMeaningfulVisual or
                    VisualExecutionAction.AnalyzeVisual);

    public bool CandidateAddsIndependentVisualWorkToLegacyNativePage =>
        AuthoritativeLegacyRoute ==
            PageProcessingRoute.NativeOnly &&
        HasIndependentVisualWork;

    public int PreservationElementCount =>
        Elements.Count(
            element =>
                element.Action ==
                VisualExecutionAction.PreserveMeaningfulVisual);

    public int AnalysisElementCount =>
        Elements.Count(
            element =>
                element.Action ==
                VisualExecutionAction.AnalyzeVisual);

    public int NoAdditionalSemanticProcessingElementCount =>
        Elements.Count(
            element =>
                element.Action ==
                VisualExecutionAction.NoAdditionalSemanticProcessing);
}
