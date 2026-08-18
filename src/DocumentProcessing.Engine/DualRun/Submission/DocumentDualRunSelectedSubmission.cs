using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun.Transport;

namespace DocumentProcessing.Engine.DualRun.Submission;

/// <summary>
/// Expensive submission envelope created only after the document-level Dual Run
/// selection has resolved to selected.
///
/// Future DocumentProcessor integration must build authoritative fingerprints
/// and this object only inside the selected branch.
/// </summary>
public sealed record DocumentDualRunSelectedSubmission
{
    #region ctor

    public DocumentDualRunSelectedSubmission(
        DocumentSource source,
        string sourceDocumentSha256,
        long sourceByteLength,
        DocumentFormatId format,
        string engineVersion,
        IEnumerable<DocumentDualRunAuthoritativePageBaseline> authoritativePages)
    {
        Source =
            source ??
            throw new ArgumentNullException(
                nameof(source));

        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (sourceByteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceByteLength));
        }

        SourceByteLength =
            sourceByteLength;

        Format =
            format;

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Dual Run engine version cannot be empty.",
                nameof(engineVersion));
        }

        EngineVersion =
            engineVersion.Trim();

        ArgumentNullException.ThrowIfNull(
            authoritativePages);

        var pages =
            authoritativePages
                .ToArray();

        if (pages.Length ==
            0)
        {
            throw new ArgumentException(
                "Selected Dual Run submission requires authoritative page baselines.",
                nameof(authoritativePages));
        }

        for (var index = 0;
             index <
             pages.Length;
             index++)
        {
            var page =
                pages[index] ??
                throw new ArgumentException(
                    "Authoritative page baselines cannot contain null values.",
                    nameof(authoritativePages));

            var expectedPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPageNumber)
            {
                throw new ArgumentException(
                    $"Authoritative page baselines must be contiguous and one-based; " +
                    $"expected page {expectedPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(authoritativePages));
            }
        }

        AuthoritativePages =
            Array.AsReadOnly(
                pages);
    }

    #endregion

    #region Properties

    public DocumentSource Source { get; }

    public string SourceDocumentSha256 { get; }

    public long SourceByteLength { get; }

    public DocumentFormatId Format { get; }

    public string EngineVersion { get; }

    public IReadOnlyList<DocumentDualRunAuthoritativePageBaseline>
        AuthoritativePages { get; }

    #endregion

    #region Methods Validation

    private static string NormalizeSha256(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Dual Run source SHA-256 cannot be empty.",
                parameterName);
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Dual Run source SHA-256 must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }

    #endregion
}
