using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Controlled non-authoritative execution of the H.3B visual axis.
///
/// NoAdditionalSemanticProcessing performs no visual I/O.
///
/// PreserveMeaningfulVisual exercises the H.4D.3A exact source-occurrence
/// materializer and writes to <see cref="Stream.Null"/> because shadow execution
/// must not persist or publish candidate assets.
///
/// AnalyzeVisual renders one full-page raster and performs one neutral layout
/// analysis per affected page. This runner has no OCR dependency and cannot
/// authorize text recognition.
///
/// The authoritative DocumentIngestionResult is already built before
/// DocumentProcessor invokes this runner.
/// </summary>
public sealed class DocumentControlledCandidateVisualExecutionRunner
{
    private readonly DocumentControlledCandidateVisualExecutionDependencies
        _dependencies;

    public DocumentControlledCandidateVisualExecutionRunner(
        DocumentControlledCandidateVisualExecutionDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    public async ValueTask<DocumentControlledCandidateVisualExecutionReport>
        RunAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            DocumentShadowPlanningReport shadowPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            shadowPlanning);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        cancellationToken.ThrowIfCancellationRequested();

        DocumentControlledCandidateVisualExecutionReport report;

        int? currentPhysicalPageNumber =
            null;

        int? currentSourceVisualIndex =
            null;

