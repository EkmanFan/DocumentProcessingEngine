namespace DocumentProcessing.Core.DualRun.Transport;

/// <summary>
/// Version identifiers and fixed spool-file names for the local Dual Run V1
/// worker protocol.
/// </summary>
public static class DocumentDualRunTransportSchema
{
    #region Variables and Constants

    public const string RequestV1 =
        "document-dual-run-request-v1";

    public const string ResultV1 =
        "document-dual-run-result-v1";

    public const string SourceSnapshotFileName =
        "source.bin";

    public const string RequestFileName =
        "request.json";

    public const string ResultFileName =
        "result.json";

    #endregion
}

/// <summary>
/// Raised when a valid JSON object declares a transport schema version that the
/// current process does not understand.
/// </summary>
public sealed class UnsupportedDocumentDualRunTransportSchemaException
    : Exception
{
    #region ctor

    public UnsupportedDocumentDualRunTransportSchemaException(
        string? observedSchemaVersion,
        string expectedSchemaVersion)
        : base(
            $"Unsupported Dual Run transport schema " +
            $"'{observedSchemaVersion ?? "<null>"}'; expected " +
            $"'{expectedSchemaVersion}'.")
    {
        ObservedSchemaVersion =
            observedSchemaVersion;

        ExpectedSchemaVersion =
            expectedSchemaVersion;
    }

    #endregion

    #region Properties

    public string? ObservedSchemaVersion { get; }

    public string ExpectedSchemaVersion { get; }

    #endregion
}

internal static class DocumentDualRunTransportValidation
{
    #region Methods Text

    public static string RequiredText(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Value cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }

    public static string? OptionalText(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();

    public static string Sha256(
        string? value,
        string parameterName)
    {
        var normalized =
            RequiredText(
                    value,
                    parameterName)
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }

    #endregion

    #region Methods Path

    public static string SourceSnapshotPath(
        string? value,
        string parameterName)
    {
        var supplied =
            RequiredText(
                value,
                parameterName);

        if (!Path.IsPathFullyQualified(
                supplied))
        {
            throw new ArgumentException(
                "Dual Run source snapshot path must be fully qualified.",
                parameterName);
        }

        var normalized =
            Path.GetFullPath(
                supplied);

        if (!string.Equals(
                Path.GetFileName(
                    normalized),
                DocumentDualRunTransportSchema.SourceSnapshotFileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Dual Run source snapshot must be named " +
                $"'{DocumentDualRunTransportSchema.SourceSnapshotFileName}'.",
                parameterName);
        }

        return normalized;
    }

    #endregion
}
