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
    /// Compatibility composition that still owns native extraction.
    /// </summary>
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
            nativeExtractionIdentity);

        return new DocumentProcessor(
            format,
            nativeExtractor,
            new DefaultDocumentPreflightAssessor(
                format),
            DocumentPageProcessingPlanner.CreateDefault(),
            CreateHybridExecutionDependencies(
                documentRasterizer,
                visualRasterObservationSource,
                layoutAnalyzer,
                textRecognizer,
                layoutAnalysisIdentity),
            engineVersion,
            nativeExtractionIdentity);
    }

    /// <summary>
    /// Engine-owned paged/hybrid strategy for already acquired native evidence.
    /// </summary>
    internal static DocumentProcessor CreatePreparedHybrid(
        DocumentFormatId format,
        IDocumentRasterizer documentRasterizer,
        IVisualRasterObservationSource visualRasterObservationSource,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        ProcessingComponentIdentity layoutAnalysisIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            nativeExtractionIdentity);

        return new DocumentProcessor(
            format,
            new DefaultDocumentPreflightAssessor(
                format),
            DocumentPageProcessingPlanner.CreateDefault(),
            CreateHybridExecutionDependencies(
                documentRasterizer,
                visualRasterObservationSource,
                layoutAnalyzer,
                textRecognizer,
                layoutAnalysisIdentity),
            engineVersion,
            nativeExtractionIdentity);
    }

    /// <summary>
    /// Engine-owned native-only strategy for formats that expose no current
    /// paged/raster enrichment capability.
    /// </summary>
    internal static DocumentProcessor CreatePreparedNative(
        DocumentFormatId format,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            nativeExtractionIdentity);

        return new DocumentProcessor(
            format,
            new DefaultDocumentPreflightAssessor(
                format),
            DocumentPageProcessingPlanner.CreateDefault(),
            hybridExecution:
                null,
            engineVersion,
            nativeExtractionIdentity);
    }

    #endregion

    #region Methods Hybrid Dependencies

    private static DocumentHybridExecutionDependencies
        CreateHybridExecutionDependencies(
            IDocumentRasterizer documentRasterizer,
            IVisualRasterObservationSource visualRasterObservationSource,
            IPageLayoutAnalyzer layoutAnalyzer,
            IRegionTextRecognizer textRecognizer,
            ProcessingComponentIdentity layoutAnalysisIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            documentRasterizer);

        ArgumentNullException.ThrowIfNull(
            visualRasterObservationSource);

        ArgumentNullException.ThrowIfNull(
            layoutAnalyzer);

        ArgumentNullException.ThrowIfNull(
            textRecognizer);

        ArgumentNullException.ThrowIfNull(
            layoutAnalysisIdentity);

        var visualPreserver =
            new VisualAssetPreserver();

        return new DocumentHybridExecutionDependencies(
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
    }

    #endregion
}
