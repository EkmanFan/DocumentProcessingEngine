using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.DualRun;

namespace DocumentProcessing.Engine.DualRun.InProcess;

/// <summary>
/// Explicit opt-in composition for Dual Run candidate text execution.
///
/// The one-argument constructor provides NativeText-only candidate execution.
/// The four-argument constructor additionally enables OCR-backed
/// candidate text execution.
///
/// Visual execution is intentionally absent from both compositions.
/// </summary>
public sealed class DocumentDualRunCandidateTextExecutionDependencies
{
    #region Variables and Constants

    #endregion

    #region ctor

    public DocumentDualRunCandidateTextExecutionDependencies(
        IDocumentDualRunCandidateTextExecutionObserver observer)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));
    }

    public DocumentDualRunCandidateTextExecutionDependencies(
        IDocumentDualRunCandidateTextExecutionObserver observer,
        IDocumentRasterizer documentRasterizer,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer)
        : this(
            observer)
    {
        DocumentRasterizer =
            documentRasterizer ??
            throw new ArgumentNullException(
                nameof(documentRasterizer));

        LayoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));

        TextRecognizer =
            textRecognizer ??
            throw new ArgumentNullException(
                nameof(textRecognizer));
    }

    #endregion

    #region Properties

    public IDocumentDualRunCandidateTextExecutionObserver Observer { get; }

    internal IDocumentRasterizer? DocumentRasterizer { get; }

    internal IPageLayoutAnalyzer? LayoutAnalyzer { get; }

    internal IRegionTextRecognizer? TextRecognizer { get; }

    #endregion

    #region Methods

    internal bool CanExecuteOcrBackedText =>
        DocumentRasterizer is not null &&
        LayoutAnalyzer is not null &&
        TextRecognizer is not null;

    #endregion
}
