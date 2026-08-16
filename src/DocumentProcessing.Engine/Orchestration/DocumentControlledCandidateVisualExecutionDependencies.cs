using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in H.4D.3B composition for controlled candidate visual
/// execution.
///
/// The composition intentionally contains no OCR capability. Text execution
/// remains owned by the H.4D.1/H.4D.2B controlled text runtime.
/// </summary>
public sealed class DocumentControlledCandidateVisualExecutionDependencies
{
    public DocumentControlledCandidateVisualExecutionDependencies(
        IDocumentControlledCandidateVisualExecutionObserver observer,
        ISourceVisualAssetMaterializer sourceVisualAssetMaterializer,
        IDocumentRasterizer documentRasterizer,
        IPageLayoutAnalyzer layoutAnalyzer)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));

        SourceVisualAssetMaterializer =
            sourceVisualAssetMaterializer ??
            throw new ArgumentNullException(
                nameof(sourceVisualAssetMaterializer));

        DocumentRasterizer =
            documentRasterizer ??
            throw new ArgumentNullException(
                nameof(documentRasterizer));

        LayoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));
    }

    public IDocumentControlledCandidateVisualExecutionObserver Observer { get; }

    internal ISourceVisualAssetMaterializer SourceVisualAssetMaterializer { get; }

    internal IDocumentRasterizer DocumentRasterizer { get; }

    internal IPageLayoutAnalyzer LayoutAnalyzer { get; }
}
