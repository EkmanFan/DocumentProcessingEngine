using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DocumentProcessing.Core.Hybrid;

namespace DocumentProcessing.Core.DualRun.Transport;

/// <summary>
/// Versioned deterministic SHA-256 projections matching the current Dual Run
/// text-comparison semantics without transporting HybridDocumentElement graphs.
/// </summary>
public static class DocumentDualRunTextFingerprint
{
    #region Variables and Constants

    private const string SelectedTextSequenceProjectionId =
        "dual-run-selected-text-sequence-v1";

    private const string TextProjectionId =
        "dual-run-text-projection-v1";

    #endregion

    #region Methods Public

    public static string SelectedTextSequenceSha256(
        IReadOnlyList<HybridDocumentElement> authoritativeText)
    {
        ValidateAuthoritativeText(
            authoritativeText);

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        AppendString(
            hash,
            SelectedTextSequenceProjectionId);

        AppendInt32(
            hash,
            authoritativeText.Count);

        foreach (var element in
                 authoritativeText)
        {
            AppendString(
                hash,
                element.Text!);
        }

        return LowerHex(
            hash.GetHashAndReset());
    }

    public static string TextProjectionSha256(
        IReadOnlyList<HybridDocumentElement> authoritativeText)
    {
        ValidateAuthoritativeText(
            authoritativeText);

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        AppendString(
            hash,
            TextProjectionId);

        AppendInt32(
            hash,
            authoritativeText.Count);

        foreach (var element in
                 authoritativeText)
        {
            AppendInt32(
                hash,
                element.ReadingOrder);

            AppendInt32(
                hash,
                (int)element.Kind);

            AppendDouble(
                hash,
                element.Bounds.Left);

            AppendDouble(
                hash,
                element.Bounds.Top);

            AppendDouble(
                hash,
                element.Bounds.Right);

            AppendDouble(
                hash,
                element.Bounds.Bottom);

            AppendString(
                hash,
                element.Text!);

            AppendInt32(
                hash,
                (int)element.TextOrigin);

            AppendNullableInt32(
                hash,
                element
                    .NativeBlock
                    ?.SourceSequence);

            AppendBoolean(
                hash,
                element.Reconciliation is not null);
        }

        return LowerHex(
            hash.GetHashAndReset());
    }

    #endregion

    #region Methods Validation

    private static void ValidateAuthoritativeText(
        IReadOnlyList<HybridDocumentElement> authoritativeText)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeText);

        if (authoritativeText.Any(
                element =>
                    element is null ||
                    !element.HasAuthoritativeText))
        {
            throw new ArgumentException(
                "Dual Run text fingerprint input must contain authoritative text elements only.",
                nameof(authoritativeText));
        }
    }

    #endregion

    #region Methods Encoding

    private static void AppendString(
        IncrementalHash hash,
        string value)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                value);

        AppendInt32(
            hash,
            bytes.Length);

        hash.AppendData(
            bytes);
    }

    private static void AppendInt32(
        IncrementalHash hash,
        int value)
    {
        Span<byte> buffer =
            stackalloc byte[sizeof(int)];

        BinaryPrimitives
            .WriteInt32LittleEndian(
                buffer,
                value);

        hash.AppendData(
            buffer);
    }

    private static void AppendNullableInt32(
        IncrementalHash hash,
        int? value)
    {
        AppendBoolean(
            hash,
            value.HasValue);

        if (value.HasValue)
        {
            AppendInt32(
                hash,
                value.Value);
        }
    }

    private static void AppendDouble(
        IncrementalHash hash,
        double value)
    {
        Span<byte> buffer =
            stackalloc byte[sizeof(long)];

        var canonical =
            value ==
            0d
                ? 0d
                : value;

        BinaryPrimitives
            .WriteInt64LittleEndian(
                buffer,
                BitConverter.DoubleToInt64Bits(
                    canonical));

        hash.AppendData(
            buffer);
    }

    private static void AppendBoolean(
        IncrementalHash hash,
        bool value)
    {
        Span<byte> buffer =
            stackalloc byte[1];

        buffer[0] =
            value
                ? (byte)1
                : (byte)0;

        hash.AppendData(
            buffer);
    }

    private static string LowerHex(
        byte[] value) =>
        Convert
            .ToHexString(
                value)
            .ToLowerInvariant();

    #endregion
}
