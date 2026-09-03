using System.Security.Claims;
using DocumentProcessing.Manager.Blazor.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;

namespace DocumentProcessing.Manager.Blazor.Security;

internal static class ManagerIdentityEndpoints
{
    public static IEndpointRouteBuilder MapManagerIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/apologia/login", RedirectToApologia)
            .AllowAnonymous();
        endpoints.MapPost("/auth/apologia/exchange", ExchangeAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        endpoints.MapGet(
                "/auth/access-denied",
                () => Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied"))
            .AllowAnonymous();
        return endpoints;
    }

    private static IResult RedirectToApologia(
        ManagerIdentityOptions options,
        string? returnUrl)
    {
        var destination = QueryHelpers.AddQueryString(
            options.ApologiaConnectAddress.AbsoluteUri,
            "returnUrl",
            NormalizeReturnUrl(returnUrl));
        return Results.Redirect(destination);
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext context,
        ManagerIdentityOptions options,
        ManagerSessionTicketValidator validator,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var validation = validator.ValidateAndConsume(form["ticket"]);
        if (!validation.IsValid || validation.Payload is null)
        {
            loggerFactory.CreateLogger("ManagerIdentity")
                .LogWarning(
                    "An Apologia Manager session ticket was rejected ({Failure}).",
                    validation.Failure);
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid session ticket");
        }

        var payload = validation.Payload;
        var sessionExpiresAt = timeProvider.GetUtcNow().Add(options.SessionLifetime);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload.Subject),
            new(ClaimTypes.Name, payload.DisplayName),
            new(ClaimTypes.Email, payload.Email),
            new(ManagerAuthenticationDefaults.LanguageClaimType, payload.Language),
            new(
                ManagerAuthenticationDefaults.SessionExpiresClaimType,
                sessionExpiresAt.ToUnixTimeSeconds().ToString())
        };
        claims.AddRange(payload.Permissions.Select(permission =>
            new Claim(ManagerAuthenticationDefaults.PermissionClaimType, permission)));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                ManagerAuthenticationDefaults.Scheme,
                ClaimTypes.Name,
                ClaimTypes.Role));
        await context.SignInAsync(
            ManagerAuthenticationDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = sessionExpiresAt
            });

        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(
                new RequestCulture(payload.Language)),
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = options.SessionLifetime,
                Path = "/"
            });
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        return Results.LocalRedirect(NormalizeReturnUrl(form["returnUrl"]));
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
        {
            return "/";
        }

        return returnUrl;
    }
}
