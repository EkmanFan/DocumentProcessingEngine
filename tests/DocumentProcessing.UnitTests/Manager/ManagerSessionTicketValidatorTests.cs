using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerSessionTicketValidatorTests
{
    private const string SharedSecret =
        "manager-session-unit-test-shared-secret-2026";
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-09-03T10:00:00Z");

    [Fact]
    public void ValidateAndConsume_AcceptsAValidTicketOnlyOnce()
    {
        var validator = CreateValidator();
        var ticket = CreateTicket(CreatePayload(
            permissions:
            [
                ManagerPermissions.Operate,
                ManagerPermissions.ReplayDelivery
            ]));

        var accepted = validator.ValidateAndConsume(ticket);
        var replayed = validator.ValidateAndConsume(ticket);

        Assert.True(accepted.IsValid);
        Assert.Equal("Mallory", accepted.Payload!.DisplayName);
        Assert.Contains(ManagerPermissions.ReplayDelivery, accepted.Payload.Permissions);
        Assert.False(replayed.IsValid);
        Assert.Equal("replayed", replayed.Failure);
    }

    [Fact]
    public void ValidateAndConsume_RejectsTamperingExpiryAndWrongAudience()
    {
        var validator = CreateValidator();
        var validTicket = CreateTicket(CreatePayload());
        var segments = validTicket.Split('.');
        var tamperedPayload = segments[1][0] == 'A'
            ? $"B{segments[1][1..]}"
            : $"A{segments[1][1..]}";

        Assert.Equal(
            "invalid_signature",
            validator.ValidateAndConsume(
                $"v1.{tamperedPayload}.{segments[2]}").Failure);
        Assert.Equal(
            "expired_or_invalid_lifetime",
            validator.ValidateAndConsume(CreateTicket(CreatePayload(
                issuedAt: Now.AddMinutes(-2),
                expiresAt: Now.AddMinutes(-1),
                nonce: "expired"))).Failure);
        Assert.Equal(
            "invalid_claims",
            validator.ValidateAndConsume(CreateTicket(CreatePayload(
                audience: "another-application",
                nonce: "wrong-audience"))).Failure);
    }

    [Fact]
    public void ValidateAndConsume_RejectsUnknownOrMissingOperationPermission()
    {
        var validator = CreateValidator();

        Assert.Equal(
            "invalid_claims",
            validator.ValidateAndConsume(CreateTicket(CreatePayload(
                permissions: ["identity.accounts.manage"],
                nonce: "unknown-permission"))).Failure);
        Assert.Equal(
            "invalid_claims",
            validator.ValidateAndConsume(CreateTicket(CreatePayload(
                permissions: [ManagerPermissions.ReplayDelivery],
                nonce: "missing-operate"))).Failure);
    }

    [Fact]
    public void PrincipalValidator_ExpiresAnEstablishedManagerSession()
    {
        var clock = new StubTimeProvider(Now);
        var validator = new ManagerSessionPrincipalValidator(clock);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(
                    ManagerAuthenticationDefaults.SessionExpiresClaimType,
                    Now.AddMinutes(5).ToUnixTimeSeconds().ToString())
            ],
            ManagerAuthenticationDefaults.Scheme));

        Assert.True(validator.IsValid(principal));
        clock.UtcNow = Now.AddMinutes(6);
        Assert.False(validator.IsValid(principal));
    }

    [Fact]
    public void IdentityOptions_RejectUnsafeOriginsAndShortSecrets()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ManagerIdentityOptions.Load(CreateConfiguration(
                "http://studio.example/connect",
                SharedSecret)));
        Assert.Throws<InvalidOperationException>(() =>
            ManagerIdentityOptions.Load(CreateConfiguration(
                "https://studio.example/connect",
                "too-short")));

        var options = ManagerIdentityOptions.Load(CreateConfiguration(
            "https://studio.example/document-manager/connect",
            SharedSecret));
        Assert.Equal(TimeSpan.FromMinutes(5), options.SessionLifetime);
    }

    private static ManagerSessionTicketValidator CreateValidator() =>
        new(
            CreateOptions(),
            new ManagerSessionNonceStore(),
            new StubTimeProvider(Now));

    private static ManagerIdentityOptions CreateOptions() =>
        new(
            new Uri("https://studio.example/document-manager/connect"),
            SharedSecret,
            "apologia-studio",
            "document-manager-ui",
            TimeSpan.FromMinutes(5));

    private static ManagerSessionTicketPayload CreatePayload(
        IReadOnlyList<string>? permissions = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null,
        string audience = "document-manager-ui",
        string nonce = "unique-nonce")
    {
        var issuance = issuedAt ?? Now;
        return new ManagerSessionTicketPayload(
            "apologia-studio",
            audience,
            "11111111-1111-1111-1111-111111111111",
            "Mallory",
            "mallory@example.test",
            "en",
            permissions ?? [ManagerPermissions.Operate],
            issuance.ToUnixTimeSeconds(),
            (expiresAt ?? issuance.AddSeconds(30)).ToUnixTimeSeconds(),
            nonce);
    }

    private static string CreateTicket(ManagerSessionTicketPayload payload)
    {
        var payloadSegment = WebEncoders.Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions.Web));
        var signedValue = $"v1.{payloadSegment}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return $"{signedValue}.{WebEncoders.Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(signedValue)))}";
    }

    private static IConfiguration CreateConfiguration(
        string connectUrl,
        string secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ManagerIdentity:ApologiaConnectUrl"] = connectUrl,
                ["ManagerIdentity:SharedSecret"] = secret
            })
            .Build();

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
