namespace DocumentProcessing.Core.Quality;

/// <summary>
/// Deterministic quality facts aggregated over one structural segment.
///
/// Counts are derived from exact source-element membership. No threshold,
/// severity, admissibility decision, or opaque score is attached.
/// </summary>
public sealed record DocumentSegmentQualityObservations
{
    public DocumentSegmentQualityObservations(
        string sourceDocumentSha256,
        string segmentId,
        int sourceElementCount,
        int authoritativeTextElementCount,
        int nativeTextElementCount,
        int ocrTextElementCount,
        int visualElementCount,
        int unresolvedTextElementCount,
        int deferredElementCount,
        int excludedElementCount,
        int reconciliationDivergenceElementCount,
        int normalizationChangedTextElementCount,
        int ocrEvidenceElementCount,
        int ocrEvidenceWithoutConfidenceObservationElementCount,
        bool isMixedTextOrigin,
        bool hasUnresolvedEvidence)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (string.IsNullOrWhiteSpace(
                segmentId))
        {
            throw new ArgumentException(
                "Segment quality observation ID cannot be empty.",
                nameof(segmentId));
        }

        ValidateCount(
            sourceElementCount,
            nameof(sourceElementCount));

        ValidateCount(
            authoritativeTextElementCount,
            nameof(authoritativeTextElementCount));

        ValidateCount(
            nativeTextElementCount,
            nameof(nativeTextElementCount));

        ValidateCount(
            ocrTextElementCount,
            nameof(ocrTextElementCount));

        ValidateCount(
            visualElementCount,
            nameof(visualElementCount));

        ValidateCount(
            unresolvedTextElementCount,
            nameof(unresolvedTextElementCount));

        ValidateCount(
            deferredElementCount,
            nameof(deferredElementCount));

        ValidateCount(
            excludedElementCount,
            nameof(excludedElementCount));

        ValidateCount(
            reconciliationDivergenceElementCount,
            nameof(reconciliationDivergenceElementCount));

        ValidateCount(
            normalizationChangedTextElementCount,
            nameof(normalizationChangedTextElementCount));

        ValidateCount(
            ocrEvidenceElementCount,
            nameof(ocrEvidenceElementCount));

        ValidateCount(
            ocrEvidenceWithoutConfidenceObservationElementCount,
            nameof(ocrEvidenceWithoutConfidenceObservationElementCount));

        if (authoritativeTextElementCount >
                sourceElementCount ||
            visualElementCount >
                sourceElementCount ||
            unresolvedTextElementCount >
                sourceElementCount ||
            deferredElementCount >
                sourceElementCount ||
            excludedElementCount >
                sourceElementCount ||
            reconciliationDivergenceElementCount >
                sourceElementCount ||
            normalizationChangedTextElementCount >
                sourceElementCount ||
            ocrEvidenceElementCount >
                sourceElementCount)
        {
            throw new ArgumentException(
                "Segment quality counts cannot exceed source element count.");
        }

        if (nativeTextElementCount +
                ocrTextElementCount !=
            authoritativeTextElementCount)
        {
            throw new ArgumentException(
                "Native + OCR authoritative text counts must equal authoritative text element count.");
        }

        if (ocrEvidenceWithoutConfidenceObservationElementCount >
            ocrEvidenceElementCount)
        {
            throw new ArgumentException(
                "OCR evidence without confidence cannot exceed OCR evidence element count.");
        }

        SegmentId =
            segmentId.Trim();

        SourceElementCount =
            sourceElementCount;

        AuthoritativeTextElementCount =
            authoritativeTextElementCount;

        NativeTextElementCount =
            nativeTextElementCount;

        OcrTextElementCount =
            ocrTextElementCount;

        VisualElementCount =
            visualElementCount;

        UnresolvedTextElementCount =
            unresolvedTextElementCount;

        DeferredElementCount =
            deferredElementCount;

        ExcludedElementCount =
            excludedElementCount;

        ReconciliationDivergenceElementCount =
            reconciliationDivergenceElementCount;

        NormalizationChangedTextElementCount =
            normalizationChangedTextElementCount;

        OcrEvidenceElementCount =
            ocrEvidenceElementCount;

        OcrEvidenceWithoutConfidenceObservationElementCount =
            ocrEvidenceWithoutConfidenceObservationElementCount;

        IsMixedTextOrigin =
            isMixedTextOrigin;

        HasUnresolvedEvidence =
            hasUnresolvedEvidence;
    }

    public string SourceDocumentSha256 { get; }

    public string SegmentId { get; }

    public int SourceElementCount { get; }

    public int AuthoritativeTextElementCount { get; }

    public int NativeTextElementCount { get; }

    public int OcrTextElementCount { get; }

    public int VisualElementCount { get; }

    public int UnresolvedTextElementCount { get; }

    public int DeferredElementCount { get; }

    public int ExcludedElementCount { get; }

    public int ReconciliationDivergenceElementCount { get; }

    public int NormalizationChangedTextElementCount { get; }

    public int OcrEvidenceElementCount { get; }

    public int OcrEvidenceWithoutConfidenceObservationElementCount { get; }

    public bool IsMixedTextOrigin { get; }

    public bool HasUnresolvedEvidence { get; }

    private static void ValidateCount(
        int value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Quality observation count cannot be negative.");
        }
    }

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
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
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
