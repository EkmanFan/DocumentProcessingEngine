using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace DocumentProcessing.Manager.Blazor.Security;

internal sealed class ManagerSessionPrincipalValidator(TimeProvider timeProvider)
{
    public bool IsValid(ClaimsPrincipal principal)
    {
        var expiration = principal.FindFirstValue(
            ManagerAuthenticationDefaults.SessionExpiresClaimType);
        return principal.Identity?.IsAuthenticated is true &&
               long.TryParse(expiration, out var expiresAt) &&
               expiresAt > timeProvider.GetUtcNow().ToUnixTimeSeconds();
    }
}

internal sealed class ManagerRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    ManagerSessionPrincipalValidator principalValidator)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken) =>
        Task.FromResult(principalValidator.IsValid(authenticationState.User));
}
