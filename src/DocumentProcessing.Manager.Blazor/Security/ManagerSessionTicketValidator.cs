using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.Configuration;
using Microsoft.AspNetCore.WebUtilities;

namespace DocumentProcessing.Manager.Blazor.Security;

internal sealed record ManagerSessionTicketPayload(
    string Issuer,
    string Audience,
    string Subject,
    string DisplayName,
    string Email,
    string Language,
    IReadOnlyList<string> Permissions,
    long IssuedAtUnixSeconds,
    long ExpiresAtUnixSeconds,
    string Nonce);

internal sealed record ManagerSessionTicketValidation(
    bool IsValid,
    ManagerSessionTicketPayload? Payload,
    string Failure)
{
    public static ManagerSessionTicketValidation Success(
        ManagerSessionTicketPayload payload) => new(true, payload, string.Empty);

    public static ManagerSessionTicketValidation Reject(string failure) =>
        new(false, null, failure);
}

internal sealed class ManagerSessionNonceStore
{
    private readonly ConcurrentDictionary<string, long> _consumed =
        new(StringComparer.Ordinal);

    public bool TryConsume(string nonce, long expiresAt, long now)
    {
        foreach (var item in _consumed.Where(item => item.Value < now))
        {
            _consumed.TryRemove(item.Key, out _);
        }

        return _consumed.TryAdd(nonce, expiresAt);
    }
}

internal sealed class ManagerSessionTicketValidator(
    ManagerIdentityOptions options,
    ManagerSessionNonceStore nonceStore,
    TimeProvider timeProvider)
{
    private const int MaximumTicketLength = 12_000;
    private const long MaximumLifetimeSeconds = 60;
    private const long AllowedClockSkewSeconds = 30;

    public ManagerSessionTicketValidation ValidateAndConsume(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > MaximumTicketLength)
        {
            return ManagerSessionTicketValidation.Reject("missing_or_oversized");
        }

        var segments = ticket.Split('.');
        if (segments.Length != 3 || segments[0] != "v1")
        {
            return ManagerSessionTicketValidation.Reject("invalid_format");
        }

        byte[] providedSignature;
        byte[] payloadBytes;
        try
        {
            providedSignature = WebEncoders.Base64UrlDecode(segments[2]);
            payloadBytes = WebEncoders.Base64UrlDecode(segments[1]);
        }
        catch (FormatException)
        {
            return ManagerSessionTicketValidation.Reject("invalid_encoding");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SharedSecret));
        var expectedSignature = hmac.ComputeHash(
            Encoding.ASCII.GetBytes($"v1.{segments[1]}"));
        if (providedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(
                providedSignature,
                expectedSignature))
        {
            return ManagerSessionTicketValidation.Reject("invalid_signature");
        }

        ManagerSessionTicketPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ManagerSessionTicketPayload>(
                payloadBytes,
                JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return ManagerSessionTicketValidation.Reject("invalid_payload");
        }

        if (payload is null ||
            payload.Issuer != options.Issuer ||
            payload.Audience != options.Audience ||
            !Guid.TryParse(payload.Subject, out _) ||
            payload.DisplayName is null ||
            payload.DisplayName.Length > 200 ||
            payload.Email is null ||
            payload.Email.Length > 320 ||
            payload.Language is not ("en" or "fr") ||
            string.IsNullOrWhiteSpace(payload.Nonce) ||
            payload.Nonce.Length > 128 ||
            payload.Permissions is null ||
            payload.Permissions.Any(permission => !ManagerPermissions.All.Contains(permission)) ||
            !payload.Permissions.Contains(ManagerPermissions.Operate, StringComparer.Ordinal))
        {
            return ManagerSessionTicketValidation.Reject("invalid_claims");
        }

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var lifetime = payload.ExpiresAtUnixSeconds - payload.IssuedAtUnixSeconds;
        if (payload.IssuedAtUnixSeconds > now + AllowedClockSkewSeconds ||
            payload.ExpiresAtUnixSeconds <= now ||
            lifetime is <= 0 or > MaximumLifetimeSeconds)
        {
            return ManagerSessionTicketValidation.Reject("expired_or_invalid_lifetime");
        }

        if (!nonceStore.TryConsume(payload.Nonce, payload.ExpiresAtUnixSeconds, now))
        {
            return ManagerSessionTicketValidation.Reject("replayed");
        }

        return ManagerSessionTicketValidation.Success(payload);
    }
}
