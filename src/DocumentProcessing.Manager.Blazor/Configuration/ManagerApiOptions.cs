namespace DocumentProcessing.Manager.Blazor.Configuration;

internal sealed class ManagerApiOptions
{
    #region Variables and Constants

    private const long
        DefaultMaximumUploadBytes =
            2L *
            1024 *
            1024 *
            1024;

    #endregion

    #region Properties

    public Uri BaseAddress { get; }

    public string ApiKey { get; }

    public TimeSpan RefreshInterval { get; }

    public TimeSpan RequestTimeout { get; }

    public TimeSpan SubmissionTimeout { get; }

    public long MaximumUploadBytes { get; }

    #endregion

    #region ctor

    private ManagerApiOptions(
        Uri baseAddress,
        string apiKey,
        TimeSpan refreshInterval,
        TimeSpan requestTimeout,
        TimeSpan submissionTimeout,
        long maximumUploadBytes)
    {
        BaseAddress =
            baseAddress;

        ApiKey =
            apiKey;

        RefreshInterval =
            refreshInterval;

        RequestTimeout =
            requestTimeout;

        SubmissionTimeout =
            submissionTimeout;

        MaximumUploadBytes =
            maximumUploadBytes;
    }

    #endregion

    #region Methods Factory

    public static ManagerApiOptions Load(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        var rawBaseAddress =
            Require(
                configuration["ManagerApi:BaseAddress"],
                "ManagerApi:BaseAddress");

        if (!Uri.TryCreate(
                rawBaseAddress,
                UriKind.Absolute,
                out var baseAddress) ||
            baseAddress.Scheme is not
                ("http" or "https"))
        {
            throw new InvalidOperationException(
                "Configuration 'ManagerApi:BaseAddress' must be an absolute HTTP endpoint.");
        }

        var apiKey =
            Require(
                configuration["ManagerApi:ApiKey"],
                "ManagerApi:ApiKey");

        if (apiKey.Length <
            32)
        {
            throw new InvalidOperationException(
                "ManagerApi:ApiKey must contain at least 32 characters.");
        }

        return new ManagerApiOptions(
            EnsureTrailingSlash(
                baseAddress),
            apiKey,
            ReadSeconds(
                configuration,
                "ManagerApi:RefreshIntervalSeconds",
                defaultValue:
                    2,
                maximumValue:
                    60),
            ReadSeconds(
                configuration,
                "ManagerApi:RequestTimeoutSeconds",
                defaultValue:
                    30,
                maximumValue:
                    300),
            ReadSeconds(
                configuration,
                "ManagerApi:SubmissionTimeoutSeconds",
                defaultValue:
                    3600,
                maximumValue:
                    86400),
            ReadPositiveLong(
                configuration,
                "ManagerApi:MaximumUploadBytes",
                DefaultMaximumUploadBytes));
    }

    #endregion

    #region Methods Validation

    private static string Require(
        string? value,
        string key) =>
        string.IsNullOrWhiteSpace(
            value)
            ? throw new InvalidOperationException(
                $"Required configuration '{key}' is missing.")
            : value.Trim();

    private static Uri EnsureTrailingSlash(
        Uri value) =>
        value.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? value
            : new Uri(
                $"{value.AbsoluteUri}/",
                UriKind.Absolute);

    private static TimeSpan ReadSeconds(
        IConfiguration configuration,
        string key,
        int defaultValue,
        int maximumValue)
    {
        var value =
            configuration.GetValue<int?>(
                key) ??
            defaultValue;

        if (value <=
                0 ||
            value >
                maximumValue)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be between 1 and {maximumValue} seconds.");
        }

        return TimeSpan.FromSeconds(
            value);
    }

    private static long ReadPositiveLong(
        IConfiguration configuration,
        string key,
        long defaultValue)
    {
        var value =
            configuration.GetValue<long?>(
                key) ??
            defaultValue;

        if (value <=
            0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be greater than zero.");
        }

        return value;
    }

    #endregion
}
