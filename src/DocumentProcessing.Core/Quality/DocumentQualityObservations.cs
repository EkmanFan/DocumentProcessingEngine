using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Quality;

/// <summary>
/// Portable document-level collection of deterministic quality observations.
///
/// Aggregate counters are derived from element/segment observations rather than
/// persisted as independent claims.
/// </summary>
public sealed record DocumentQualityObservations
{
    public DocumentQualityObservations(
        string sourceDocumentSha256,
        IReadOnlyList<DocumentElementQualityObservations> elements,
        IReadOnlyList<DocumentSegmentQualityObservations> segments)
    {
        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        ArgumentNullException.ThrowIfNull(
            elements);

        ArgumentNullException.ThrowIfNull(
            segments);

        var elementArray =
            elements.ToArray();

        var segmentArray =
            segments.ToArray();

        if (elementArray.Any(
                element =>
                    !string.Equals(
                        element.SourceDocumentSha256,
                        SourceDocumentSha256,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every element quality observation must belong to the declared source document.",
                nameof(elements));
        }

        if (segmentArray.Any(
                segment =>
                    !string.Equals(
                        segment.SourceDocumentSha256,
                        SourceDocumentSha256,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every segment quality observation must belong to the declared source document.",
                nameof(segments));
        }

        if (elementArray
                .Select(
                    element =>
                        element.ElementId)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            elementArray.Length)
        {
            throw new ArgumentException(
                "Element quality observation IDs must be unique.",
                nameof(elements));
        }

        if (segmentArray
                .Select(
                    segment =>
                        segment.SegmentId)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            segmentArray.Length)
        {
            throw new ArgumentException(
                "Segment quality observation IDs must be unique.",
                nameof(segments));
        }

        Elements = elementArray;
        Segments = segmentArray;
    }

    public string SourceDocumentSha256 { get; }

    public IReadOnlyList<DocumentElementQualityObservations> Elements { get; }

    public IReadOnlyList<DocumentSegmentQualityObservations> Segments { get; }

    public int ElementCount =>
        Elements.Count;

    public int AuthoritativeTextElementCount =>
        Elements.Count(
            element =>
                element.HasAuthoritativeText);

    public int NativeTextElementCount =>
        Elements.Count(
            element =>
                element.HasAuthoritativeText &&
                element.TextOrigin ==
                    TextSelectionOrigin.Native);

    public int OcrTextElementCount =>
        Elements.Count(
            element =>
                element.HasAuthoritativeText &&
                element.TextOrigin ==
                    TextSelectionOrigin.Ocr);

    public int VisualElementCount =>
        Elements.Count(
            element =>
                element.Kind ==
                HybridDocumentElementKind.Visual);

    public int UnresolvedTextElementCount =>
        Elements.Count(
            element =>
                element.Kind ==
                HybridDocumentElementKind.UnresolvedText);

    public int DeferredElementCount =>
        Elements.Count(
            element =>
                element.Kind ==
                HybridDocumentElementKind.Deferred);

    public int ExcludedElementCount =>
        Elements.Count(
            element =>
                element.IsExcluded);

    public int ReconciliationDivergenceElementCount =>
        Elements.Count(
            element =>
                element.HasReconciliationDivergence);

    public int NormalizationChangedTextElementCount =>
        Elements.Count(
            element =>
                element.NormalizationChangedText);

    public int OcrEvidenceElementCount =>
        Elements.Count(
            element =>
                element.HasOcrEvidence);

    public int OcrEvidenceWithoutConfidenceObservationElementCount =>
        Elements.Count(
            element =>
                element.HasOcrEvidence &&
                !element.HasOcrConfidenceObservations);

    public int SegmentCount =>
        Segments.Count;

    public int MixedTextOriginSegmentCount =>
        Segments.Count(
            segment =>
                segment.IsMixedTextOrigin);

    public int SegmentsWithUnresolvedEvidenceCount =>
        Segments.Count(
            segment =>
                segment.HasUnresolvedEvidence);

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
