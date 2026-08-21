using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Epub;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing;

/// <summary>
/// Consumer configuration for one <see cref="DocumentProcessingHost"/>.
/// </summary>
/// <remarks>
/// The V1 composition root selects concrete shared Layout/OCR providers and
/// bounded EPUB acquisition explicitly. The optional visual-destination
/// callback is format-neutral and applies to Engine-selected preserved visuals.
/// </remarks>
public sealed class DocumentProcessingHostOptions
{
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
    /// Gets the optional user-provided writer for visual assets selected by the
    /// Engine for preservation.
    /// </summary>
    public UserVisualAssetWriter?
        UserVisualAssetWriter { get; }

    /// <summary>
    /// Gets bounded EPUB validation and native-acquisition configuration.
    /// </summary>
    public EpubDocumentFormatOptions Epub { get; }

    /// <summary>
    /// Gets the optional application logger factory for internal technical
    /// diagnostics. Diagnostic details never enter processing results.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; }

    #endregion

    #region ctor

    public DocumentProcessingHostOptions(
        string engineVersion,
        PpStructureV3Options ppStructureV3,
        PaddleOcrOptions paddleOcr,
        UserVisualAssetWriter?
            userVisualAssetWriter = null,
        EpubDocumentFormatOptions?
            epub = null,
        ILoggerFactory?
            loggerFactory = null)
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

        UserVisualAssetWriter =
            userVisualAssetWriter;

        Epub =
            epub ??
            new EpubDocumentFormatOptions();

        LoggerFactory =
            loggerFactory;
    }

    #endregion
}