        try
        {
            if (!shadowPlanning.IsCompleted)
            {
                report =
                    new DocumentControlledCandidateVisualExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateVisualExecutionStatus
                            .PlanningUnavailable,
                        pages:
                            []);
            }
            else
            {
                ValidateCoverage(
                    format,
                    extraction,
                    shadowPlanning,
                    sourceDocumentSha256);

                var hasPreservation =
                    shadowPlanning.Pages.Any(
                        page =>
                            page
                                .Shadow
                                .Candidate
                                .Plan
                                .RequiresMeaningfulVisualPreservation);

                var hasAnalysis =
                    shadowPlanning.Pages.Any(
                        page =>
                            page
                                .Shadow
                                .Candidate
                                .Plan
                                .RequiresVisualAnalysis);

                if (hasPreservation &&
                    !_dependencies
                        .SourceVisualAssetMaterializer
                        .CanMaterialize(
                            format))
                {
                    var firstPreservation =
                        FirstVisualAction(
                            shadowPlanning,
                            VisualExecutionAction.PreserveMeaningfulVisual);

                    currentPhysicalPageNumber =
                        firstPreservation.PhysicalPageNumber;

                    currentSourceVisualIndex =
                        firstPreservation.SourceVisualIndex;

                    throw new NotSupportedException(
                        $"The controlled candidate source-visual materializer cannot " +
                        $"process format '{format}'.");
                }

                if (hasAnalysis &&
                    !_dependencies
                        .DocumentRasterizer
                        .CanRasterize(
                            format))
                {
                    var firstAnalysis =
                        FirstVisualAction(
                            shadowPlanning,
                            VisualExecutionAction.AnalyzeVisual);

                    currentPhysicalPageNumber =
                        firstAnalysis.PhysicalPageNumber;

                    currentSourceVisualIndex =
                        firstAnalysis.SourceVisualIndex;

                    throw new NotSupportedException(
                        $"The controlled candidate visual rasterizer cannot process " +
                        $"format '{format}'.");
                }

                var pages =
                    new List<DocumentControlledCandidateVisualPageExecution>(
                        extraction.Pages.Count);

                IDocumentRasterizationSession? rasterSession =
                    null;

                try
                {
                    if (hasAnalysis)
                    {
                        var firstAnalysis =
                            FirstVisualAction(
                                shadowPlanning,
                                VisualExecutionAction.AnalyzeVisual);

                        currentPhysicalPageNumber =
                            firstAnalysis.PhysicalPageNumber;

                        currentSourceVisualIndex =
                            firstAnalysis.SourceVisualIndex;

                        rasterSession =
                            await _dependencies
                                .DocumentRasterizer
                                .OpenAsync(
                                    source,
                                    format,
                                    cancellationToken)
                                .ConfigureAwait(false);

                        if (rasterSession is null)
                        {
                            throw new InvalidDataException(
                                "Controlled candidate visual rasterizer returned no session.");
                        }
                    }

                    for (var pageIndex = 0;
                         pageIndex <
                         extraction.Pages.Count;
                         pageIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var extractionPage =
                            extraction.Pages[
                                pageIndex];

                        var shadowPage =
                            shadowPlanning.Pages[
                                pageIndex];

                        var candidatePlan =
                            shadowPage
                                .Shadow
                                .Candidate
                                .Plan;

                        currentPhysicalPageNumber =
                            extractionPage.PhysicalPageNumber;

                        currentSourceVisualIndex =
                            null;

                        RasterRenderResult? analysisRaster =
                            null;

                        LayoutAnalysisResult? analysisLayout =
                            null;

                        if (candidatePlan.RequiresVisualAnalysis)
                        {
                            if (rasterSession is null)
                            {
                                throw new InvalidOperationException(
                                    "AnalyzeVisual reached execution without a " +
                                    "document-scoped controlled raster session.");
                            }

                            currentSourceVisualIndex =
                                candidatePlan
                                    .VisualElements
                                    .First(
                                        visual =>
                                            visual.Action ==
                                            VisualExecutionAction.AnalyzeVisual)
                                    .SourceVisualIndex;

                            await using var rasterBytes =
                                new MemoryStream();

                            analysisRaster =
                                await rasterSession
                                    .RenderPageAsync(
                                        extractionPage.PhysicalPageNumber,
                                        rasterBytes,
                                        cancellationToken)
                                    .ConfigureAwait(false);

                            ValidateAnalysisRaster(
                                extractionPage,
                                analysisRaster);

                            Rewind(
                                rasterBytes);

                            analysisLayout =
                                await _dependencies
                                    .LayoutAnalyzer
                                    .AnalyzeAsync(
                                        rasterBytes,
                                        extractionPage.PhysicalPageNumber,
                                        analysisRaster.OutputPixelWidth,
                                        analysisRaster.OutputPixelHeight,
                                        cancellationToken)
                                    .ConfigureAwait(false);

                            ValidateAnalysisLayout(
                                extractionPage,
                                analysisLayout);
                        }

                        var elements =
                            new List<DocumentControlledCandidateVisualElementExecution>(
                                candidatePlan.VisualElements.Count);

                        foreach (var visualPlan in
                                 candidatePlan
                                     .VisualElements
                                     .OrderBy(
                                         visual =>
                                             visual.SourceVisualIndex))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            currentSourceVisualIndex =
                                visualPlan.SourceVisualIndex;

                            switch (visualPlan.Action)
                            {
                                case VisualExecutionAction
                                    .NoAdditionalSemanticProcessing:
                                    elements.Add(
                                        new DocumentControlledCandidateVisualElementExecution(
                                            visualPlan.SourceVisualIndex,
                                            visualPlan.Action));

                                    break;

                                case VisualExecutionAction
                                    .PreserveMeaningfulVisual:
                                {
                                    var materialization =
                                        await _dependencies
                                            .SourceVisualAssetMaterializer
                                            .MaterializeAsync(
                                                source,
                                                format,
                                                extraction,
                                                extractionPage.PhysicalPageNumber,
                                                visualPlan.SourceVisualIndex,
                                                Stream.Null,
                                                cancellationToken)
                                            .ConfigureAwait(false);

                                    ValidatePreservation(
                                        extractionPage,
                                        visualPlan,
                                        materialization);

                                    elements.Add(
                                        new DocumentControlledCandidateVisualElementExecution(
                                            visualPlan.SourceVisualIndex,
                                            visualPlan.Action,
                                            materialization));

                                    break;
                                }

                                case VisualExecutionAction.AnalyzeVisual:
                                    if (analysisRaster is null ||
                                        analysisLayout is null)
                                    {
                                        throw new InvalidOperationException(
                                            "AnalyzeVisual reached element execution without " +
                                            "page-level raster/layout evidence.");
                                    }

                                    elements.Add(
                                        new DocumentControlledCandidateVisualElementExecution(
                                            visualPlan.SourceVisualIndex,
                                            visualPlan.Action));

                                    break;

                                default:
                                    throw new InvalidOperationException(
                                        $"Unsupported controlled visual action " +
                                        $"'{visualPlan.Action}'.");
                            }
                        }

                        pages.Add(
                            new DocumentControlledCandidateVisualPageExecution(
                                extractionPage.PhysicalPageNumber,
                                shadowPage
                                    .AuthoritativeLegacy
                                    .Plan
                                    .Route,
                                elements,
                                analysisRaster,
                                analysisLayout));
                    }
                }
                finally
                {
                    if (rasterSession is not null)
                    {
                        await rasterSession
                            .DisposeAsync()
                            .ConfigureAwait(false);
                    }
                }

