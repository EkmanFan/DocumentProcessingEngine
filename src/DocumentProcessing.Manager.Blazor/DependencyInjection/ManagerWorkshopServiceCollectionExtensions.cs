using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.ManagerApi;

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

        services
            .AddHttpClient<IManagerHostClient,
                ManagerHostClient>(
                client =>
                {
                    client.BaseAddress =
                        options.BaseAddress;

                    client.Timeout =
                        options.RequestTimeout;

                    client.DefaultRequestHeaders.Add(
                        "X-Manager-Api-Key",
                        options.ApiKey);
                });

        return services;
    }

    #endregion
}
