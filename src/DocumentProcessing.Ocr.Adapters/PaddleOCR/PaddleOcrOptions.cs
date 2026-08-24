namespace DocumentProcessing.Ocr.Adapters.PaddleOCR;

/// <summary>
/// Runtime configuration for the PaddleOCR text-recognition provider.
/// </summary>
public sealed class PaddleOcrOptions
{
    #region Properties

    public Uri Endpoint { get; }

    public string ProfileId { get; }

    public TimeSpan? RequestTimeout { get; }

    #endregion

    #region ctor

    public PaddleOcrOptions(
        Uri endpoint,
        string profileId,
        TimeSpan? requestTimeout = null)
    {
        Endpoint =
            ValidateHttpEndpoint(
                endpoint,
                nameof(endpoint));

        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "OCR profile ID cannot be empty.",
                nameof(profileId));
        }

        if (requestTimeout is not null &&
            (requestTimeout <= TimeSpan.Zero ||
             requestTimeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "OCR request timeout must be finite and greater than zero.");
        }

        ProfileId =
            profileId.Trim();

        RequestTimeout =
            requestTimeout;
    }

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
