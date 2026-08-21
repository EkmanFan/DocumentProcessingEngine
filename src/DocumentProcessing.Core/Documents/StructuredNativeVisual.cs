using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// One referenced source-native visual candidate and the deterministic source
/// facts used by Engine preservation policy.
/// </summary>
public sealed record StructuredNativeVisual
{
    #region Properties

    public string VisualId { get; }

    public DocumentSourceLocation Location { get; }

    public string SourceResourceId { get; }

    public string MediaType { get; }

    public bool IsAuxiliary { get; }

    public bool IsPublicationCover { get; }

    public bool IsNavigation { get; }

    public bool IsExplicitlyPresentationOnly { get; }

    public bool IsPreliminaryMatter { get; }

    public bool HasBodyMatterBoundary { get; }

    #endregion

    #region ctor

    public StructuredNativeVisual(
        string visualId,
        DocumentSourceLocation location,
        string sourceResourceId,
        string mediaType,
        bool isAuxiliary,
        bool isPublicationCover = false,
        bool isNavigation = false,
        bool isExplicitlyPresentationOnly = false,
        bool isPreliminaryMatter = false,
        bool hasBodyMatterBoundary = false)
    {
        if (string.IsNullOrWhiteSpace(
                visualId))
        {
            throw new ArgumentException(
                "Structured native visual ID cannot be empty.",
                nameof(visualId));
        }

        Location =
            location ??
            throw new ArgumentNullException(
                nameof(location));

        if (string.IsNullOrWhiteSpace(
                sourceResourceId))
        {
            throw new ArgumentException(
                "Structured native visual source resource cannot be empty.",
                nameof(sourceResourceId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType.Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Structured native visual media type must be an image media type.",
                nameof(mediaType));
        }

        VisualId =
            visualId.Trim();

        SourceResourceId =
            sourceResourceId.Trim();

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        IsAuxiliary =
            isAuxiliary;

        IsPublicationCover =
            isPublicationCover;

        IsNavigation =
            isNavigation;

        IsExplicitlyPresentationOnly =
            isExplicitlyPresentationOnly;

        IsPreliminaryMatter =
            isPreliminaryMatter;

        HasBodyMatterBoundary =
            hasBodyMatterBoundary;
    }

    #endregion
}
