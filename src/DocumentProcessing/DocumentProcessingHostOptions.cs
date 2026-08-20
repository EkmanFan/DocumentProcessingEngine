using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;

namespace DocumentProcessing;

/// <summary>
/// Consumer configuration for one <see cref="DocumentProcessingHost"/>.
/// </summary>
/// <remarks>
/// The V1 composition root selects concrete shared Layout/OCR providers
/// explicitly. The optional visual-destination callback is format-neutral and
/// applies to layout-driven preserved visuals.
/// </remarks>
public sealed class DocumentProcessingHostOptions
{
    #region ctor

    public DocumentProcessingHostOptions(
        string engineVersion,
        PpStructureV3Options ppStructureV3,
        PaddleOcrOptions paddleOcr,
        PreservedLayoutVisualDestinationFactory?
            openPreservedLayoutVisualDestinationAsync = null)
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

        OpenPreservedLayoutVisualDestinationAsync =
            openPreservedLayoutVisualDestinationAsync;
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
    /// Gets the optional destination factory for preserved layout visuals.
    /// </summary>
    public PreservedLayoutVisualDestinationFactory?
        OpenPreservedLayoutVisualDestinationAsync { get; }

    #endregion
}
