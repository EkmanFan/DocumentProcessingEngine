namespace DocumentProcessing.Manager.Blazor.Configuration;

internal sealed record ManagerIdentityOptions(
    Uri ApologiaConnectAddress,
    string SharedSecret,
    string Issuer,
    string Audience,
    TimeSpan SessionLifetime)
{
    public static ManagerIdentityOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectAddress = RequireUri(
            configuration["ManagerIdentity:ApologiaConnectUrl"],
            "ManagerIdentity:ApologiaConnectUrl");
        var sharedSecret = configuration["ManagerIdentity:SharedSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(sharedSecret) || sharedSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "ManagerIdentity:SharedSecret must contain at least 32 characters.");
        }

        var sessionMinutes = configuration.GetValue<int?>(
            "ManagerIdentity:SessionLifetimeMinutes") ?? 5;
        if (sessionMinutes is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "ManagerIdentity:SessionLifetimeMinutes must be between 1 and 30.");
        }

        return new ManagerIdentityOptions(
            connectAddress,
            sharedSecret,
            ReadOrDefault(
                configuration["ManagerIdentity:Issuer"],
                "apologia-studio"),
            ReadOrDefault(
                configuration["ManagerIdentity:Audience"],
                "document-manager-ui"),
            TimeSpan.FromMinutes(sessionMinutes));
    }

    private static string ReadOrDefault(string? value, string defaultValue)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? defaultValue : trimmed;
    }

    private static Uri RequireUri(string? value, string key)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var address) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Fragment) ||
            !(address.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
              address.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && address.IsLoopback))
        {
            throw new InvalidOperationException(
                $"{key} must be an absolute HTTPS URI, except for local loopback HTTP.");
        }

        return address;
    }
}
