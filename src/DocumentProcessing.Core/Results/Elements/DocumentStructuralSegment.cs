using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Portable structural segment assembled from processed document elements.
/// </summary>
/// <remarks>
/// A structural segment is a document-structure unit, not a retrieval chunk.
///
/// Source membership is expressed only through <see cref="SourceElementIds"/>.
/// The contract deliberately carries no physical-page span. A paginated
/// consumer can derive such a span from the locations of the referenced
/// elements, while EPUB or DOCX remain free of invented page coordinates.
/// </remarks>
public sealed record DocumentStructuralSegment
{
    #region Properties

    /// <summary>
    /// Gets the stable segment identifier within the result.
    /// </summary>
    public string SegmentId { get; }

    /// <summary>
    /// Gets the zero-based structural-segment order.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Gets the authoritative structural-segment text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the SHA-256 of the exact UTF-8 <see cref="Text"/>.
    /// </summary>
    public string TextSha256 { get; }

    /// <summary>
    /// Gets the optional structural heading.
    /// </summary>
    public string? HeadingText { get; }

    /// <summary>
    /// Gets the ordered source-element membership for this segment.
    /// </summary>
    public IReadOnlyList<string> SourceElementIds { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one portable structural segment.
    /// </summary>
    /// <param name="segmentId">Stable segment identifier within the result.</param>
    /// <param name="ordinal">Zero-based structural-segment order.</param>
    /// <param name="text">Authoritative structural-segment text.</param>
    /// <param name="textSha256">
    /// SHA-256 of the exact UTF-8 <paramref name="text"/>.
    /// </param>
    /// <param name="headingText">Optional structural heading.</param>
    /// <param name="sourceElementIds">
    /// Ordered source-element membership for this segment.
    /// </param>
    public DocumentStructuralSegment(
        string segmentId,
        int ordinal,
        string text,
        string textSha256,
        string? headingText,
        IReadOnlyList<string> sourceElementIds)
    {
        if (string.IsNullOrWhiteSpace(
                segmentId))
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

        if (string.IsNullOrWhiteSpace(
                text))
        {
            throw new ArgumentException(
                "Segment text cannot be empty.",
                nameof(text));
        }

        if (string.IsNullOrWhiteSpace(
                textSha256))
        {
            throw new ArgumentException(
                "Segment text SHA-256 cannot be empty.",
                nameof(textSha256));
        }

        if (!ProvenanceTextHashing.MatchesUtf8Sha256(
                text,
                textSha256))
        {
            throw new ArgumentException(
                "Segment text SHA-256 does not match the exact UTF-8 text.",
                nameof(textSha256));
        }

        ArgumentNullException.ThrowIfNull(
            sourceElementIds);

        if (sourceElementIds.Count == 0)
        {
            throw new ArgumentException(
                "Segment must retain at least one source element ID.",
                nameof(sourceElementIds));
        }

        if (sourceElementIds.Any(
                string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Segment source element IDs cannot be empty.",
                nameof(sourceElementIds));
        }

        var normalizedSourceElementIds =
            sourceElementIds
                .Select(
                    value =>
                        value.Trim())
                .ToArray();

        if (normalizedSourceElementIds
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            normalizedSourceElementIds.Length)
        {
            throw new ArgumentException(
                "A structural segment cannot reference the same source element more than once.",
                nameof(sourceElementIds));
        }

        SegmentId =
            segmentId.Trim();

        Ordinal =
            ordinal;

        Text =
            text;

        TextSha256 =
            textSha256.Trim()
                .ToLowerInvariant();

        HeadingText =
            string.IsNullOrWhiteSpace(
                headingText)
                ? null
                : headingText.Trim();

        SourceElementIds =
            normalizedSourceElementIds;
    }

    #endregion
}
