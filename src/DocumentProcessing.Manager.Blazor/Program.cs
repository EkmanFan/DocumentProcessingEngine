using System.Globalization;
using Microsoft.AspNetCore.Localization;
using DocumentProcessing.Manager.Blazor.Components;
using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.DependencyInjection;
using DocumentProcessing.Manager.Blazor.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
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
        var identityOptions =
            ManagerIdentityOptions.Load(
                builder.Configuration);

        builder.Services.AddSingleton(
            embeddingOptions);
        builder.Services.AddSingleton(identityOptions);
        builder.Services.AddSingleton<ManagerSessionNonceStore>();
        builder.Services.AddSingleton<ManagerSessionTicketValidator>();
        builder.Services.AddSingleton<ManagerSessionPrincipalValidator>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    ManagerAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme =
                    ManagerAuthenticationDefaults.Scheme;
                options.DefaultSignInScheme =
                    ManagerAuthenticationDefaults.Scheme;
            })
            .AddCookie(
                ManagerAuthenticationDefaults.Scheme,
                options =>
                {
                    options.Cookie.Name =
                        ".DocumentProcessing.Manager.Human";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite =
                        Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                    options.Cookie.SecurePolicy =
                        CookieSecurePolicy.SameAsRequest;
                    options.LoginPath = "/auth/apologia/login";
                    options.AccessDeniedPath = "/auth/access-denied";
                    options.ExpireTimeSpan = identityOptions.SessionLifetime;
                    options.SlidingExpiration = false;
                });
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in ManagerPermissions.All)
            {
                options.AddPolicy(
                    permission,
                    policy => policy.RequireClaim(
                        ManagerAuthenticationDefaults.PermissionClaimType,
                        permission));
            }
        });
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<
            AuthenticationStateProvider,
            ManagerRevalidatingAuthenticationStateProvider>();

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

        application.UseAuthentication();
        application.UseAuthorization();
        application.UseAntiforgery();

        application.MapStaticAssets();
        application.MapManagerIdentityEndpoints();

        application
            .MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .RequireAuthorization(ManagerPermissions.Operate);

        application.Run();
    }

    #endregion
}
