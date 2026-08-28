using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.ManagerApi;
using DocumentProcessing.Manager.Blazor.Workshop;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentProcessing.Manager.Blazor.DependencyInjection;

/// <summary>
/// Registers the reusable Manager workshop component dependencies.
/// </summary>
public static class ManagerWorkshopServiceCollectionExtensions
{
    #region Methods

    /// <summary>
    /// Registers the server-side authenticated Manager HTTP adapter consumed by
    /// the workshop component. Localized presentation follows the host's
    /// ambient <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public static IServiceCollection AddDocumentProcessingManagerWorkshop(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        var options =
            ManagerApiOptions.Load(
                configuration);

        services.AddLocalization();

        services.AddSingleton(
            options);

        services.AddTransient<ManagerWorkshopUploadService>();

        services.TryAddSingleton<IManagerVisualDestinationPicker,
            LinuxDesktopVisualDestinationPicker>();

        services
            .AddHttpClient<IManagerHostClient,
                ManagerHostClient>(
                client =>
                    ConfigureClient(
                        client,
                        options,
                        options.RequestTimeout));

        services
            .AddHttpClient<IManagerSubmissionClient,
                ManagerSubmissionClient>(
                client =>
                    ConfigureClient(
                        client,
                        options,
                        options.SubmissionTimeout));

        services
            .AddHttpClient<IManagerResultClient,
                ManagerResultClient>(
                client =>
                    ConfigureClient(
                        client,
                        options,
                        options.ResultDownloadTimeout));

        return services;
    }

    private static void ConfigureClient(
        HttpClient client,
        ManagerApiOptions options,
        TimeSpan timeout)
    {
        client.BaseAddress =
            options.BaseAddress;

        client.Timeout =
            timeout;

        client.DefaultRequestHeaders.Add(
            "X-Manager-Api-Key",
            options.ApiKey);
    }

    #endregion
}
