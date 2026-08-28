using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Manager.DPEngine;
using DocumentProcessing.Manager.Host.Api;
using DocumentProcessing.Manager.Host.Configuration;
using DocumentProcessing.Manager.Host.Hosting;
using DocumentProcessing.Manager.Persistence.Files;
using DocumentProcessing.Manager.Persistence.Postgres;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Runtime;
using DocumentProcessing.Manager.Submissions;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using Npgsql;

namespace DocumentProcessing.Manager.Host;

/// <summary>
/// Executable composition root for the durable document-processing Manager.
/// </summary>
public static class Program
{
    #region Methods Entry Point

    /// <summary>
    /// Configures, migrates and runs the Manager HTTP host.
    /// </summary>
    public static async Task Main(
        string[] args)
    {
        var builder =
            WebApplication.CreateBuilder(
                args);

        var configuration =
            ManagerHostConfiguration.Load(
                builder.Configuration);

        builder.WebHost.ConfigureKestrel(
            options =>
                options.Limits.MaxRequestBodySize =
                    configuration.MaximumSourceBytes);

        builder.Services.AddProblemDetails();

        builder.Services.ConfigureHttpJsonOptions(
            options =>
                options.SerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(
                        JsonNamingPolicy.CamelCase)));

        ConfigureServices(
            builder.Services,
            configuration);

        var application =
            builder.Build();

        application.UseExceptionHandler();

        ManagerApi.Map(
            application,
            configuration.ApiKey,
            configuration.MaximumSourceBytes);

        await application.Services
            .GetRequiredService<PostgresManagerSchema>()
            .InitializeAsync(
                application.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        await application
            .RunAsync()
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods Composition

    private static void ConfigureServices(
        IServiceCollection services,
        ManagerHostConfiguration configuration)
    {
        services.AddSingleton(
            configuration);

        services.AddSingleton(
            TimeProvider.System);

        services.AddSingleton(
            _ =>
                NpgsqlDataSource.Create(
                    configuration.ConnectionString));

        services.AddSingleton<PostgresManagerSchema>();

        services.AddSingleton<PostgresManagerStateStore>();
        services.AddSingleton<IManagerStateStore>(
            provider =>
                provider.GetRequiredService<PostgresManagerStateStore>());

        services.AddSingleton<PostgresManagerSettingsStore>();
        services.AddSingleton<IManagerSettingsStore>(
            provider =>
                provider.GetRequiredService<PostgresManagerSettingsStore>());

        services.AddSingleton<PostgresManagerRuntimeLeaseStore>();
        services.AddSingleton<IManagerRuntimeLeaseStore>(
            provider =>
                provider.GetRequiredService<PostgresManagerRuntimeLeaseStore>());

        services.AddSingleton<PostgresProcessingQueueStore>();
        services.AddSingleton<IProcessingQueueStore>(
            provider =>
                provider.GetRequiredService<PostgresProcessingQueueStore>());

        services.AddSingleton<PostgresProcessingQueueReader>();
        services.AddSingleton<IProcessingQueueReader>(
            provider =>
                provider.GetRequiredService<PostgresProcessingQueueReader>());
        services.AddSingleton<IProcessingHistoryReader>(
            provider =>
                provider.GetRequiredService<PostgresProcessingQueueReader>());

        services.AddSingleton<PostgresDocumentSubmissionStore>();
        services.AddSingleton<IDocumentSubmissionReader>(
            provider =>
                provider.GetRequiredService<PostgresDocumentSubmissionStore>());
        services.AddSingleton<IDocumentSubmissionWriter>(
            provider =>
                provider.GetRequiredService<PostgresDocumentSubmissionStore>());

        services.AddSingleton<PostgresProcessingResultRegistry>();
        services.AddSingleton<IProcessingResultRegistryReader>(
            provider =>
                provider.GetRequiredService<PostgresProcessingResultRegistry>());
        services.AddSingleton<IProcessingResultRegistryWriter>(
            provider =>
                provider.GetRequiredService<PostgresProcessingResultRegistry>());

        services.AddSingleton(
            _ =>
                new FileSystemSourceArtifactCustodyStore(
                    new FileSystemSourceArtifactCustodyOptions(
                        configuration.SourceRoot,
                        configuration.MaximumSourceBytes)));
        services.AddSingleton<ISourceArtifactReader>(
            provider =>
                provider.GetRequiredService<FileSystemSourceArtifactCustodyStore>());
        services.AddSingleton<ISourceArtifactWriter>(
            provider =>
                provider.GetRequiredService<FileSystemSourceArtifactCustodyStore>());

        services.AddSingleton(
            _ =>
                new FileSystemProcessingResultArtifactStore(
                    new FileSystemProcessingResultArtifactOptions(
                        configuration.ResultRoot,
                        configuration.MaximumResultBytes)));
        services.AddSingleton<IProcessingResultArtifactReader>(
            provider =>
                provider.GetRequiredService<FileSystemProcessingResultArtifactStore>());
        services.AddSingleton<IProcessingResultArtifactWriter>(
            provider =>
                provider.GetRequiredService<FileSystemProcessingResultArtifactStore>());

        services.AddSingleton<IProcessingVisualAssetStore>(
            _ =>
                new FileSystemProcessingVisualAssetStore(
                    maximumVisualBytes:
                        Math.Min(
                            FileSystemProcessingVisualAssetStore.DefaultMaximumVisualBytes,
                            configuration.MaximumResultBytes),
                    maximumVisualSetBytes:
                        configuration.MaximumResultBytes));

        services.AddSingleton(
            provider =>
                new global::DocumentProcessing.DocumentProcessingHost(
                    new global::DocumentProcessing.DocumentProcessingHostOptions(
                        configuration.EngineVersion,
                        new PpStructureV3Options(
                            configuration.LayoutEndpoint),
                        new PaddleOcrOptions(
                            configuration.OcrEndpoint,
                            configuration.OcrProfileId),
                        loggerFactory:
                            provider.GetRequiredService<ILoggerFactory>(),
                        providerLifecycle:
                            configuration.ProviderLifecycle)));

        services.AddSingleton<IDocumentProcessingResultEncoder,
            PagedDocumentProcessingResultJsonEncoder>();

        services.AddSingleton<DocumentProcessingHostExecutor>();
        services.AddSingleton<IDocumentProcessingExecutor>(
            provider =>
                provider.GetRequiredService<DocumentProcessingHostExecutor>());

        services.AddSingleton<SubmitDocumentService>();

        services.AddSingleton(
            _ =>
                new SequentialProcessingDispatcherOptions(
                    configuration.WorkerId,
                    configuration.ProcessingLeaseDuration,
                    configuration.ProcessingLeaseRenewalInterval));

        services.AddSingleton<IProcessingFailurePolicy>(
            _ =>
                new BoundedProcessingFailurePolicy(
                    configuration.MaximumAttempts));

        services.AddSingleton<SequentialProcessingDispatcher>();

        services.AddSingleton(
            _ =>
                new DocumentProcessingManagerRuntimeOptions(
                    configuration.WorkerId,
                    configuration.RuntimeLeaseDuration,
                    configuration.RuntimeLeaseRenewalInterval,
                    configuration.IdlePollingInterval));

        services.AddSingleton<DocumentProcessingManagerRuntime>();
        services.AddHostedService<ManagerRuntimeHostedService>();
    }

    #endregion
}
