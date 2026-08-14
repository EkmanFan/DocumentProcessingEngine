using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Portable custody projection for one structural segment.
///
/// The segment remains a neutral document-structure unit, not a retrieval
/// chunk. Text/hash consistency and local source-element uniqueness are
/// intrinsic invariants of the portable model.
/// </summary>
public sealed record DocumentSegmentProvenance
{
    public DocumentSegmentProvenance(
        string sourceDocumentSha256,
        string segmentId,
        int ordinal,
        string text,
        string textSha256,
        string? headingText,
        int firstPhysicalPageNumber,
        int lastPhysicalPageNumber,
        IReadOnlyList<string> sourceElementIds,
        IReadOnlyList<TextSelectionOrigin> textOrigins,
        bool hasUnresolvedEvidence)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (string.IsNullOrWhiteSpace(segmentId))
        {
            throw new ArgumentException(
                "Segment ID cannot be empty.",
                nameof(segmentId));
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Segment text cannot be empty.",
                nameof(text));
        }

        if (firstPhysicalPageNumber <= 0 ||
            lastPhysicalPageNumber < firstPhysicalPageNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstPhysicalPageNumber));
        }

        ArgumentNullException.ThrowIfNull(sourceElementIds);
        ArgumentNullException.ThrowIfNull(textOrigins);

        if (sourceElementIds.Count == 0)
        {
            throw new ArgumentException(
                "Segment must retain at least one source element ID.",
                nameof(sourceElementIds));
        }

        if (sourceElementIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Segment source element IDs cannot be empty.",
                nameof(sourceElementIds));
        }

        var normalizedSourceElementIds =
            sourceElementIds
                .Select(value => value.Trim())
                .ToArray();

        if (normalizedSourceElementIds
                .Distinct(StringComparer.Ordinal)
                .Count() !=
            normalizedSourceElementIds.Length)
        {
            throw new ArgumentException(
                "A structural segment cannot reference the same source element more than once.",
                nameof(sourceElementIds));
        }

        SegmentId = segmentId.Trim();
        Ordinal = ordinal;
        Text = text;

        TextSha256 =
            NormalizeAndVerifyTextSha256(
                text,
                textSha256,
                nameof(textSha256));

        HeadingText =
            string.IsNullOrWhiteSpace(headingText)
                ? null
                : headingText.Trim();

        FirstPhysicalPageNumber = firstPhysicalPageNumber;
        LastPhysicalPageNumber = lastPhysicalPageNumber;
        SourceElementIds = normalizedSourceElementIds;
        TextOrigins = textOrigins.ToArray();
        HasUnresolvedEvidence = hasUnresolvedEvidence;
    }

    public string SourceDocumentSha256 { get; }
    public string SegmentId { get; }
    public int Ordinal { get; }
    public string Text { get; }
    public string TextSha256 { get; }
    public string? HeadingText { get; }
    public int FirstPhysicalPageNumber { get; }
    public int LastPhysicalPageNumber { get; }
    public IReadOnlyList<string> SourceElementIds { get; }
    public IReadOnlyList<TextSelectionOrigin> TextOrigins { get; }
    public bool IsMixedTextOrigin => TextOrigins.Count > 1;
    public bool HasUnresolvedEvidence { get; }

    private static string NormalizeAndVerifyTextSha256(
        string text,
        string value,
        string parameterName)
    {
        var normalized =
            NormalizeSha256(
                value,
                parameterName);

        if (!ProvenanceTextHashing.MatchesUtf8Sha256(
                text,
                normalized))
        {
            throw new ArgumentException(
                "Segment text SHA-256 does not match the exact UTF-8 text.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "SHA-256 value cannot be empty.",
                parameterName);
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
