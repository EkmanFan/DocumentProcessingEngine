namespace DocumentProcessing.Formats.Pdf;

/// <summary>
/// Configuration values required by the current V1 PDF processing strategy.
/// </summary>
/// <remarks>
/// These are runtime configuration values, not injected processing services.
/// The Host/strategy composition code owns the concrete PP-StructureV3,
/// PaddleOCR, PdfPig, raster, planning, and reconciliation objects.
///
/// Provider decoupling remains explicitly deferred; the generic Engine does not
/// depend on any of these PDF runtime details.
/// </remarks>
public sealed class PdfDocumentProcessingOptions
{
    #region ctor

    /// <summary>
    /// Creates the current PDF strategy configuration.
    /// </summary>
    public PdfDocumentProcessingOptions(
        Uri layoutEndpoint,
        Uri ocrEndpoint,
        string ocrProfileId,
        PdfPreservedVisualDestinationFactory?
            openPreservedVisualDestinationAsync = null,
        TimeSpan? layoutRequestTimeout = null,
        TimeSpan? ocrRequestTimeout = null)
    {
        LayoutEndpoint =
            ValidateHttpEndpoint(
                layoutEndpoint,
                nameof(layoutEndpoint));

        OcrEndpoint =
            ValidateHttpEndpoint(
                ocrEndpoint,
                nameof(ocrEndpoint));

        if (string.IsNullOrWhiteSpace(
                ocrProfileId))
        {
            throw new ArgumentException(
                "OCR profile ID cannot be empty.",
                nameof(ocrProfileId));
        }

        if (layoutRequestTimeout is not null &&
            (layoutRequestTimeout <= TimeSpan.Zero ||
             layoutRequestTimeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(layoutRequestTimeout),
                "Layout request timeout must be finite and greater than zero.");
        }

        if (ocrRequestTimeout is not null &&
            (ocrRequestTimeout <= TimeSpan.Zero ||
             ocrRequestTimeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ocrRequestTimeout),
                "OCR request timeout must be finite and greater than zero.");
        }

        OcrProfileId =
            ocrProfileId.Trim();

        OpenPreservedVisualDestinationAsync =
            openPreservedVisualDestinationAsync;

        LayoutRequestTimeout =
            layoutRequestTimeout;

        OcrRequestTimeout =
            ocrRequestTimeout;
    }

    #endregion

    #region Properties

    public Uri LayoutEndpoint { get; }

    public Uri OcrEndpoint { get; }

    public string OcrProfileId { get; }

    public PdfPreservedVisualDestinationFactory?
        OpenPreservedVisualDestinationAsync { get; }

    public TimeSpan? LayoutRequestTimeout { get; }

    public TimeSpan? OcrRequestTimeout { get; }

    #endregion

    #region Methods Validation

    private static Uri ValidateHttpEndpoint(
        Uri endpoint,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            endpoint);

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp &&
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Processing endpoint must be an absolute HTTP or HTTPS URI.",
                parameterName);
        }

        return endpoint;
    }

    #endregion
}
