using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit opt-in composition for controlled candidate text execution.
///
/// The one-argument constructor preserves H.4D.1 NativeText-only behavior.
/// The four-argument constructor additionally enables H.4D.2B OCR-backed
/// candidate text execution.
///
/// Visual execution is intentionally absent from both compositions.
/// </summary>
public sealed class DocumentControlledCandidateTextExecutionDependencies
{
    #region Variables and Constants

    #endregion

    #region ctor

    public DocumentControlledCandidateTextExecutionDependencies(
        IDocumentControlledCandidateTextExecutionObserver observer)
    {
        Observer =
            observer ??
            throw new ArgumentNullException(
                nameof(observer));
    }

    public DocumentControlledCandidateTextExecutionDependencies(
        IDocumentControlledCandidateTextExecutionObserver observer,
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

    public IDocumentControlledCandidateTextExecutionObserver Observer { get; }

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
