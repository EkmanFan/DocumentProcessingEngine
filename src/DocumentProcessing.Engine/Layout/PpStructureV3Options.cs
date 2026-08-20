namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Runtime configuration for the PP-StructureV3 layout-analysis provider.
/// </summary>
public sealed class PpStructureV3Options
{
    #region ctor

    public PpStructureV3Options(
        Uri endpoint,
        TimeSpan? requestTimeout = null)
    {
        Endpoint =
            ValidateHttpEndpoint(
                endpoint,
                nameof(endpoint));

        if (requestTimeout is not null &&
            (requestTimeout <= TimeSpan.Zero ||
             requestTimeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "Layout request timeout must be finite and greater than zero.");
        }

        RequestTimeout =
            requestTimeout;
    }

    #endregion

    #region Properties

    public Uri Endpoint { get; }

    public TimeSpan? RequestTimeout { get; }

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
