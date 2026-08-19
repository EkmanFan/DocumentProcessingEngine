using DocumentProcessing.Formats.Pdf;

namespace DocumentProcessing;

/// <summary>
/// Consumer configuration for one <see cref="DocumentProcessingHost"/>.
/// </summary>
/// <remarks>
/// This contract contains configuration values only. Consumers do not inject
/// document-type detectors, format processors, layout/OCR clients, or other
/// internal processing services.
/// </remarks>
public sealed class DocumentProcessingHostOptions
{
    #region ctor

    /// <summary>
    /// Creates the V1 host configuration.
    /// </summary>
    public DocumentProcessingHostOptions(
        string engineVersion,
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
    /// Gets V1 PDF runtime configuration.
    /// </summary>
    public PdfDocumentProcessingOptions Pdf { get; }

    #endregion
}
