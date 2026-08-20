using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Pdf;

namespace DocumentProcessing;

/// <summary>
/// Consumer configuration for one <see cref="DocumentProcessingHost"/>.
/// </summary>
/// <remarks>
/// The V1 composition root selects concrete shared Layout/OCR providers
/// explicitly. Format-specific configuration remains separate.
/// </remarks>
public sealed class DocumentProcessingHostOptions
{
    #region ctor

    public DocumentProcessingHostOptions(
        string engineVersion,
        PpStructureV3Options ppStructureV3,
        PaddleOcrOptions paddleOcr,
        PdfDocumentProcessingOptions pdf)
    {
        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        EngineVersion =
            engineVersion.Trim();

        PpStructureV3 =
            ppStructureV3 ??
            throw new ArgumentNullException(
                nameof(ppStructureV3));

        PaddleOcr =
            paddleOcr ??
            throw new ArgumentNullException(
                nameof(paddleOcr));

        Pdf =
            pdf ??
            throw new ArgumentNullException(
                nameof(pdf));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the engine/build identity retained in processing provenance.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets configuration for the selected shared layout provider.
    /// </summary>
    public PpStructureV3Options PpStructureV3 { get; }

    /// <summary>
    /// Gets configuration for the selected shared OCR provider.
    /// </summary>
    public PaddleOcrOptions PaddleOcr { get; }

    /// <summary>
    /// Gets the remaining V1 PDF-specific integration configuration.
    /// </summary>
    public PdfDocumentProcessingOptions Pdf { get; }

    #endregion
}
