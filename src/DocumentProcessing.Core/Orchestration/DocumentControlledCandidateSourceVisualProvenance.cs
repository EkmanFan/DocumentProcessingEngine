using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Neutral source-occurrence provenance for a candidate visual selected for
/// preservation.
///
/// This deliberately does not invent layout-observation or Figure semantics.
/// The materialization describes exact bytes written by H.4D.3A/H.4D.3B.
/// </summary>
public sealed record DocumentControlledCandidateSourceVisualProvenance
{
    public DocumentControlledCandidateSourceVisualProvenance(
        string sourceDocumentSha256,
        SourceVisualAssetMaterialization materialization)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256);

        Materialization =
            materialization ??
            throw new ArgumentNullException(
                nameof(materialization));
    }

    public string SourceDocumentSha256 { get; }

    public SourceVisualAssetMaterialization Materialization { get; }

    public int PhysicalPageNumber =>
        Materialization.PhysicalPageNumber;

    public int SourceVisualIndex =>
        Materialization.SourceVisualIndex;

    private static string NormalizeSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Source SHA-256 cannot be empty.",
                nameof(value));
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Source SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }
}
