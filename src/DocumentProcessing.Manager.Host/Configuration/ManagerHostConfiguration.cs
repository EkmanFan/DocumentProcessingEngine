using DocumentProcessing.ProviderLifecycle;
using DocumentProcessing.Manager.DPEngine;

namespace DocumentProcessing.Manager.Host.Configuration;

internal sealed class ManagerHostConfiguration
{
    #region Variables and Constants

    private const long
        DefaultMaximumArtifactBytes =
            2L *
            1024 *
            1024 *
            1024;

    #endregion

    #region Properties

    public string ConnectionString { get; }

    public string ApiKey { get; }

    public string ConsumerApiKey { get; }

    public string? DeliveryReplayApiKey { get; }

    public TimeSpan ConsumerClaimDuration { get; }

    public string SourceRoot { get; }

    public string ResultRoot { get; }

    public long MaximumSourceBytes { get; }

    public long MaximumResultBytes { get; }

    public string WorkerId { get; }

    public string EngineVersion { get; }

    public Uri LayoutEndpoint { get; }

    public Uri OcrEndpoint { get; }

    public string OcrProfileId { get; }

    public ProcessingProviderLifecycleOptions ProviderLifecycle { get; }

    public TimeSpan ProcessingLeaseDuration { get; }

    public TimeSpan ProcessingLeaseRenewalInterval { get; }

    public TimeSpan RuntimeLeaseDuration { get; }

    public TimeSpan RuntimeLeaseRenewalInterval { get; }

    public TimeSpan IdlePollingInterval { get; }

    public int MaximumAttempts { get; }

    public int ComplexDocumentPageThreshold { get; }

    public bool AllowPermanentDeletion { get; }

    #endregion

    #region ctor

    private ManagerHostConfiguration(
        string connectionString,
        string apiKey,
        string consumerApiKey,
        string? deliveryReplayApiKey,
        TimeSpan consumerClaimDuration,
        string sourceRoot,
        string resultRoot,
        long maximumSourceBytes,
        long maximumResultBytes,
        string workerId,
        string engineVersion,
        Uri layoutEndpoint,
        Uri ocrEndpoint,
        string ocrProfileId,
        ProcessingProviderLifecycleOptions providerLifecycle,
        TimeSpan processingLeaseDuration,
        TimeSpan processingLeaseRenewalInterval,
        TimeSpan runtimeLeaseDuration,
        TimeSpan runtimeLeaseRenewalInterval,
        TimeSpan idlePollingInterval,
        int maximumAttempts,
        int complexDocumentPageThreshold,
        bool allowPermanentDeletion)
    {
        ConnectionString =
            connectionString;

        ApiKey =
            apiKey;

        ConsumerApiKey =
            consumerApiKey;

        DeliveryReplayApiKey =
            deliveryReplayApiKey;

        ConsumerClaimDuration =
            consumerClaimDuration;

        SourceRoot =
            sourceRoot;

        ResultRoot =
            resultRoot;

        MaximumSourceBytes =
            maximumSourceBytes;

        MaximumResultBytes =
            maximumResultBytes;

        WorkerId =
            workerId;

        EngineVersion =
            engineVersion;

        LayoutEndpoint =
            layoutEndpoint;

        OcrEndpoint =
            ocrEndpoint;

        OcrProfileId =
            ocrProfileId;

        ProviderLifecycle =
            providerLifecycle;

        ProcessingLeaseDuration =
            processingLeaseDuration;

        ProcessingLeaseRenewalInterval =
            processingLeaseRenewalInterval;

        RuntimeLeaseDuration =
            runtimeLeaseDuration;

        RuntimeLeaseRenewalInterval =
            runtimeLeaseRenewalInterval;

        IdlePollingInterval =
            idlePollingInterval;

        MaximumAttempts =
            maximumAttempts;

        ComplexDocumentPageThreshold =
            complexDocumentPageThreshold;

        AllowPermanentDeletion =
            allowPermanentDeletion;
    }

    #endregion

