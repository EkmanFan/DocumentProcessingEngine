namespace DocumentProcessing.Manager.Blazor.Configuration;

internal sealed class ManagerEmbeddingOptions
{
    #region Variables and Constants

    private const string
        AllowedParentOriginsKey =
            "ManagerEmbedding:AllowedParentOrigins";

    #endregion

    #region Properties

    public IReadOnlyList<string> AllowedParentOrigins { get; }

    public bool IsEnabled =>
        AllowedParentOrigins.Count >
        0;

    public string FrameAncestorsPolicy =>
        $"frame-ancestors 'self' {string.Join(' ', AllowedParentOrigins)}";

    #endregion

    #region ctor

    private ManagerEmbeddingOptions(
        IReadOnlyList<string> allowedParentOrigins)
    {
        AllowedParentOrigins =
            allowedParentOrigins;
    }

    #endregion

    #region Methods Factory

    public static ManagerEmbeddingOptions Load(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        var values =
            configuration
                .GetSection(
                    AllowedParentOriginsKey)
                .Get<string[]>() ??
            [];

        var origins =
            values
                .Select(
                    ParseOrigin)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new ManagerEmbeddingOptions(
            origins);
    }

    #endregion

    #region Methods Validation

    private static string ParseOrigin(
        string value)
    {
        var trimmedValue =
            value?.Trim();

        if (!Uri.TryCreate(
                trimmedValue,
                UriKind.Absolute,
                out var address) ||
            !string.IsNullOrEmpty(
                address.UserInfo) ||
            !string.IsNullOrEmpty(
                address.Query) ||
            !string.IsNullOrEmpty(
                address.Fragment) ||
            address.AbsolutePath !=
                "/")
        {
            throw new InvalidOperationException(
                $"Configuration '{AllowedParentOriginsKey}' must contain origin-only absolute URIs.");
        }

        var usesHttps =
            address.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        var usesLoopbackHttp =
            address.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            address.IsLoopback;

        if (!usesHttps &&
            !usesLoopbackHttp)
        {
            throw new InvalidOperationException(
                $"Configuration '{AllowedParentOriginsKey}' must use HTTPS, except for a local loopback origin.");
        }

        return address.GetLeftPart(
            UriPartial.Authority);
    }

    #endregion
}