                report =
                    new DocumentControlledCandidateVisualExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateVisualExecutionStatus.Completed,
                        pages);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            report =
                new DocumentControlledCandidateVisualExecutionReport(
                    sourceDocumentSha256,
                    DocumentControlledCandidateVisualExecutionStatus.Failed,
                    pages:
                        [],
                    new DocumentControlledCandidateVisualExecutionFailure(
                        exception.GetType().FullName ??
                        exception.GetType().Name,
                        exception.Message,
                        currentPhysicalPageNumber,
                        currentSourceVisualIndex));
        }

        await DeliverBestEffortAsync(
                report,
                cancellationToken)
            .ConfigureAwait(false);

        return report;
    }

    private static void ValidateCoverage(
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        DocumentShadowPlanningReport shadowPlanning,
        string sourceDocumentSha256)
    {
        if (extraction.Format !=
            format)
        {
            throw new InvalidDataException(
                $"Controlled visual extraction format '{extraction.Format}' does " +
                $"not match detected format '{format}'.");
        }

        if (shadowPlanning.Format !=
            format)
        {
            throw new InvalidDataException(
                $"Controlled visual format '{format}' does not match " +
                $"shadow-planning format '{shadowPlanning.Format}'.");
        }

        if (!string.Equals(
                shadowPlanning.SourceDocumentSha256,
                sourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Controlled visual source SHA-256 does not match shadow-planning evidence.");
        }

        if (shadowPlanning.Pages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Shadow-planning page count {shadowPlanning.Pages.Count} does not " +
                $"match extraction page count {extraction.Pages.Count}.");
        }

        for (var pageIndex = 0;
             pageIndex <
             extraction.Pages.Count;
             pageIndex++)
        {
            var extractionPage =
                extraction.Pages[
                    pageIndex];

            var shadowPage =
                shadowPlanning.Pages[
                    pageIndex];

            if (shadowPage.PhysicalPageNumber !=
                extractionPage.PhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Shadow-planning page at index {pageIndex} is " +
                    $"{shadowPage.PhysicalPageNumber}; expected " +
                    $"{extractionPage.PhysicalPageNumber}.");
            }

            var visualPlans =
                shadowPage
                    .Shadow
                    .Candidate
                    .Plan
                    .VisualElements;

            if (visualPlans.Count !=
                extractionPage.RasterImageCount)
            {
                throw new InvalidDataException(
                    $"Physical page {extractionPage.PhysicalPageNumber} has " +
                    $"{extractionPage.RasterImageCount} source visual occurrence(s), " +
                    $"but the candidate visual plan contains {visualPlans.Count} element(s).");
            }

            var indexes =
                visualPlans
                    .Select(
                        visual =>
                            visual.SourceVisualIndex)
                    .OrderBy(
                        index =>
                            index)
                    .ToArray();

            for (var sourceVisualIndex = 0;
                 sourceVisualIndex <
                 extractionPage.RasterImageCount;
                 sourceVisualIndex++)
            {
                if (indexes[sourceVisualIndex] !=
                    sourceVisualIndex)
                {
                    throw new InvalidDataException(
                        $"Physical page {extractionPage.PhysicalPageNumber} visual plan " +
                        $"does not provide exact source visual coverage 0.." +
                        $"{extractionPage.RasterImageCount - 1}.");
                }
            }
        }
    }

    private static (
        int PhysicalPageNumber,
        int SourceVisualIndex)
        FirstVisualAction(
            DocumentShadowPlanningReport shadowPlanning,
            VisualExecutionAction action)
    {
        foreach (var page in
                 shadowPlanning.Pages)
        {
            var match =
                page
                    .Shadow
                    .Candidate
                    .Plan
                    .VisualElements
                    .FirstOrDefault(
                        visual =>
                            visual.Action ==
                            action);

            if (match is not null)
            {
                return (
                    page.PhysicalPageNumber,
                    match.SourceVisualIndex);
            }
        }

        throw new InvalidOperationException(
            $"Controlled candidate visual action '{action}' was expected but not found.");
    }

    private static void ValidateAnalysisRaster(
        DocumentExtractionPage extractionPage,
        RasterRenderResult raster)
    {
        ArgumentNullException.ThrowIfNull(
            raster);

        if (raster.PhysicalPageNumber !=
            extractionPage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Controlled candidate visual-analysis raster belongs to a different page.");
        }

        if (!raster.IsFullPage)
        {
            throw new InvalidDataException(
                "Controlled candidate AnalyzeVisual requires a full-page raster.");
        }
    }

    private static void ValidateAnalysisLayout(
        DocumentExtractionPage extractionPage,
        LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            layout);

        if (layout.PhysicalPageNumber !=
            extractionPage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Controlled candidate visual-analysis layout belongs to a different page.");
        }
    }

    private static void ValidatePreservation(
        DocumentExtractionPage extractionPage,
        VisualElementExecutionPlan visualPlan,
        SourceVisualAssetMaterialization materialization)
    {
        ArgumentNullException.ThrowIfNull(
            materialization);

        if (materialization.PhysicalPageNumber !=
            extractionPage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Controlled candidate preserved source visual belongs to a different page.");
        }

        if (materialization.SourceVisualIndex !=
            visualPlan.SourceVisualIndex)
        {
            throw new InvalidDataException(
                "Controlled candidate preserved source visual index does not match its plan.");
        }
    }

    private static void Rewind(
        Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException(
                "Internal controlled visual-analysis buffer must be seekable.");
        }

        stream.Position =
            0;
    }

    private async ValueTask DeliverBestEffortAsync(
        DocumentControlledCandidateVisualExecutionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dependencies
                .Observer
                .ObserveAsync(
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            // Controlled candidate visual telemetry is non-authoritative.
        }
    }
}