    #region Methods Factory

    public static ManagerHostConfiguration Load(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        var connectionString =
            Require(
                configuration.GetConnectionString(
                    "ManagerPostgres"),
                "ConnectionStrings:ManagerPostgres");

        var apiKey =
            Require(
                configuration["ManagerHost:ApiKey"],
                "ManagerHost:ApiKey");

        if (apiKey.Length <
            32)
        {
            throw new InvalidOperationException(
                "ManagerHost:ApiKey must contain at least 32 characters.");
        }

        var consumerApiKey =
            Require(
                configuration["ManagerHost:ConsumerApiKey"],
                "ManagerHost:ConsumerApiKey");

        if (consumerApiKey.Length < 32)
        {
            throw new InvalidOperationException(
                "ManagerHost:ConsumerApiKey must contain at least 32 characters.");
        }

        if (string.Equals(apiKey, consumerApiKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Manager UI and consumer API keys must be distinct.");
        }

        var deliveryReplayApiKey = ReadOptional(
            configuration["ManagerHost:DeliveryReplayApiKey"]);
        if (deliveryReplayApiKey is not null &&
            deliveryReplayApiKey.Length < 32)
        {
            throw new InvalidOperationException(
                "ManagerHost:DeliveryReplayApiKey must contain at least 32 characters when configured.");
        }

        if (deliveryReplayApiKey is not null &&
            (string.Equals(deliveryReplayApiKey, apiKey, StringComparison.Ordinal) ||
             string.Equals(deliveryReplayApiKey, consumerApiKey, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The delivery replay API key must be distinct from the Manager UI and consumer keys.");
        }

        var sourceRoot =
            RequireAbsolutePath(
                configuration["ManagerHost:SourceRoot"],
                "ManagerHost:SourceRoot");

        var resultRoot =
            RequireAbsolutePath(
                configuration["ManagerHost:ResultRoot"],
                "ManagerHost:ResultRoot");

        if (string.Equals(
                sourceRoot,
                resultRoot,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Manager source and result roots must be distinct directories.");
        }

        var processingLeaseDuration =
            ReadPositiveDuration(
                configuration,
                "ManagerHost:ProcessingLeaseSeconds",
                TimeSpan.FromMinutes(
                    2));

        var processingLeaseRenewalInterval =
            ReadPositiveDuration(
                configuration,
                "ManagerHost:ProcessingLeaseRenewalSeconds",
                TimeSpan.FromSeconds(
                    30));

        var runtimeLeaseDuration =
            ReadPositiveDuration(
                configuration,
                "ManagerHost:RuntimeLeaseSeconds",
                TimeSpan.FromSeconds(
                    30));

        var runtimeLeaseRenewalInterval =
            ReadPositiveDuration(
                configuration,
                "ManagerHost:RuntimeLeaseRenewalSeconds",
                TimeSpan.FromSeconds(
                    10));

        EnsureShorter(
            processingLeaseRenewalInterval,
            processingLeaseDuration,
            "ManagerHost:ProcessingLeaseRenewalSeconds");

        EnsureShorter(
            runtimeLeaseRenewalInterval,
            runtimeLeaseDuration,
            "ManagerHost:RuntimeLeaseRenewalSeconds");

        return new ManagerHostConfiguration(
            connectionString,
            apiKey,
            consumerApiKey,
            deliveryReplayApiKey,
            ReadPositiveDuration(
                configuration,
                "ManagerHost:ConsumerClaimSeconds",
                TimeSpan.FromMinutes(5)),
            sourceRoot,
            resultRoot,
            ReadPositiveLong(
                configuration,
                "ManagerHost:MaximumSourceBytes",
                DefaultMaximumArtifactBytes),
            ReadPositiveLong(
                configuration,
                "ManagerHost:MaximumResultBytes",
                DefaultMaximumArtifactBytes),
            ReadOptional(
                configuration["ManagerHost:WorkerId"]) ??
            $"{Environment.MachineName}-{Environment.ProcessId}",
            ReadOptional(
                configuration["ManagerHost:EngineVersion"]) ??
            "manager-host-v1",
            ReadHttpUri(
                configuration["ManagerHost:LayoutEndpoint"] ??
                "http://127.0.0.1:8080/layout-parsing",
                "ManagerHost:LayoutEndpoint"),
            ReadHttpUri(
                configuration["ManagerHost:OcrEndpoint"] ??
                "http://127.0.0.1:8081/ocr",
                "ManagerHost:OcrEndpoint"),
            ReadOptional(
                configuration["ManagerHost:OcrProfileId"]) ??
            "paddleocr-3.7.0-ppocrv6-medium-cpu-v1",
            ReadProviderLifecycle(
                configuration),
            processingLeaseDuration,
            processingLeaseRenewalInterval,
            runtimeLeaseDuration,
            runtimeLeaseRenewalInterval,
            TimeSpan.FromMilliseconds(
                ReadPositiveLong(
                    configuration,
                    "ManagerHost:IdlePollingMilliseconds",
                    defaultValue:
                        500)),
            ReadPositiveInt(
                configuration,
                "ManagerHost:MaximumAttempts",
                defaultValue:
                    3),
            ReadPositiveInt(
                configuration,
                "ManagerHost:ComplexDocumentPageThreshold",
                defaultValue:
                    DocumentProcessingSplitPreviewProvider.DefaultComplexDocumentPageThreshold),
            configuration.GetValue<bool>(
                "ManagerHost:AllowPermanentDeletion"));
    }

    #endregion

    #region Methods Validation

    private static string Require(
        string? value,
        string configurationKey) =>
        ReadOptional(
            value) ??
        throw new InvalidOperationException(
            $"Required configuration '{configurationKey}' is missing.");

    private static string RequireAbsolutePath(
        string? value,
        string configurationKey)
    {
        var path =
            Require(
                value,
                configurationKey);

        if (!Path.IsPathFullyQualified(
                path))
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationKey}' must be an absolute path.");
        }

        return Path.GetFullPath(
            path);
    }

    private static string? ReadOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();

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
                $"Configuration '{key}' must be positive.");
        }

