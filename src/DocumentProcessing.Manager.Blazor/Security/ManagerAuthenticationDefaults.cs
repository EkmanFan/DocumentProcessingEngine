namespace DocumentProcessing.Manager.Blazor.Security;

internal static class ManagerAuthenticationDefaults
{
    public const string Scheme = "ApologiaManagerSession";
    public const string PermissionClaimType = "apologia.permission";
    public const string LanguageClaimType = "apologia.interface_language";
    public const string SessionExpiresClaimType = "apologia.manager_session_expires";
}

internal static class ManagerPermissions
{
    public const string Operate = "manager.operate";
    public const string ReplayDelivery = "manager.delivery.replay";
    public const string PurgeCustody = "manager.custody.purge";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Operate,
            ReplayDelivery,
            PurgeCustody
        };
}
