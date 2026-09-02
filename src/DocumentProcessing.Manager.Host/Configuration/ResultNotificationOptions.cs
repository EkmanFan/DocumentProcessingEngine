namespace DocumentProcessing.Manager.Host.Configuration;

internal sealed record ResultNotificationObserver(
    string ConsumerId,
    Uri CallbackUrl,
    string SharedSecret);

internal sealed record ResultNotificationOptions(
    IReadOnlyList<ResultNotificationObserver> Observers,
    TimeSpan ReconciliationInterval,
    TimeSpan RetryInterval)
{
    public static ResultNotificationOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var observers = configuration
            .GetSection("ManagerNotifications:Observers")
            .GetChildren()
            .Select(ReadObserver)
            .ToArray();

        var duplicateConsumer = observers
            .GroupBy(observer => observer.ConsumerId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateConsumer is not null)
        {
            throw new InvalidOperationException(
                $"Manager notification consumer '{duplicateConsumer.Key}' is configured more than once.");
        }

        return new ResultNotificationOptions(
            observers,
            ReadPositiveSeconds(
                configuration,
                "ManagerNotifications:ReconciliationSeconds",
                300),
            ReadPositiveSeconds(
                configuration,
                "ManagerNotifications:RetrySeconds",
                10));
    }

    private static ResultNotificationObserver ReadObserver(
        IConfigurationSection section)
    {
        var consumerId = Require(section["ConsumerId"], "ConsumerId");
        var callbackText = Require(section["CallbackUrl"], "CallbackUrl");
        var sharedSecret = Require(section["SharedSecret"], "SharedSecret");

        if (!Uri.TryCreate(callbackText, UriKind.Absolute, out var callbackUrl) ||
            !UsesAllowedTransport(callbackUrl))
        {
            throw new InvalidOperationException(
                "Manager notification callback URLs must use HTTPS, except for loopback HTTP.");
        }

        if (sharedSecret.Length < 32 || ContainsNewLine(sharedSecret))
        {
            throw new InvalidOperationException(
                "Manager notification shared secrets must contain at least 32 characters and no line breaks.");
        }

        if (ContainsNewLine(consumerId))
        {
            throw new InvalidOperationException(
                "Manager notification consumer IDs cannot contain line breaks.");
        }

        return new ResultNotificationObserver(
            consumerId,
            callbackUrl,
            sharedSecret);
    }

    private static TimeSpan ReadPositiveSeconds(
        IConfiguration configuration,
        string key,
        int defaultSeconds)
    {
        var seconds = configuration.GetValue<int?>(key) ?? defaultSeconds;
        return seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive number of seconds.");
    }

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Manager notification observer '{name}' is required.")
            : value.Trim();

    private static bool UsesAllowedTransport(Uri value) =>
        value.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        (value.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
         value.IsLoopback);

    private static bool ContainsNewLine(string value) =>
        value.Contains('\r') || value.Contains('\n');
}