        return value;
    }

    private static int ReadPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        var value =
            configuration.GetValue<int?>(
                key) ??
            defaultValue;

        if (value <=
            0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be positive.");
        }

        return value;
    }

    private static TimeSpan ReadPositiveDuration(
        IConfiguration configuration,
        string key,
        TimeSpan defaultValue) =>
        TimeSpan.FromSeconds(
            ReadPositiveLong(
                configuration,
                key,
                checked((long)defaultValue.TotalSeconds)));

    private static void EnsureShorter(
        TimeSpan interval,
        TimeSpan duration,
        string intervalKey)
    {
        if (interval >=
            duration)
        {
            throw new InvalidOperationException(
                $"Configuration '{intervalKey}' must be shorter than its lease duration.");
        }
    }

    private static Uri ReadHttpUri(
        string value,
        string configurationKey)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not
                ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationKey}' must be an absolute HTTP endpoint.");
        }

        return uri;
    }

    private static ProcessingProviderLifecycleOptions ReadProviderLifecycle(
        IConfiguration configuration)
    {
        var value =
            ReadOptional(
                configuration["ManagerHost:ProviderLifecycle"]) ??
            "managedDocker";

        return value.ToLowerInvariant() switch
        {
            "manageddocker" =>
                ProcessingProviderLifecycleOptions.CreateManagedDocker(
                    new ManagedDockerProcessingProviderOptions(
                        repositoryRoot:
                            ReadOptional(
                                configuration[
                                    "ManagerHost:ProviderRepositoryRoot"]))),
            "external" =>
                ProcessingProviderLifecycleOptions.External,
            _ =>
                throw new InvalidOperationException(
                    "Configuration 'ManagerHost:ProviderLifecycle' must be " +
                    "'managedDocker' or 'external'.")
        };
    }

    #endregion
}
