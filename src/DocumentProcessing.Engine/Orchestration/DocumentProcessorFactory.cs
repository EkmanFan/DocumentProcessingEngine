using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Composes authoritative <see cref="DocumentProcessor"/> instances from
/// format-neutral capabilities while keeping Engine implementation details
/// private to the Engine assembly.
/// </summary>
public static class DocumentProcessorFactory
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        ReconciliationIdentity =
            new(
                "native-ocr-text-reconciler",
                "native-ocr-reconciliation-v1");

    #endregion

    #region Methods Composition

    /// <summary>
    /// Creates the current full hybrid processor from externally selected
    /// format and shared processing capabilities.
    /// </summary>
    /// <remarks>
    /// The caller supplies format-owned extraction/raster/visual capabilities
    /// and shared layout/OCR implementations. Document preflight assessment,
    /// planner selection, visual preservation, hybrid executors, visual planning
    /// dependencies and reconciliation identity are Engine responsibilities and
    /// are composed here.
    /// </remarks>
    public static DocumentProcessor CreateHybrid(
        DocumentFormatId format,
        IDocumentExtractor nativeExtractor,
        IDocumentRasterizer documentRasterizer,
        IVisualRasterObservationSource visualRasterObservationSource,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        ProcessingComponentIdentity layoutAnalysisIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            nativeExtractor);

        ArgumentNullException.ThrowIfNull(
            documentRasterizer);

        ArgumentNullException.ThrowIfNull(
            visualRasterObservationSource);

        ArgumentNullException.ThrowIfNull(
            layoutAnalyzer);

        ArgumentNullException.ThrowIfNull(
            textRecognizer);

        ArgumentNullException.ThrowIfNull(
            nativeExtractionIdentity);

        ArgumentNullException.ThrowIfNull(
            layoutAnalysisIdentity);

        var visualPreserver =
            new VisualAssetPreserver();

        var hybridExecution =
            new DocumentHybridExecutionDependencies(
                documentRasterizer,
                new MissingNativeHybridPageExecutor(
                    layoutAnalyzer,
                    textRecognizer,
                    visualPreserver),
                new NativePresentHybridPageExecutor(
                    layoutAnalyzer,
                    textRecognizer,
                    visualPreserver),
                layoutAnalysisIdentity,
                ReconciliationIdentity,
                new DocumentAuthoritativeVisualPlanningDependencies(
                    visualRasterObservationSource),
                new HealthyNativeVisualPageExecutor(
                    layoutAnalyzer,
                    visualPreserver));

        return new DocumentProcessor(
            format,
            nativeExtractor,
            new DefaultDocumentPreflightAssessor(
                format),
            DocumentPageProcessingPlanner.CreateDefault(),
            hybridExecution,
            engineVersion,
            nativeExtractionIdentity);
    }

    #endregion
}
