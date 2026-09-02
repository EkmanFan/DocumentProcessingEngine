using System.Globalization;
using Microsoft.AspNetCore.Localization;
using DocumentProcessing.Manager.Blazor.Components;
using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace DocumentProcessing.Manager.Blazor;

/// <summary>
/// Executable server-side Blazor adapter for the document-processing Manager.
/// </summary>
public static class Program
{
    #region Methods Entry Point

    /// <summary>
    /// Configures and runs the Manager workshop user interface.
    /// </summary>
    public static void Main(
        string[] args)
    {
        var builder =
            WebApplication.CreateBuilder(
                args);

        var embeddingOptions =
            ManagerEmbeddingOptions.Load(
                builder.Configuration);

        builder.Services.AddSingleton(
            embeddingOptions);

        if (embeddingOptions.IsEnabled)
        {
            builder.Services.AddAntiforgery(
                options =>
                    options.SuppressXFrameOptionsHeader =
                        true);
        }

        builder.Services
            .AddDocumentProcessingManagerWorkshop(
                builder.Configuration);

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.Configure<RequestLocalizationOptions>(
            options =>
            {
                var supportedCultures =
                    new[]
                    {
                        new CultureInfo(
                            "en"),
                        new CultureInfo(
                            "fr")
                    };

                options.DefaultRequestCulture =
                    new RequestCulture(
                        "en");

                options.SupportedCultures =
                    supportedCultures;

                options.SupportedUICultures =
                    supportedCultures;

                options.RequestCultureProviders =
                [
                    new CookieRequestCultureProvider()
                ];
            });

        var application =
            builder.Build();

        if (!application.Environment.IsDevelopment())
        {
            application.UseExceptionHandler(
                "/error");
        }

        application.UseStatusCodePagesWithReExecute(
            "/not-found",
            createScopeForStatusCodePages:
                true);

        application.UseRequestLocalization();

        if (embeddingOptions.IsEnabled)
        {
            application.Use(
                async (
                    context,
                    next) =>
                {
                    context.Response.OnStarting(
                        () =>
                        {
                            context.Response.Headers[
                                    HeaderNames.ContentSecurityPolicy] =
                                embeddingOptions.FrameAncestorsPolicy;

                            context.Response.Headers.Remove(
                                HeaderNames.XFrameOptions);

                            return Task.CompletedTask;
                        });

                    await next(
                            context)
                        .ConfigureAwait(false);
                });
        }

        application.UseAntiforgery();

        application.MapStaticAssets();

        application
            .MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        application.Run();
    }

    #endregion
}
