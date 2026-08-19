namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Portable processing evidence summarized for one structural segment.
/// </summary>
/// <remarks>
/// Segment custody references text-source kinds rather than PDF-specific
/// origins and contains no physical-page span. The structural segment itself
/// retains ordered source-element membership.
/// </remarks>
public sealed record DocumentSegmentProcessingEvidence
{
    #region ctor

    /// <summary>
    /// Creates portable processing evidence for one structural segment.
    /// </summary>
    public DocumentSegmentProcessingEvidence(
        string segmentId,
        IReadOnlyList<DocumentTextSourceKind> textSources,
        bool hasUnresolvedEvidence)
    {
        if (string.IsNullOrWhiteSpace(
                segmentId))
        {
            throw new ArgumentException(
                "Segment ID cannot be empty.",
                nameof(segmentId));
        }

        ArgumentNullException.ThrowIfNull(
            textSources);

        if (textSources.Count == 0)
        {
            throw new ArgumentException(
                "Segment processing evidence must contain at least one text source.",
                nameof(textSources));
        }

        if (textSources.Any(
                source =>
                    source ==
                    DocumentTextSourceKind.None))
        {
            throw new ArgumentException(
                "Structural-segment text sources cannot contain None.",
                nameof(textSources));
        }

        SegmentId =
            segmentId.Trim();

        TextSources =
            textSources
                .Distinct()
                .ToArray();

        HasUnresolvedEvidence =
            hasUnresolvedEvidence;
    }

    #endregion

    #region Properties

    public string SegmentId { get; }

    public IReadOnlyList<DocumentTextSourceKind> TextSources { get; }

    public bool IsMixedTextSource =>
        TextSources.Count > 1;

    public bool HasUnresolvedEvidence { get; }

    #endregion
}
